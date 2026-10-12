import { ArrowRight, CalendarCheck, PartyPopper, Sparkles, TrendingUp } from 'lucide-react'
import type { ReviewOutcome, ReviewTier, Topic } from '../types'
import { getTopicRisk } from '../utils/memoryModel'
import { RISK_PRESENTATION } from './riskPresentation'

interface QuizResultViewProps {
  /** Topic as it was before the review */
  topic: Topic
  score: number
  total: number
  outcome: ReviewOutcome
  onFinish: () => void
}

const TIER_COPY: Record<ReviewTier, { badge: string; tile: string; headline: string; message: string }> = {
  perfect: {
    badge: 'badge-success',
    tile: 'bg-success-soft text-success',
    headline: 'Tebrikler! Konuyu Hafızana Sabitledin',
    message: 'Tüm soruları doğru yanıtladın. Bellek gücün iki katına çıktı.',
  },
  partial: {
    badge: 'badge-brand',
    tile: 'bg-brand-soft text-brand',
    headline: 'Harika İlerleme! Konu Güçlendi',
    message: 'Küçük bir eksik kaldı. Bir hafta sonra kısa bir tekrarla tamamlayacağız.',
  },
  weak: {
    badge: 'badge-risk',
    tile: 'bg-risk-soft text-risk-ink',
    headline: 'İyi Başlangıç! Yakında Tekrar Edelim',
    message: 'Bu konu biraz daha ilgi istiyor. İki gün sonra yeniden hatırlatacağız.',
  },
}

function QuizResultView({ topic, score, total, outcome, onFinish }: QuizResultViewProps) {
  const before = getTopicRisk(topic)
  const after = getTopicRisk(outcome.updatedTopic)
  const beforeRisk = RISK_PRESENTATION[before.riskLevel]
  const afterRisk = RISK_PRESENTATION[after.riskLevel]
  const { review, retentionAtNextReview } = outcome
  const copy = TIER_COPY[review.tier]

  return (
    <>
      <header className="flex flex-col items-center text-center">
        <span className={`icon-tile size-14 ${copy.tile}`} aria-hidden="true">
          <PartyPopper size={28} />
        </span>
        <span className={`badge ${copy.badge} mt-3`}>
          {score} / {total} doğru
        </span>
        <h2 id="mini-quiz-title" className="mt-2 text-xl font-bold tracking-tight text-ink">
          {copy.headline}
        </h2>
        <p className="mt-1 text-sm text-ink-secondary">{copy.message}</p>
      </header>

      <section className="mt-5 grid grid-cols-[1fr_auto_1fr] items-stretch gap-2" aria-label="Hafıza durumu karşılaştırması">
        <div className="rounded-tile bg-surface-muted p-card-sm">
          <p className="section-subtitle">Eski Durum</p>
          <p className="mt-1 text-2xl font-extrabold text-ink">%{before.retention}</p>
          <p className="text-xs text-ink-muted">Hatırlama Oranı</p>
          <span className={`badge ${beforeRisk.badge} mt-2`}>{beforeRisk.label}</span>
          <p className="mt-2 text-xs text-ink-secondary">Bellek gücü: {topic.currentStrength.toLocaleString('tr-TR')} gün</p>
        </div>

        <span className="flex items-center text-ink-muted" aria-hidden="true">
          <ArrowRight size={18} />
        </span>

        <div className="rounded-tile border border-success bg-success-soft p-card-sm">
          <p className="section-subtitle flex items-center gap-1 text-success-ink">
            <Sparkles size={12} aria-hidden="true" />
            Yeni Durum
          </p>
          <p className="mt-1 text-2xl font-extrabold text-ink">%{after.retention}</p>
          <p className="text-xs text-ink-muted">Hatırlama Oranı</p>
          <span className={`badge ${afterRisk.badge} mt-2`}>{afterRisk.label}</span>
          <p className="mt-2 flex items-center gap-1 text-xs font-semibold text-success-ink">
            <TrendingUp size={12} aria-hidden="true" />
            Bellek gücü: {review.newStrength.toLocaleString('tr-TR')} gün
          </p>
        </div>
      </section>

      <div className="list-row mt-3">
        <span className="icon-tile bg-brand text-white" aria-hidden="true">
          <CalendarCheck size={18} />
        </span>
        <div className="min-w-0 flex-1">
          <p className="section-subtitle">Yeni Tekrar Planı</p>
          <p className="text-sm font-semibold text-ink">
            Sonraki tekrar {review.nextReviewInDays} gün sonra planlandı
          </p>
          <p className="text-xs text-ink-muted">
            O gün tahmini hatırlama oranın %{retentionAtNextReview} olacak.
          </p>
        </div>
      </div>

      <button type="button" className="btn-primary mt-5 w-full" onClick={onFinish}>
        Ana Sayfaya Dön
      </button>
    </>
  )
}

export default QuizResultView
