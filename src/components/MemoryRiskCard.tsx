import { BrainCircuit, Clock, ShieldCheck, Triangle } from 'lucide-react'
import type { ReviewOutcome, Topic } from '../types'
import { getTopicRisk } from '../utils/memoryModel'
import { getRiskPresentation } from './riskPresentation'

interface MemoryRiskCardProps {
  /** Most at-risk topic; when absent the card shows its all-clear state */
  topic: Topic | undefined
  /** Number of topics currently in the high_risk band */
  highRiskCount: number
  /** Latest finished review, surfaced as feedback in the all-clear state */
  lastReview?: ReviewOutcome
  /** Estimated micro-review duration shown on the CTA */
  reviewMinutes?: number
  onStartReview?: (topicId: string) => void
}

function MemoryRiskCard({
  topic,
  highRiskCount,
  lastReview,
  reviewMinutes = 5,
  onStartReview,
}: MemoryRiskCardProps) {
  return (
    <section className="card" aria-labelledby="memory-risk-title">
      <header className="mb-4 flex items-start justify-between gap-3">
        <div>
          <h2 id="memory-risk-title" className="section-title">
            Unutmak Üzeresin
          </h2>
          <p className="section-subtitle mt-0.5">
            {highRiskCount} konu yüksek risk altında
          </p>
        </div>
        {topic ? (
          <span className="icon-tile size-9 bg-danger-soft text-danger" aria-hidden="true">
            <BrainCircuit size={18} />
          </span>
        ) : (
          <span className="icon-tile size-9 bg-success-soft text-success" aria-hidden="true">
            <ShieldCheck size={18} />
          </span>
        )}
      </header>

      {/* Keyed by state so switching between at-risk and all-clear animates in. */}
      <div key={topic?.id ?? 'all-clear'} className="motion-safe:animate-pop-in">
        {topic ? (
          <AtRiskTopic topic={topic} reviewMinutes={reviewMinutes} onStartReview={onStartReview} />
        ) : (
          <AllClear lastReview={lastReview} />
        )}
      </div>
    </section>
  )
}

interface AtRiskTopicProps {
  topic: Topic
  reviewMinutes: number
  onStartReview?: (topicId: string) => void
}

function AtRiskTopic({ topic, reviewMinutes, onStartReview }: AtRiskTopicProps) {
  const risk = getTopicRisk(topic)
  const { retention } = risk
  const styles = getRiskPresentation(risk)

  return (
    <>
      <div className="list-row">
        <span className="icon-tile bg-brand text-white" aria-hidden="true">
          <Triangle size={18} />
        </span>
        <div className="min-w-0 flex-1">
          <p className="section-subtitle">{topic.category}</p>
          <p className="truncate text-sm font-semibold text-ink">{topic.title}</p>
          <p className="text-xs text-ink-muted">
            {topic.lastStudiedDaysAgo} gün önce çalışıldı
          </p>
        </div>
      </div>

      <RetentionMeter retention={retention} barClass={styles.bar} badgeClass={styles.badge} />

      <button type="button" className="btn-primary mt-4 w-full" onClick={() => onStartReview?.(topic.id)}>
        Hemen Tekrar Et ({reviewMinutes} dk)
      </button>
    </>
  )
}

function AllClear({ lastReview }: { lastReview?: ReviewOutcome }) {
  if (!lastReview) {
    return <p className="text-sm text-ink-secondary">Şu an unutmak üzere olduğun bir konu yok. Harika gidiyorsun!</p>
  }

  const { updatedTopic, review } = lastReview
  const risk = getTopicRisk(updatedTopic)
  const { retention, isFollowUp } = risk
  const styles = getRiskPresentation(risk)

  return (
    <div role="status">
      <div className="list-row">
        <span className={`icon-tile text-white ${isFollowUp ? 'bg-risk' : 'bg-success'}`} aria-hidden="true">
          {isFollowUp ? <Clock size={18} /> : <ShieldCheck size={18} />}
        </span>
        <div className="min-w-0 flex-1">
          <p className="section-subtitle">{updatedTopic.category}</p>
          <p className="truncate text-sm font-semibold text-ink">{updatedTopic.title} tekrar edildi</p>
          <p className="text-xs text-ink-muted">Sonraki tekrar {review.nextReviewInDays} gün sonra</p>
        </div>
      </div>

      <RetentionMeter retention={retention} barClass={styles.bar} badgeClass={styles.badge} label={styles.label} />
    </div>
  )
}

interface RetentionMeterProps {
  retention: number
  barClass: string
  badgeClass: string
  label?: string
}

function RetentionMeter({ retention, barClass, badgeClass, label }: RetentionMeterProps) {
  return (
    <div className="mt-4 flex items-center gap-3">
      <div
        className="h-1.5 flex-1 overflow-hidden rounded-full bg-surface-muted"
        role="progressbar"
        aria-valuenow={retention}
        aria-valuemin={0}
        aria-valuemax={100}
        aria-label="Hatırlama oranı"
      >
        <div
          className={`h-full rounded-full ${barClass} motion-safe:animate-bar-grow`}
          style={{ width: `${retention}%` }}
        />
      </div>
      <span className={`badge ${badgeClass}`}>
        %{retention} Hatırlama{label ? ` · ${label}` : ' Oranı'}
      </span>
    </div>
  )
}

export default MemoryRiskCard
