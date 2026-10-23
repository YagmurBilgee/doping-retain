import { BrainCircuit, CalendarCheck, Clock, RotateCcw, ShieldCheck, Triangle } from 'lucide-react'
import { daysSinceStudied, type LoadState, type ReviewCompletion, type TopicsResponse, type TopicSummary } from '../api/types'
import { getRiskPresentation } from './riskPresentation'

interface MemoryRiskCardProps {
  /** Backend evaluation of the student's topics */
  topics: LoadState<TopicsResponse>
  onRetry: () => void
  /** Latest review stored by the server, shown as feedback when no topic is at high risk */
  lastReview?: ReviewCompletion
  /** Estimated micro-review duration shown on the CTA */
  reviewMinutes?: number
  onStartReview?: (topicId: string) => void
}

function MemoryRiskCard({ topics, onRetry, lastReview, reviewMinutes = 5, onStartReview }: MemoryRiskCardProps) {
  const data = topics.status === 'ready' ? topics.data : undefined
  const topic = data?.topics.find(({ id }) => id === data.highestRiskTopicId)
  const reinforcementTopics = data?.topics.filter((summary) => summary.needsInitialReinforcement) ?? []

  return (
    <section className="card" aria-labelledby="memory-risk-title" aria-busy={topics.status === 'loading'}>
      <header className="mb-4 flex items-start justify-between gap-3">
        <div>
          <h2 id="memory-risk-title" className="section-title">
            Dopingle Hatırla
          </h2>
          <p className="section-subtitle mt-0.5">
            {data ? `${data.highRiskCount} konu yüksek risk altında` : 'Hafıza durumun'}
          </p>
        </div>
        {topic || !data ? (
          <span className="icon-tile size-9 bg-danger-soft text-danger" aria-hidden="true">
            <BrainCircuit size={18} />
          </span>
        ) : (
          <span className="icon-tile size-9 bg-success-soft text-success" aria-hidden="true">
            <ShieldCheck size={18} />
          </span>
        )}
      </header>

      {topics.status === 'loading' && <p className="text-sm text-ink-secondary">Konuların yükleniyor…</p>}

      {topics.status === 'error' && (
        <div role="alert">
          <p className="text-sm text-ink-secondary">Konular yüklenemedi. {topics.message}</p>
          <button type="button" className="btn-secondary mt-3 inline-flex items-center gap-1.5" onClick={onRetry}>
            <RotateCcw size={14} aria-hidden="true" />
            Tekrar Dene
          </button>
        </div>
      )}

      {data && (
        // Keyed by state so switching between at-risk and all-clear animates in.
        <div key={topic?.id ?? 'all-clear'} className="motion-safe:animate-pop-in">
          {topic ? (
            <AtRiskTopic
              topic={topic}
              daysAgo={daysSinceStudied(topic, data.evaluatedAt)}
              reviewMinutes={reviewMinutes}
              onStartReview={onStartReview}
            />
          ) : (
            <AllClear lastReview={lastReview} hasReinforcementTopics={reinforcementTopics.length > 0} />
          )}
        </div>
      )}

      {reinforcementTopics.length > 0 && <ReinforcementTopics topics={reinforcementTopics} onOpenTopic={onStartReview} />}

      {/* Only from a loaded response, so loading or errors never show a made-up zero. */}
      {data && <WeeklyReviews count={data.weeklyReviewedTopicCount} />}
    </section>
  )
}

/** Secondary footer with the server's weekly count of distinct reviewed topics. */
function WeeklyReviews({ count }: { count: number }) {
  return (
    <p className="mt-4 flex items-center gap-1.5 border-t border-line pt-3 text-xs text-ink-muted">
      <CalendarCheck size={14} className="shrink-0" aria-hidden="true" />
      {count > 0 ? `Bu hafta ${count} farklı konuyu tekrar ettin.` : 'Bu haftaki ilk tekrarını yapmaya hazır mısın?'}
    </p>
  )
}

interface ReinforcementTopicsProps {
  topics: TopicSummary[]
  onOpenTopic?: (topicId: string) => void
}

