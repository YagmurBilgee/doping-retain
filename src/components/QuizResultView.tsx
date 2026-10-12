import { ArrowRight, CalendarCheck, Clock, PartyPopper, TrendingUp, type LucideIcon } from 'lucide-react'
import type { ReviewOutcome, ReviewTier, Topic } from '../types'
import { getTopicRisk } from '../utils/memoryModel'
import { getRiskPresentation, type RiskPresentation } from './riskPresentation'

interface QuizResultViewProps {
  /** Topic as it was before the review */
  topic: Topic
  score: number
  total: number
  outcome: ReviewOutcome
  onFinish: () => void
}

const TIER_COPY: Record<ReviewTier, { icon: LucideIcon; badge: string; tile: string; headline: string; message: string }> = {
  perfect: {
    icon: PartyPopper,
    badge: 'badge-success',
    tile: 'bg-success-soft text-success',
    headline: 'Tebrikler! Konuyu Hafızana Sabitledin',
    message: 'Tüm soruları doğru yanıtladın. Bellek gücün iki katına çıktı.',
  },
  partial: {
    icon: TrendingUp,
    badge: 'badge-brand',
    tile: 'bg-brand-soft text-brand',
    headline: 'Harika İlerleme! Konu Güçlendi',
    message: 'Küçük bir eksik kaldı. Bir hafta sonra kısa bir tekrarla tamamlayacağız.',
  },
  weak: {
    icon: Clock,
    badge: 'badge-risk',
    tile: 'bg-risk-soft text-risk-ink',
    headline: 'İyi Başlangıç! Konuyu Takibe Aldık',
    message: 'Bu konu biraz daha ilgi istiyor. İki gün sonra yeniden hatırlatacağız.',
  },
}

function QuizResultView({ topic, score, total, outcome, onFinish }: QuizResultViewProps) {
  const before = getTopicRisk(topic)
  const after = getTopicRisk(outcome.updatedTopic)
  const afterPresentation = getRiskPresentation(after)
  const { review, retentionAtNextReview, retentionGain } = outcome
  const copy = TIER_COPY[review.tier]
  const TierIcon = copy.icon

  return (
    <>
      <header className="flex flex-col items-center text-center">
        <span className={`icon-tile size-14 ${copy.tile} motion-safe:animate-pop-in`} aria-hidden="true">
          <TierIcon size={28} />
        </span>
        <span className={`badge ${copy.badge} mt-3`}>
          {score} / {total} doğru
        </span>
        <h2 id="mini-quiz-title" className="mt-2 text-xl font-bold tracking-tight text-ink">
          {copy.headline}
        </h2>
        <p className="mt-1 text-sm text-ink-secondary">{copy.message}</p>
      </header>

      <section
        className="mt-5 grid grid-cols-[1fr_auto_1fr] items-center gap-2 rounded-card bg-surface-muted p-card-sm"
        aria-label={`Hatırlama oranı %${before.retention} değerinden %${after.retention} değerine çıktı`}
      >
        <RetentionRing
          caption="Önce"
          retention={before.retention}
          presentation={getRiskPresentation(before)}
          strengthDays={topic.currentStrength}
        />

        <div className="flex flex-col items-center gap-1.5" aria-hidden="true">
          <ArrowRight size={18} className="text-ink-muted" />
          <span className={`badge ${afterPresentation.badge} motion-safe:animate-pop-in`}>+{retentionGain} puan</span>
        </div>

        <RetentionRing
          caption="Sonra"
          retention={after.retention}
          presentation={afterPresentation}
          strengthDays={review.newStrength}
          isStrengthened
        />
      </section>

      <div className="mt-3 flex items-center gap-3 rounded-tile border border-brand/20 bg-brand-soft p-card-sm">
        <span className="icon-tile bg-brand text-white" aria-hidden="true">
          <CalendarCheck size={18} />
        </span>
        <div className="min-w-0 flex-1">
          <p className="text-base font-bold text-brand">Sonraki Tekrar: {review.nextReviewInDays} gün sonra</p>
          <p className="text-xs text-ink-secondary">O gün tahmini hatırlama oranın %{retentionAtNextReview} olacak.</p>
        </div>
      </div>

      <button type="button" className="btn-primary mt-5 w-full" onClick={onFinish}>
        Ana Sayfaya Dön
      </button>
    </>
  )
}

interface RetentionRingProps {
  caption: string
  retention: number
  presentation: RiskPresentation
  strengthDays: number
  isStrengthened?: boolean
}

function RetentionRing({ caption, retention, presentation, strengthDays, isStrengthened = false }: RetentionRingProps) {
  return (
    <div className="flex flex-col items-center text-center">
      <p className="section-subtitle">{caption}</p>
      <div className="relative mt-2 size-24">
        <svg viewBox="0 0 100 100" className="size-full -rotate-90" aria-hidden="true">
          <circle cx="50" cy="50" r="42" fill="none" strokeWidth="9" className="stroke-line" />
          <circle
            cx="50"
            cy="50"
            r="42"
            fill="none"
            strokeWidth="9"
            strokeLinecap="round"
            stroke="currentColor"
            pathLength={100}
            strokeDasharray="100"
            strokeDashoffset={100 - retention}
            className={`${presentation.text} motion-safe:animate-ring-fill`}
          />
        </svg>
        <span className="absolute inset-0 flex items-center justify-center text-2xl font-extrabold text-ink">
          %{retention}
        </span>
      </div>
      <span className={`badge ${presentation.badge} mt-2`}>{presentation.label}</span>
      <p
        className={`mt-1.5 flex items-center gap-1 text-xs ${
          isStrengthened ? 'font-semibold text-success-ink' : 'text-ink-secondary'
        }`}
      >
        {isStrengthened && <TrendingUp size={12} aria-hidden="true" />}
        Bellek gücü: {strengthDays.toLocaleString('tr-TR')} gün
      </p>
    </div>
  )
}

export default QuizResultView
