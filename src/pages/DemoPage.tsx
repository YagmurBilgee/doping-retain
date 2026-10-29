import { useCallback, useEffect, useRef, useState } from 'react'
import { ArrowLeft, CheckCircle2, ExternalLink, Pencil, RotateCcw } from 'lucide-react'
import { ApiError, errorMessage, getDemoGroup, resetDemoGroup } from '../api/client'
import type { DemoGroupResponse, DemoTopicRow, LoadState, Student, StudentsResponse } from '../api/types'
import Logo from '../components/Logo'
import ModalShell from '../components/ModalShell'
import StudentSelector from '../components/StudentSelector'
import NormalTestEditorModal from '../components/demo/NormalTestEditorModal'
import { formatDateTime, getStatusPresentation, testTypeLabel } from '../components/reviewPresentation'
import { DEMO_PANEL_GROUP, onInAppLinkClick, urlFor } from '../demoSelection'

interface DemoPageProps {
  /** The selected sixth-grade student; fixed for this mount (App keys the page by it) */
  student: Student | null
  groupStudents: Student[]
  students: LoadState<StudentsResponse>
  demoEnabled: boolean
  onSelectStudent: (studentId: string) => void
  onOpenStudentScreen: (student: Student) => void
  onRetryStudents: () => void
}

interface RowProps {
  row: DemoTopicRow
}

function TopicName({ row: { topic } }: RowProps) {
  return (
    <>
      <p className="section-subtitle">
        {topic.category} · {topic.unitName}
      </p>
      <p className="font-semibold text-ink">{topic.title}</p>
      <p className="text-xs text-ink-muted">Konu no: {topic.id}</p>
    </>
  )
}

function NormalTestCell({ row: { latestNormalTest } }: RowProps) {
  if (!latestNormalTest) return <p className="text-ink-muted">Test geçmişi yok</p>
  return (
    <>
      <p className="font-semibold text-ink">
        {latestNormalTest.correctCount}/{latestNormalTest.totalQuestions}
      </p>
      <p className="text-xs text-ink-secondary">{formatDateTime(latestNormalTest.completedAt)}</p>
    </>
  )
}

/** The latest observation the model evaluates: a normal test or a reminder mini quiz. */
function ObservationCell({ row: { topic } }: RowProps) {
  if (!topic.latestTest) return <p className="text-ink-muted">—</p>
  return (
    <>
      <p className="font-semibold text-ink">
        {testTypeLabel(topic.latestTest.testType)} · {topic.latestTest.correctCount}/{topic.latestTest.totalQuestions}
      </p>
      <p className="text-xs text-ink-secondary">{formatDateTime(topic.latestTest.completedAt)}</p>
    </>
  )
}

function RecommendationBadge({ row: { topic } }: RowProps) {
  const status = getStatusPresentation(topic.reviewStatus)
  return <span className={`badge ${status.badge} shrink-0`}>{status.label}</span>
}

/** The backend's review time; shown only for scheduled and due topics, where it is a real date. */
function NextReview({ row: { topic } }: RowProps) {
  const hasReviewDate = topic.reviewStatus === 'scheduled' || topic.reviewStatus === 'due'
  return <>{hasReviewDate && topic.nextReviewAt ? formatDateTime(topic.nextReviewAt) : '—'}</>
}

function EditButton({ row, onEdit }: RowProps & { onEdit: (row: DemoTopicRow) => void }) {
  return (
    <button
      type="button"
      className="btn-secondary px-3 py-1.5 text-xs"
      aria-label={`${row.topic.title} konusunu düzenle`}
      onClick={() => onEdit(row)}
    >
      <Pencil size={13} aria-hidden="true" /> Düzenle
    </button>
  )
}

/**
 * Demo panel of the sixth grade (/demo): every topic of the selected student with its last normal test, the
 * latest observation and the backend's recommendation, a normal-test editor per topic and the group reset.
 * Nothing is calculated here; after every change the panel reloads the backend's evaluation.
 */
