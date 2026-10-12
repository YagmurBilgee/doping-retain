import { Lightbulb } from 'lucide-react'
import type { Topic } from '../types'
import { getTopicRisk } from '../utils/memoryModel'
import ModalShell from './ModalShell'
import { getRiskPresentation } from './riskPresentation'

interface TopicDetailModalProps {
  isOpen: boolean
  topic: Topic | undefined
  onClose: () => void
  onStartQuiz: (topicId: string) => void
  /** Estimated micro-review duration shown in the insight copy */
  reviewMinutes?: number
}

function TopicDetailModal({
  isOpen,
  topic,
  onClose,
  onStartQuiz,
  reviewMinutes = 5,
}: TopicDetailModalProps) {
  return (
    <ModalShell isOpen={isOpen && topic !== undefined} onClose={onClose} labelledBy="topic-detail-title">
      {topic && (
        <TopicDetailContent
          topic={topic}
          questionCount={topic.quiz?.length ?? 0}
          reviewMinutes={reviewMinutes}
          onClose={onClose}
          onStartQuiz={() => onStartQuiz(topic.id)}
        />
      )}
    </ModalShell>
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
  const topicRisk = getTopicRisk(topic)
  const { retention } = topicRisk
  const risk = getRiskPresentation(topicRisk)

  return (
    <>
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
    </>
  )
}

export default TopicDetailModal
