import { useEffect, useRef, useState } from 'react'
import { Lightbulb, RotateCcw } from 'lucide-react'
import { errorMessage, getTopic, isAbort, startReviewSession } from '../api/client'
import { daysSinceStudied, type LoadState, type ReviewSession, type TopicDetail } from '../api/types'
import ModalShell from './ModalShell'
import { getRiskPresentation } from './riskPresentation'

interface TopicDetailModalProps {
  /** Mount this component per opened topic; unmounting cancels its requests. */
  topicId: string
  onClose: () => void
  /** The quiz button started or resumed a review session on the server */
  onSessionStarted: (session: ReviewSession, topicTitle: string) => void
  /** Estimated micro-review duration shown in the insight copy */
  reviewMinutes?: number
}

function TopicDetailModal({ topicId, onClose, onSessionStarted, reviewMinutes = 5 }: TopicDetailModalProps) {
  const [detail, setDetail] = useState<LoadState<TopicDetail>>({ status: 'loading' })
  const [attempt, setAttempt] = useState(0)
  const [start, setStart] = useState<{ pending: boolean; error?: string }>({ pending: false })
  // Set while a start request is in flight: blocks duplicates and is aborted when the modal closes.
  const startRequest = useRef<AbortController | null>(null)

  useEffect(() => () => startRequest.current?.abort(), [])

  useEffect(() => {
    const controller = new AbortController()
    getTopic(topicId, controller.signal).then(
      (data) => setDetail({ status: 'ready', data }),
      (error: unknown) => {
        if (!controller.signal.aborted) setDetail({ status: 'error', message: errorMessage(error) })
      },
    )
    return () => controller.abort()
  }, [topicId, attempt])

  const retry = () => {
    setDetail({ status: 'loading' })
    setAttempt((current) => current + 1)
  }

  // Only this explicit click creates (or resumes) a session.
  const startQuiz = async (topicTitle: string) => {
    if (startRequest.current) return
    const controller = new AbortController()
    startRequest.current = controller
    setStart({ pending: true })

    try {
      const session = await startReviewSession(topicId, controller.signal)
      if (!controller.signal.aborted) onSessionStarted(session, topicTitle)
    } catch (error) {
      if (!isAbort(error) && !controller.signal.aborted) setStart({ pending: false, error: errorMessage(error) })
    } finally {
      if (startRequest.current === controller) startRequest.current = null
    }
  }

  return (
    <ModalShell isOpen onClose={onClose} labelledBy="topic-detail-title">
      {detail.status === 'ready' ? (
        <TopicDetailContent
          topic={detail.data}
          reviewMinutes={reviewMinutes}
          isStarting={start.pending}
          startError={start.error}
          onClose={onClose}
          onStartQuiz={() => void startQuiz(detail.data.title)}
        />
      ) : (
        <div className="pr-10" aria-busy={detail.status === 'loading'}>
          <h2 id="topic-detail-title" className="section-title">
            {detail.status === 'loading' ? 'Konu yükleniyor…' : 'Konu yüklenemedi'}
          </h2>
          {detail.status === 'error' && (
            <div role="alert">
              <p className="mt-2 text-sm text-ink-secondary">{detail.message}</p>
              <button type="button" className="btn-secondary mt-4 inline-flex items-center gap-1.5" onClick={retry}>
                <RotateCcw size={14} aria-hidden="true" />
                Tekrar Dene
              </button>
            </div>
          )}
        </div>
      )}
    </ModalShell>
  )
}

interface TopicDetailContentProps {
  topic: TopicDetail
  reviewMinutes: number
  isStarting: boolean
  startError?: string
  onClose: () => void
  onStartQuiz: () => void
}

function TopicDetailContent({ topic, reviewMinutes, isStarting, startError, onClose, onStartQuiz }: TopicDetailContentProps) {
  const risk = getRiskPresentation(topic)
  const reinforcement = topic.needsInitialReinforcement

  return (
    <>
      <header className="pr-10">
        <span className="badge badge-brand">{topic.category}</span>
        <h2 id="topic-detail-title" className="mt-2 text-2xl font-bold tracking-tight text-ink">
          {topic.title}
        </h2>
        <span className={`badge ${risk.badge} mt-2`}>
          {reinforcement ? `İlk test başarısı %${topic.initialTestAccuracy ?? 0}` : `%${topic.retention} Hatırlama`} ·{' '}
          {risk.label}
        </span>
      </header>

      <section className="mt-5 flex gap-3 rounded-tile bg-brand-soft p-card-sm" aria-labelledby="why-now-title">
        <span className="icon-tile size-9 bg-surface text-brand" aria-hidden="true">
          <Lightbulb size={18} />
        </span>
        <div>
          <h3 id="why-now-title" className="text-sm font-semibold text-ink">
            {reinforcement ? 'Neden Pekiştirme?' : 'Neden Şimdi Tekrar?'}
          </h3>
          {reinforcement ? (
            <p className="mt-1 text-sm text-ink-secondary">
              Bu konunun ilk testinde doğru yanıt oranın %{topic.initialTestAccuracy ?? 0}. Bu bir unutma değil; konu henüz
              yeterince oturmamış görünüyor. {reviewMinutes} dakikalık kısa bir pekiştirmeyle eksiklerini görebilir, ardından
              tekrarlarla güçlendirebilirsin.
            </p>
          ) : (
            <p className="mt-1 text-sm text-ink-secondary">
              Bu konuyu en son {daysSinceStudied(topic, topic.evaluatedAt)} gün önce çalıştın. Hafıza gücünü korumak ve
              unutmayı engellemek için {reviewMinutes} dakikalık hızlı bir pekiştirme öneriyoruz.
            </p>
          )}
        </div>
      </section>

      {topic.summaryPoints.length > 0 && (
        <section className="mt-5" aria-labelledby="quick-recall-title">
          <h3 id="quick-recall-title" className="section-title text-base">
            Hızlı Hatırlatma
          </h3>
          <ol className="mt-3 flex flex-col gap-2">
            {topic.summaryPoints.map(({ icon, tag, text, note }) => (
              <li key={tag} className="list-row items-start">
                <span className="icon-tile size-9 bg-surface text-lg shadow-card" aria-hidden="true">
                  {icon}
                </span>
                <div className="min-w-0 flex-1">
                  <span className="badge badge-brand">{tag}</span>
                  <p className="mt-1 text-base font-bold tracking-tight text-ink">{text}</p>
                  {note && <p className="text-xs text-ink-secondary">{note}</p>}
                </div>
              </li>
            ))}
          </ol>
        </section>
      )}

      {startError && (
        <p className="mt-5 text-sm text-danger-ink" role="alert">
          Test başlatılamadı. {startError}
        </p>
      )}

      <footer className="mt-6 flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
        <button type="button" className="btn-secondary" onClick={onClose}>
          Daha Sonra
        </button>
        <button
          type="button"
          className="btn-primary"
          onClick={onStartQuiz}
          disabled={topic.questionCount === 0 || isStarting}
        >
          {isStarting
            ? 'Test hazırlanıyor…'
            : reinforcement
              ? 'Pekiştirmeye Başla'
              : `${topic.questionCount} Soruluk Pekiştirme Testine Başla`}
        </button>
      </footer>
    </>
  )
}

export default TopicDetailModal