function DemoPage({ student, groupStudents, students, demoEnabled, onSelectStudent, onOpenStudentScreen, onRetryStudents }: DemoPageProps) {
  const [group, setGroup] = useState<LoadState<DemoGroupResponse>>({ status: 'loading' })
  const [editing, setEditing] = useState<{ topicId: string; title: string } | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [resetState, setResetState] = useState<{ confirming: boolean; pending: boolean; error?: string }>({ confirming: false, pending: false })
  // The backend clock at the last load and when it was read, so "now" follows it (also with a fixed demo clock).
  // Null until the first load; the browser clock stands in until then.
  const clockRef = useRef<{ backend: number; local: number } | null>(null)
  const loadRequest = useRef<AbortController | null>(null)
  const resetInFlight = useRef(false)

  const load = useCallback(() => {
    loadRequest.current?.abort()
    const controller = new AbortController()
    loadRequest.current = controller
    getDemoGroup(controller.signal).then(
      (data) => {
        if (controller.signal.aborted) return
        clockRef.current = { backend: Date.parse(data.evaluatedAt), local: Date.now() }
        setGroup({ status: 'ready', data })
      },
      (error: unknown) => {
        if (controller.signal.aborted) return
        // 404: the backend does not map the demo endpoints (Demo:Enabled is off).
        setGroup(
          error instanceof ApiError && error.status === 404
            ? { status: 'error', message: 'disabled' }
            : { status: 'error', message: errorMessage(error) },
        )
      },
    )
  }, [])

  useEffect(() => {
    if (demoEnabled) load()
    return () => loadRequest.current?.abort()
  }, [demoEnabled, load])

  const backendNow = useCallback(() => {
    const clock = clockRef.current
    return clock ? clock.backend + (Date.now() - clock.local) : Date.now()
  }, [])

  const handleSaved = (row: DemoTopicRow, action: 'save' | 'clear') => {
    setEditing(null)
    const status = getStatusPresentation(row.topic.reviewStatus).label
    const result = row.latestNormalTest ? `${row.latestNormalTest.correctCount}/${row.latestNormalTest.totalQuestions}` : null
    setNotice(
      action === 'save'
        ? `${student?.displayName} · ${row.topic.title}: normal test kaydedildi (sunucu skoru ${result}). Öneri: ${status}.`
        : `${student?.displayName} · ${row.topic.title}: test geçmişi temizlendi. Öneri: ${status}.`,
    )
    load()
  }

  const reset = async () => {
    if (resetInFlight.current) return
    resetInFlight.current = true
    setResetState({ confirming: true, pending: true })
    try {
      await resetDemoGroup(DEMO_PANEL_GROUP)
      setResetState({ confirming: false, pending: false })
      setNotice('6. sınıf demo verileri sıfırlandı: beş öğrenci başlangıç senaryosuna döndü. YKS verileri değişmedi.')
      load()
    } catch (error) {
      setResetState({ confirming: true, pending: false, error: errorMessage(error) })
    } finally {
      resetInFlight.current = false
    }
  }

  const openEditor = (row: DemoTopicRow) => {
    setNotice(null)
    setEditing({ topicId: row.topic.id, title: row.topic.title })
  }

  const rows = group.status === 'ready' ? group.data.students.find((s) => s.id === student?.id)?.topics ?? [] : []
  const evaluatedAt = group.status === 'ready' ? group.data.evaluatedAt : null
  const disabled = (students.status === 'ready' && !demoEnabled) || (group.status === 'error' && group.message === 'disabled')
  const homeHref = urlFor('home', DEMO_PANEL_GROUP, student?.id ?? null)

  return (
    <div className="min-h-screen bg-canvas px-4 py-6 sm:px-7">
      <div className="mx-auto flex max-w-[1240px] flex-col gap-6">
        <header className="flex flex-wrap items-center justify-between gap-4">
          <div className="flex min-w-0 items-center gap-4">
            <Logo className="h-10 w-40 shrink-0" />
            <div className="min-w-0">
              <h1 className="font-manrope text-[26px] leading-tight font-bold text-heading">Demo Paneli</h1>
              <p className="section-subtitle">6. Sınıf · demo test sonuçlarını düzenleme</p>
            </div>
          </div>
          {!disabled && (
            <div className="flex flex-wrap items-center gap-3">
              <StudentSelector students={groupStudents} studentId={student?.id ?? null} onSelectStudent={onSelectStudent} />
              {student && (
                <a href={homeHref} onClick={onInAppLinkClick(() => onOpenStudentScreen(student))} className="btn-primary h-13">
                  <ExternalLink size={16} aria-hidden="true" />
                  Bu öğrencinin ekranını aç
                </a>
              )}
            </div>
          )}
        </header>

        {students.status === 'error' && (
          <div className="card flex flex-wrap items-center gap-3" role="alert">
            <p className="text-sm text-danger-ink">Öğrenci listesi yüklenemedi. {students.message}</p>
            <button type="button" className="btn-secondary" onClick={onRetryStudents}>
              <RotateCcw size={14} aria-hidden="true" /> Tekrar dene
            </button>
          </div>
        )}

        {disabled ? (
          <section className="card max-w-2xl">
            <h2 className="section-title">Demo paneli kullanılamıyor</h2>
            <p className="mt-2 text-sm text-ink-secondary">
              Demo düzenleme bu ortamda kapalı. Paneli açmak için backend'i <code className="font-semibold">Demo:Enabled=true</code> ayarıyla
              başlatın (örneğin <code className="font-semibold">Demo__Enabled=true</code> ortam değişkeni). Öğrenci ekranı bundan etkilenmez.
            </p>
            <a href={homeHref} onClick={onInAppLinkClick(() => student && onOpenStudentScreen(student))} className="btn-secondary mt-4">
              <ArrowLeft size={16} aria-hidden="true" /> Öğrenci ekranına dön
            </a>
          </section>
        ) : (
          <>
            {notice && (
              <div className="flex items-start gap-2 rounded-tile bg-success-soft px-4 py-3 text-sm text-success-ink" role="status">
                <CheckCircle2 size={18} className="mt-0.5 shrink-0" aria-hidden="true" />
                <p className="min-w-0 flex-1">{notice}</p>
                <button type="button" className="text-xs font-semibold underline" onClick={() => setNotice(null)}>
                  Kapat
                </button>
              </div>
            )}

            <section className="card" aria-labelledby="demo-topics-title">
              <div className="flex flex-wrap items-baseline justify-between gap-2">
                <h2 id="demo-topics-title" className="section-title">
                  {student ? `${student.displayName} · 8 konu` : 'Konular'}
                </h2>
                {evaluatedAt && <p className="section-subtitle">Değerlendirme anı: {formatDateTime(evaluatedAt)} (Türkiye saati)</p>}
              </div>

              {(group.status === 'loading' || students.status === 'loading') && <p className="mt-4 text-sm text-ink-secondary">Yükleniyor…</p>}
              {group.status === 'error' && !disabled && (
                <div className="mt-4 flex flex-wrap items-center gap-3" role="alert">
                  <p className="text-sm text-danger-ink">Panel yüklenemedi. {group.message}</p>
                  <button type="button" className="btn-secondary" onClick={load}>
                    <RotateCcw size={14} aria-hidden="true" /> Tekrar dene
                  </button>
                </div>
              )}

              {group.status === 'ready' && evaluatedAt && (
                <>
                  {/* From md up: a table that scrolls sideways when the long topic names need the room. */}
                  <div className="mt-4 hidden overflow-x-auto md:block" tabIndex={0} role="region" aria-label="Konu tablosu">
                    <table className="w-full min-w-[900px] border-collapse text-left text-sm">
                      <thead>
                        <tr className="border-b border-line text-xs text-ink-muted">
                          <th scope="col" className="py-2 pr-3 font-semibold">Ders · Ünite / Konu</th>
                          <th scope="col" className="px-3 py-2 font-semibold">Son normal test</th>
                          <th scope="col" className="px-3 py-2 font-semibold">Son gözlem</th>
                          <th scope="col" className="px-3 py-2 font-semibold">Öneri</th>
                          <th scope="col" className="px-3 py-2 font-semibold">Sonraki tekrar</th>
                          <th scope="col" className="py-2 pl-3 font-semibold">
                            <span className="sr-only">İşlem</span>
                          </th>
                        </tr>
                      </thead>
                      <tbody>
                        {rows.map((row) => (
                          <tr key={row.topic.id} className="border-b border-line align-top last:border-b-0">
                            <td className="py-3 pr-3">
                              <TopicName row={row} />
                            </td>
                            <td className="px-3 py-3">
                              <NormalTestCell row={row} />
                            </td>
                            <td className="px-3 py-3">
                              <ObservationCell row={row} />
                            </td>
                            <td className="px-3 py-3">
                              <RecommendationBadge row={row} />
                            </td>
                            <td className="px-3 py-3 text-ink-secondary">
                              <NextReview row={row} />
                            </td>
                            <td className="py-3 pl-3 text-right">
                              <EditButton row={row} onEdit={openEditor} />
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>

                  {/* Phones: the same fields as cards, so the page never needs zooming out. */}
                  <ul className="mt-4 flex flex-col gap-3 md:hidden">
                    {rows.map((row) => (
                      <li key={row.topic.id} className="rounded-tile bg-surface-muted p-card-sm">
                        <div className="flex items-start gap-2">
                          <div className="min-w-0 flex-1">
                            <TopicName row={row} />
                          </div>
                          <RecommendationBadge row={row} />
                        </div>
                        <dl className="mt-3 grid grid-cols-2 gap-x-3 gap-y-2 text-sm">
                          <div>
                            <dt className="section-subtitle">Son normal test</dt>
                            <dd>
                              <NormalTestCell row={row} />
                            </dd>
                          </div>
                          <div>
                            <dt className="section-subtitle">Son gözlem</dt>
                            <dd>
                              <ObservationCell row={row} />
                            </dd>
                          </div>
                          <div>
                            <dt className="section-subtitle">Sonraki tekrar</dt>
                            <dd className="text-ink-secondary">
                              <NextReview row={row} />
                            </dd>
                          </div>
                          <div className="flex items-end justify-end">
                            <EditButton row={row} onEdit={openEditor} />
                          </div>
                        </dl>
                      </li>
                    ))}
                  </ul>
                </>
              )}
            </section>

            <section className="card flex flex-wrap items-center justify-between gap-4" aria-labelledby="demo-reset-title">
              <div className="min-w-0 max-w-2xl">
                <h2 id="demo-reset-title" className="section-title">
                  6. sınıf demo verilerini sıfırla
                </h2>
                <p className="mt-1 text-sm text-ink-secondary">
                  Beş 6. sınıf öğrencisinin tüm test ve tekrar geçmişini başlangıç senaryosuna döndürür. YKS öğrencisinin verileri değişmez.
                </p>
              </div>
              <button type="button" className="btn-secondary" onClick={() => setResetState({ confirming: true, pending: false })}>
                <RotateCcw size={14} aria-hidden="true" /> 6. sınıf demo verilerini sıfırla
              </button>
            </section>
          </>
        )}
      </div>

      {student && editing && (
        <NormalTestEditorModal
          key={`${student.id}:${editing.topicId}`}
          studentId={student.id}
          studentName={student.displayName}
          topicId={editing.topicId}
          topicTitle={editing.title}
          backendNow={backendNow}
          onClose={() => setEditing(null)}
          onSaved={handleSaved}
        />
      )}

      <ModalShell
        isOpen={resetState.confirming}
        onClose={() => !resetState.pending && setResetState({ confirming: false, pending: false })}
        labelledBy="demo-reset-confirm-title"
      >
        <h2 id="demo-reset-confirm-title" className="pr-10 text-lg font-bold tracking-tight text-ink">
          6. sınıf demo verileri sıfırlansın mı?
        </h2>
        <p className="mt-3 text-sm text-ink-secondary">
          {groupStudents.map((s) => s.displayName).join(', ')}: beş öğrencinin normal test, mini test ve tekrar kayıtları silinir ve
          başlangıç senaryosu yeniden yüklenir. YKS öğrencisinin verileri değişmez.
        </p>
        {resetState.error && (
          <p className="mt-3 text-sm text-danger-ink" role="alert">
            Sıfırlanamadı. {resetState.error}
          </p>
        )}
        <div className="mt-5 flex flex-wrap justify-end gap-2">
          <button
            type="button"
            className="btn-secondary"
            disabled={resetState.pending}
            onClick={() => setResetState({ confirming: false, pending: false })}
          >
            Vazgeç
          </button>
          <button type="button" className="btn-primary disabled:opacity-60" disabled={resetState.pending} onClick={() => void reset()}>
            {resetState.pending ? 'Sıfırlanıyor…' : 'Evet, 6. sınıfı sıfırla'}
          </button>
        </div>
      </ModalShell>
    </div>
  )
}

export default DemoPage
