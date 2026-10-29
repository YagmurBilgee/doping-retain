import { useEffect, useRef, useState } from 'react'
import { AlertTriangle, Check, RotateCcw } from 'lucide-react'
import { clearDemoHistory, errorMessage, getDemoNormalTest, isAbort, saveDemoNormalTest } from '../../api/client'
import type { DemoNormalTest, DemoTopicRow, LoadState } from '../../api/types'
import ModalShell from '../ModalShell'
import { formatDateTime, fromIstanbulInput, toIstanbulInput, topicContext } from '../reviewPresentation'

const OPTION_LETTERS = ['A', 'B', 'C', 'D', 'E']

/** Shown before every save or clear: both replace the topic's whole history for this one student. */
export const REPLACE_WARNING =
  'Bu işlem, yalnızca bu öğrencinin bu konudaki normal test ve mini test geçmişini değiştirir. Önceki tekrar kayıtları silinir ve haftalık özet etkilenebilir.'

type Mode = 'test' | 'none'

interface NormalTestEditorModalProps {
  /** Fixed for this mount; the panel remounts (and so closes) the editor when the student changes. */
  studentId: string
  studentName: string
  topicId: string
  topicTitle: string
  /** The backend's current instant (its clock may be a fixed demo clock); used for the default date and the future check */
  backendNow: () => number
  onClose: () => void
  /** The backend stored the change; its evaluated row is passed on, the panel reloads everything anyway. */
  onSaved: (row: DemoTopicRow, action: 'save' | 'clear') => void
}

/**
 * Demo editor of one student's normal test of one topic. The backend scores the answers and decides the
 * recommendation; the score here is only a preview. Saving replaces, and clearing removes, the student's
 * whole history of the topic (normal tests and mini quizzes), exactly as the backend contract defines.
 */
