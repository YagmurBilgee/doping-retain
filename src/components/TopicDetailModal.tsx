import { useEffect, useRef, type MouseEvent, type SyntheticEvent } from 'react'
import { Lightbulb, X } from 'lucide-react'
import type { Topic } from '../types'
import { getTopicRisk } from '../utils/memoryModel'
import { RISK_PRESENTATION } from './riskPresentation'

interface TopicDetailModalProps {
  isOpen: boolean
  topic: Topic | undefined
  onClose: () => void
  onStartQuiz: (topicId: string) => void
  /** Estimated micro-review duration shown in the insight copy */
  reviewMinutes?: number
}

/**
 * Built on the native <dialog> element: showModal() provides focus trapping,
 * Escape handling and top-layer rendering without a portal.
 */
function TopicDetailModal({
  isOpen,
  topic,
  onClose,
  onStartQuiz,
  reviewMinutes = 5,
}: TopicDetailModalProps) {
  const dialogRef = useRef<HTMLDialogElement>(null)
  const shouldShow = isOpen && topic !== undefined

  useEffect(() => {
    const dialog = dialogRef.current
    if (!dialog) return

    if (shouldShow && !dialog.open) dialog.showModal()
    if (!shouldShow && dialog.open) dialog.close()
  }, [shouldShow])

  useEffect(() => {
    if (!shouldShow) return

    const previousOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    return () => {
      document.body.style.overflow = previousOverflow
    }
  }, [shouldShow])

  const handleCancel = (event: SyntheticEvent<HTMLDialogElement>) => {
    // Keep open state owned by the parent instead of letting Escape close it natively.
    event.preventDefault()
    onClose()
  }

  const handleBackdropClick = (event: MouseEvent<HTMLDialogElement>) => {
    if (event.target === event.currentTarget) onClose()
  }

  const questionCount = topic?.quiz?.length ?? 0

  return (
    <dialog
      ref={dialogRef}
      aria-labelledby="topic-detail-title"
      onCancel={handleCancel}
      onClick={handleBackdropClick}
      className="m-auto w-[calc(100%-2rem)] max-w-lg bg-transparent p-0 backdrop:bg-ink/40 backdrop:backdrop-blur-xs"
    >
      {topic && (
        <TopicDetailContent
          topic={topic}
          questionCount={questionCount}
          reviewMinutes={reviewMinutes}
          onClose={onClose}
          onStartQuiz={() => onStartQuiz(topic.id)}
        />
      )}
    </dialog>
  )
}

interface TopicDetailContentProps {
  topic: Topic
  questionCount: number
  reviewMinutes: number
  onClose: () => void
  onStartQuiz: () => void
}

function TopicDetailContent({
  topic,
  questionCount,
  reviewMinutes,
  onClose,
  onStartQuiz,
}: TopicDetailContentProps) {
  const { retention, riskLevel } = getTopicRisk(topic)
  const risk = RISK_PRESENTATION[riskLevel]

  return (
    <div className="card relative p-card shadow-float">
      <button
        type="button"
        aria-label="Kapat"
        onClick={onClose}
        className="absolute top-4 right-4 flex size-8 items-center justify-center rounded-full text-ink-secondary transition-colors hover:bg-surface-muted hover:text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand"
      >
        <X size={18} />
      </button>

      <header className="pr-10">
        <span className="badge badge-brand">{topic.category}</span>
        <h2 id="topic-detail-title" className="mt-2 text-2xl font-bold tracking-tight text-ink">
          {topic.title}
        </h2>
        <span className={`badge ${risk.badge} mt-2`}>
          %{retention} Hatırlama · {risk.label}
        </span>
      </header>

      <section className="mt-5 flex gap-3 rounded-tile bg-brand-soft p-card-sm" aria-labelledby="why-now-title">
        <span className="icon-tile size-9 bg-surface text-brand" aria-hidden="true">
          <Lightbulb size={18} />
        </span>
        <div>
          <h3 id="why-now-title" className="text-sm font-semibold text-ink">
            Neden Şimdi Tekrar?
          </h3>
          <p className="mt-1 text-sm text-ink-secondary">
            Bu konuyu en son {topic.lastStudiedDaysAgo} gün önce çalıştın. Hafıza gücünü korumak ve
            unutmayı engellemek için {reviewMinutes} dakikalık hızlı bir pekiştirme öneriyoruz.
          </p>
        </div>
      </section>

      {topic.summaryPoints && topic.summaryPoints.length > 0 && (
        <section className="mt-5" aria-labelledby="quick-recall-title">
          <h3 id="quick-recall-title" className="section-title text-base">
            Hızlı Hatırlatma
          </h3>
          <ol className="mt-3 flex flex-col gap-2">
            {topic.summaryPoints.map((point, index) => (
              <li key={point} className="list-row">
                <span className="icon-tile size-8 bg-brand text-sm font-bold text-white" aria-hidden="true">
                  {index + 1}
                </span>
                <p className="text-sm font-semibold text-ink">{point}</p>
              </li>
            ))}
          </ol>
        </section>
      )}

      <footer className="mt-6 flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
        <button type="button" className="btn-secondary" onClick={onClose}>
          Daha Sonra
        </button>
        <button type="button" className="btn-primary" onClick={onStartQuiz} disabled={questionCount === 0}>
          {questionCount} Soruluk Pekiştirme Testine Başla
        </button>
      </footer>
    </div>
  )
}

export default TopicDetailModal
