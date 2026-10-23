import { useEffect, useRef, useState } from 'react'
import { RotateCcw } from 'lucide-react'
import { errorMessage, getTopic, isAbort, startReviewSession } from '../api/client'
import type { LoadState, ReviewSession, TopicDetail } from '../api/types'
import ModalShell from './ModalShell'
import { daysSinceTest, formatScore, scheduleText, testTypeLabel } from './reviewPresentation'

interface TopicDetailModalProps {
  /** Mount this component per opened topic; unmounting cancels its requests. */
  topicId: string
  onClose: () => void
  /** The quiz button started or resumed a review session on the server */
  onSessionStarted: (session: ReviewSession, topicTitle: string) => void
}

function TopicDetailModal({ topicId, onClose, onSessionStarted }: TopicDetailModalProps) {
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
    <ModalShell isOpen size="wide" onClose={onClose} labelledBy="topic-detail-title">
      {detail.status === 'ready' ? (
        <TopicDetailContent
          topic={detail.data}
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

/**
 * Why a short review is suggested, from the backend status and dates; one sentence under the title.
 * The observed score is shown separately, below it.
 */
function reviewReason(topic: TopicDetail): string {
  switch (topic.reviewStatus) {
    case 'due': {
      const days = topic.latestTest ? daysSinceTest(topic.latestTest.completedAt, topic.evaluatedAt) : 0
      return days > 0
        ? `Son testinin üzerinden ${days} gün geçti. Kısa bir tekrarla bilgilerini tazele.`
        : 'Tekrar zamanı geldi. Kısa bir tekrarla bilgilerini tazele.'
    }
    case 'support_needed':
      return 'Son testte bazı soruları kaçırdın. Önce ilgili noktaları kısa bir hatırlatmayla pekiştir.'
    case 'scheduled': {
      const next = scheduleText(topic.reviewStatus, topic.nextReviewAt, topic.evaluatedAt)
      return `Bu konu için acele yok${next ? ` (${next.charAt(0).toLocaleLowerCase('tr-TR')}${next.slice(1)})` : ''}. İstersen şimdi de kısa bir tekrar yapabilirsin.`
    }
    case 'insufficient_data':
      return 'Bu konu için henüz test sonucu yok. Kısa hatırlatma ve mini testle ilk sonucunu oluşturabilirsin.'
  }
}

interface TopicDetailContentProps {
  topic: TopicDetail
  isStarting: boolean
  startError?: string
  onClose: () => void
  onStartQuiz: () => void
}

function TopicDetailContent({ topic, isStarting, startError, onClose, onStartQuiz }: TopicDetailContentProps) {
  const test = topic.latestTest

  return (
    <>
      <header className="flex flex-col items-center text-center">
        {/*
         * Decorative (public/images/mascot-thoughtful.png, 1254×1254, transparent). Smaller on narrow or short
         * viewports so the reminder items stay in view; negative margins absorb the empty transparent edges.
         */}
        <img
          src="/images/mascot-thoughtful.png"
          alt=""
          width={1254}
          height={1254}
          className="-mt-2 -mb-1 size-28 object-contain sm:size-[176px] [@media(max-height:760px)]:size-24"
        />
        <span className="badge badge-brand mt-1">{topic.category}</span>
        <h2 id="topic-detail-title" className="mt-2 text-2xl font-bold tracking-tight text-ink sm:text-3xl">
          {topic.title}
        </h2>
        <p className="mt-2 max-w-xl text-sm text-ink-secondary sm:text-base">{reviewReason(topic)}</p>
        {test && (
          <p className="mt-1 text-xs text-ink-muted sm:text-sm">
            Son test: {formatScore(test)} · {testTypeLabel(test.testType)}
          </p>
        )}
      </header>

      {topic.summaryPoints.length > 0 && (
        <section className="mt-5" aria-labelledby="quick-recall-title">
          <h3 id="quick-recall-title" className="section-title text-base">
            {topic.summaryPoints.length} Hap Bilgi
          </h3>
          <ol className="mt-3 flex flex-col gap-2">
            {topic.summaryPoints.map(({ tag, text, note }, index) => (
              <li key={tag} className="flex items-center gap-3 rounded-tile bg-brand-soft p-card-sm">
                <span className="flex size-9 shrink-0 items-center justify-center rounded-full bg-surface text-base font-bold text-brand" aria-hidden="true">
                  {index + 1}
                </span>
                <div className="min-w-0 flex-1">
                  <p className="text-sm font-bold text-brand">{tag}</p>
                  <p className="text-sm font-semibold text-ink">{text}</p>
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

      {/* Sticks to the bottom of the scrolling content, so the actions stay visible on short screens. */}
      <footer className="sticky -bottom-card -mx-card -mb-card mt-4 flex flex-col-reverse gap-2 bg-surface px-card pt-3 pb-card sm:flex-row sm:justify-end">
        <button type="button" className="btn-secondary" onClick={onClose}>
          Daha Sonra
        </button>
        <button
          type="button"
          className="btn-primary"
          onClick={onStartQuiz}
          disabled={topic.questionCount === 0 || isStarting}
        >
          {isStarting ? 'Test hazırlanıyor…' : `${topic.questionCount} Soruluk Teste Başla`}
        </button>
      </footer>
    </>
  )
}

export default TopicDetailModal
