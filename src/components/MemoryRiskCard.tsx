import { BrainCircuit, Triangle } from 'lucide-react'
import type { RiskLevel, Topic } from '../types'
import { getTopicRisk } from '../utils/memoryModel'

interface MemoryRiskCardProps {
  topic: Topic
  /** Number of topics currently in the high_risk band */
  highRiskCount: number
  /** Estimated micro-review duration shown on the CTA */
  reviewMinutes?: number
  onStartReview?: () => void
}

const RISK_STYLES: Record<RiskLevel, { badge: string; bar: string }> = {
  high_risk: { badge: 'badge-danger', bar: 'bg-danger' },
  review_soon: { badge: 'badge-risk', bar: 'bg-risk' },
  safe: { badge: 'badge-success', bar: 'bg-success' },
}

function MemoryRiskCard({
  topic,
  highRiskCount,
  reviewMinutes = 5,
  onStartReview,
}: MemoryRiskCardProps) {
  const { retention, riskLevel } = getTopicRisk(topic)
  const styles = RISK_STYLES[riskLevel]

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
        <span className="icon-tile size-9 bg-danger-soft text-danger" aria-hidden="true">
          <BrainCircuit size={18} />
        </span>
      </header>

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

      <div className="mt-4 flex items-center gap-3">
        <div
          className="h-1.5 flex-1 overflow-hidden rounded-full bg-surface-muted"
          role="progressbar"
          aria-valuenow={retention}
          aria-valuemin={0}
          aria-valuemax={100}
          aria-label="Hatırlama oranı"
        >
          <div className={`h-full rounded-full ${styles.bar}`} style={{ width: `${retention}%` }} />
        </div>
        <span className={`badge ${styles.badge}`}>%{retention} Hatırlama Oranı</span>
      </div>

      <button type="button" className="btn-primary mt-4 w-full" onClick={onStartReview}>
        Hemen Tekrar Et ({reviewMinutes} dk)
      </button>
    </section>
  )
}

export default MemoryRiskCard