/** Low initial mastery is not forgetting: listed below the forgetting topic, never counted as high risk. */
function ReinforcementTopics({ topics, onOpenTopic }: ReinforcementTopicsProps) {
  return (
    <section className="mt-5 border-t border-line pt-4" aria-labelledby="reinforcement-topics-title">
      <h3 id="reinforcement-topics-title" className="text-sm font-semibold text-ink">
        Pekiştirme Bekleyen Konular
      </h3>
      <ul className="mt-2 flex flex-col gap-2">
        {topics.map((reinforcementTopic) => {
          const styles = getRiskPresentation(reinforcementTopic)
          return (
            <li key={reinforcementTopic.id}>
              <button type="button" className="list-row w-full text-left" onClick={() => onOpenTopic?.(reinforcementTopic.id)}>
                <span className="min-w-0 flex-1">
                  <span className="section-subtitle block">{reinforcementTopic.category}</span>
                  <span className="block truncate text-sm font-semibold text-ink">{reinforcementTopic.title}</span>
                </span>
                <span className={`badge ${styles.badge}`}>{styles.label}</span>
              </button>
            </li>
          )
        })}
      </ul>
    </section>
  )
}

interface AtRiskTopicProps {
  topic: TopicSummary
  daysAgo: number
  reviewMinutes: number
  onStartReview?: (topicId: string) => void
}

function AtRiskTopic({ topic, daysAgo, reviewMinutes, onStartReview }: AtRiskTopicProps) {
  const styles = getRiskPresentation(topic)

  return (
    <>
      <div className="list-row">
        <span className="icon-tile bg-brand text-white" aria-hidden="true">
          <Triangle size={18} />
        </span>
        <div className="min-w-0 flex-1">
          <p className="section-subtitle">{topic.category}</p>
          <p className="truncate text-sm font-semibold text-ink">{topic.title}</p>
          <p className="text-xs text-ink-muted">{daysAgo} gün önce çalışıldı</p>
        </div>
      </div>

      <RetentionMeter retention={topic.retention} barClass={styles.bar} badgeClass={styles.badge} />

      <button type="button" className="btn-primary mt-4 w-full" onClick={() => onStartReview?.(topic.id)}>
        Hemen Tekrar Et ({reviewMinutes} dk)
      </button>
    </>
  )
}

interface AllClearProps {
  lastReview?: ReviewCompletion
  /** Topics needing initial reinforcement are not forgetting, so they never make this state "all good" */
  hasReinforcementTopics: boolean
}

/** A high-risk topic always takes precedence; this feedback only fills the all-clear state. */
function AllClear({ lastReview, hasReinforcementTopics }: AllClearProps) {
  if (!lastReview) {
    return (
      <p className="text-sm text-ink-secondary">
        {hasReinforcementTopics
          ? 'Şu an unutmak üzere olduğun bir konu yok. Pekiştirme bekleyen konuların var.'
          : 'Şu an unutmak üzere olduğun bir konu yok. Harika gidiyorsun!'}
      </p>
    )
  }

  const { updatedTopic, review, riskAfter } = lastReview
  const styles = getRiskPresentation(riskAfter)

  return (
    <div role="status">
      <div className="list-row">
        <span className={`icon-tile text-white ${riskAfter.isFollowUp ? 'bg-risk' : 'bg-success'}`} aria-hidden="true">
          {riskAfter.isFollowUp ? <Clock size={18} /> : <ShieldCheck size={18} />}
        </span>
        <div className="min-w-0 flex-1">
          <p className="section-subtitle">{updatedTopic.category}</p>
          <p className="truncate text-sm font-semibold text-ink">{updatedTopic.title} tekrar edildi</p>
          <p className="text-xs text-ink-muted">Sonraki tekrar {review.nextReviewInDays} gün sonra</p>
        </div>
      </div>

      <RetentionMeter
        retention={riskAfter.retention}
        barClass={styles.bar}
        badgeClass={styles.badge}
        label={styles.label}
      />
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