function NormalTestEditorModal({ studentId, studentName, topicId, topicTitle, backendNow, onClose, onSaved }: NormalTestEditorModalProps) {
  const [test, setTest] = useState<LoadState<DemoNormalTest>>({ status: 'loading' })
  const [attempt, setAttempt] = useState(0)
  const [mode, setMode] = useState<Mode>('test')
  const [answers, setAnswers] = useState<Record<string, number>>({})
  const [date, setDate] = useState('')
  const [time, setTime] = useState('')
  // The action waiting for the demonstrator's confirmation of the replacement warning.
  const [confirming, setConfirming] = useState<'save' | 'clear' | null>(null)
  const [submit, setSubmit] = useState<{ pending: boolean; error?: string }>({ pending: false })
  // One write at a time; a ref so a double click cannot slip in before the next render. Aborted on unmount.
  const inFlight = useRef<AbortController | null>(null)

  useEffect(() => () => inFlight.current?.abort(), [])

  useEffect(() => {
    const controller = new AbortController()
    getDemoNormalTest(studentId, topicId, controller.signal).then(
      (data) => {
        const saved = data.saved
        setAnswers(Object.fromEntries((saved?.answers ?? []).map((answer) => [answer.questionId, answer.selectedIndex])))
        const initial = toIstanbulInput(saved?.completedAt ?? new Date(backendNow()).toISOString())
        setDate(initial.date)
        setTime(initial.time)
        setMode(saved ? 'test' : 'none')
        setTest({ status: 'ready', data })
      },
      (error: unknown) => {
        if (!controller.signal.aborted) setTest({ status: 'error', message: errorMessage(error) })
      },
    )
    return () => controller.abort()
    // backendNow is read once per load; it is not a reason to reload.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [studentId, topicId, attempt])

  const retry = () => {
    setTest({ status: 'loading' })
    setAttempt((current) => current + 1)
  }

  const questions = test.status === 'ready' ? test.data.questions : []
  const unanswered = questions.filter((question) => answers[question.id] === undefined).length
  const preview = questions.filter((question) => answers[question.id] === question.correctIndex).length
  const completedAt = fromIstanbulInput(date, time)
  const isFuture = completedAt !== null && Date.parse(completedAt) > backendNow()
  const saveProblem =
    unanswered > 0
      ? `${unanswered} soru cevapsız: her soru için bir seçenek seçin.`
      : completedAt === null
        ? 'Geçerli bir tarih ve saat girin.'
        : isFuture
          ? 'Test tarihi gelecekte olamaz.'
          : null

  const run = async (action: 'save' | 'clear') => {
    if (inFlight.current || test.status !== 'ready') return
    if (action === 'save' && (saveProblem || !completedAt)) return
    const controller = new AbortController()
    inFlight.current = controller
    setSubmit({ pending: true })

    try {
      const row =
        action === 'save'
          ? await saveDemoNormalTest(
              studentId,
              topicId,
              {
                answers: questions.map((question) => ({ questionId: question.id, selectedIndex: answers[question.id] })),
                completedAt: completedAt!,
              },
              controller.signal,
            )
          : await clearDemoHistory(studentId, topicId, controller.signal)
      if (!controller.signal.aborted) onSaved(row, action)
    } catch (error) {
      // The dialog stays open with every input intact.
      if (!isAbort(error) && !controller.signal.aborted) setSubmit({ pending: false, error: errorMessage(error) })
    } finally {
      if (inFlight.current === controller) inFlight.current = null
    }
  }

  const close = () => {
    if (!submit.pending) onClose()
  }

  return (
    <ModalShell isOpen size="wide" onClose={close} labelledBy="normal-test-editor-title">
      <header className="pr-10">
        <p className="section-subtitle">{studentName} · Demo düzenleme</p>
        <h2 id="normal-test-editor-title" className="mt-1 text-lg font-bold tracking-tight text-ink">
          {topicTitle}
        </h2>
        {test.status === 'ready' && <span className="badge badge-brand mt-1">{topicContext(test.data)}</span>}
      </header>

      {test.status === 'loading' && <p className="mt-6 text-sm text-ink-secondary">Normal test yükleniyor…</p>}
      {test.status === 'error' && (
        <div className="mt-6 flex flex-wrap items-center gap-3" role="alert">
          <p className="text-sm text-danger-ink">Test yüklenemedi. {test.message}</p>
          <button type="button" className="btn-secondary" onClick={retry}>
            <RotateCcw size={14} aria-hidden="true" /> Tekrar dene
          </button>
        </div>
      )}

      {test.status === 'ready' && (
        <>
          <fieldset className="mt-5" disabled={submit.pending}>
            <legend className="text-sm font-semibold text-ink">Konudaki test geçmişi</legend>
            <div className="mt-2 flex flex-wrap gap-2">
              {(
                [
                  ['test', 'Normal test sonucu'],
                  ['none', 'Test geçmişi yok'],
                ] as const
              ).map(([value, label]) => (
                <label
                  key={value}
                  className={`inline-flex cursor-pointer items-center gap-2 rounded-tile border px-3 py-2 text-sm ${
                    mode === value ? 'border-brand bg-brand-soft font-semibold text-brand' : 'border-line text-ink-secondary'
                  }`}
                >
                  <input
                    type="radio"
                    name="history-mode"
                    value={value}
                    checked={mode === value}
                    onChange={() => {
                      setMode(value)
                      setConfirming(null)
                    }}
                    className="accent-brand"
                  />
                  {label}
                </label>
              ))}
            </div>
            <p className="mt-2 text-xs text-ink-secondary">
              {mode === 'none'
                ? '“Test geçmişi yok”, 0/10 ile aynı değildir: konu için hiç sonuç kalmaz ve öneri “Henüz test verisi yok” olur.'
                : test.data.saved
                  ? `Kayıtlı son normal test: ${formatDateTime(test.data.saved.completedAt)} · ${test.data.saved.correctCount}/${test.data.saved.totalQuestions}`
                  : 'Bu konuda henüz normal test yok; cevapları ve tarihi girerek oluşturabilirsiniz.'}
            </p>
          </fieldset>

          {mode === 'test' && (
            <fieldset className="mt-5" disabled={submit.pending}>
              <legend className="text-sm font-semibold text-ink">Tamamlanma zamanı (Türkiye saati, Europe/Istanbul)</legend>
              <div className="mt-2 flex flex-wrap gap-3">
                <label className="flex flex-col gap-1 text-xs text-ink-secondary">
                  Tarih
                  <input
                    type="date"
                    value={date}
                    onChange={(event) => setDate(event.target.value)}
                    className="h-10 rounded-tile border border-line bg-surface px-3 text-sm text-ink"
                  />
                </label>
                <label className="flex flex-col gap-1 text-xs text-ink-secondary">
                  Saat
                  <input
                    type="time"
                    value={time}
                    onChange={(event) => setTime(event.target.value)}
                    className="h-10 rounded-tile border border-line bg-surface px-3 text-sm text-ink"
                  />
                </label>
              </div>

              <ol className="mt-5 flex flex-col gap-4">
                {questions.map((question, index) => (
                  <li key={question.id}>
                    <fieldset>
                      <legend className="text-sm font-semibold text-ink">
                        {index + 1}. {question.text}
                      </legend>
                      <div className="mt-2 grid gap-2 sm:grid-cols-2">
                        {question.options.map((option, optionIndex) => {
                          const isSelected = answers[question.id] === optionIndex
                          const isCorrect = question.correctIndex === optionIndex
                          return (
                            <label
                              key={optionIndex}
                              className={`flex cursor-pointer items-start gap-2 rounded-tile border px-3 py-2 text-sm transition-colors ${
                                isSelected ? 'border-brand bg-brand-soft text-ink' : 'border-line bg-surface text-ink-secondary hover:bg-surface-muted'
                              }`}
                            >
                              <input
                                type="radio"
                                name={question.id}
                                checked={isSelected}
                                onChange={() => {
                                  setAnswers((current) => ({ ...current, [question.id]: optionIndex }))
                                  setConfirming(null)
                                }}
                                className="mt-0.5 accent-brand"
                              />
                              <span className="min-w-0 flex-1">
                                <span className="font-semibold">{OPTION_LETTERS[optionIndex]})</span> {option}
                              </span>
                              {isCorrect && (
                                <span className="badge badge-success shrink-0">
                                  <Check size={12} aria-hidden="true" /> Doğru
                                </span>
                              )}
                            </label>
                          )
                        })}
                      </div>
                    </fieldset>
                  </li>
                ))}
              </ol>
            </fieldset>
          )}

          {/* Stays in view while the ten questions scroll. */}
          <div className="sticky -bottom-card -mx-card mt-5 -mb-card border-t border-line bg-surface px-card py-4">
            {mode === 'test' && (
              <p className="text-sm text-ink" aria-live="polite">
                Skor önizlemesi: <strong>{preview}/{questions.length}</strong>
                <span className="text-ink-muted"> · kesin skoru kayıtta sunucu hesaplar</span>
              </p>
            )}
            {mode === 'test' && saveProblem && <p className="mt-1 text-xs text-risk-ink">{saveProblem}</p>}

            {confirming && (
              <div className="mt-3 flex gap-2 rounded-tile bg-risk-soft p-3 text-sm text-risk-ink" role="alert">
                <AlertTriangle size={16} className="mt-0.5 shrink-0" aria-hidden="true" />
                <p>{REPLACE_WARNING}</p>
              </div>
            )}
            {submit.error && (
              <p className="mt-2 text-sm text-danger-ink" role="alert">
                Kaydedilemedi. {submit.error}
              </p>
            )}

            <div className="mt-3 flex flex-wrap justify-end gap-2">
              <button type="button" className="btn-secondary" onClick={confirming ? () => setConfirming(null) : close} disabled={submit.pending}>
                {confirming ? 'Vazgeç' : 'Kapat'}
              </button>
              {confirming ? (
                <button type="button" className="btn-primary disabled:opacity-60" disabled={submit.pending} onClick={() => void run(confirming)}>
                  {submit.pending ? 'Kaydediliyor…' : confirming === 'save' ? 'Evet, değiştir ve kaydet' : 'Evet, geçmişi temizle'}
                </button>
              ) : (
                <button
                  type="button"
                  className="btn-primary disabled:opacity-60"
                  disabled={submit.pending || (mode === 'test' && saveProblem !== null)}
                  onClick={() => setConfirming(mode === 'test' ? 'save' : 'clear')}
                >
                  {mode === 'test' ? 'Kaydet' : 'Geçmişi temizle'}
                </button>
              )}
            </div>
          </div>
        </>
      )}
    </ModalShell>
  )
}

export default NormalTestEditorModal
