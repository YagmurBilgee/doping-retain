import { useEffect, useState, type CSSProperties } from 'react'
import { ArrowRight, CalendarCheck, Clock, PartyPopper, PlayCircle, TrendingUp, type LucideIcon } from 'lucide-react'
import type { ReviewCompletion } from '../api/types'
import type { ReviewTier, RiskLevel } from '../types'
import { getRiskPresentation, type RiskPresentation } from './riskPresentation'

interface QuizResultViewProps {
  /** Outcome stored by the server; nothing here is recalculated */
  outcome: ReviewCompletion
  onFinish: () => void
}

interface TierCopy {
  icon: LucideIcon
  badge: string
  tile: string
  /** Built from the server's question count */
  headline: (total: number) => string
  /** Built from the server's review interval, never from a hardcoded day count */
  message: (nextReviewInDays: number) => string
}

const TIER_COPY: Record<ReviewTier, TierCopy> = {
  perfect: {
    icon: PartyPopper,
    badge: 'badge-success',
    tile: 'bg-success-soft text-success',
    headline: (total) => `Harika! ${total} sorunun tamamı doğru`,
    message: () => 'Kısa tekrarı tamamladın. Bir sonraki tekrarın planlandı.',
  },
  partial: {
    icon: TrendingUp,
    badge: 'badge-brand',
    tile: 'bg-brand-soft text-brand',
    headline: () => 'Harika İlerleme! Konu Güçlendi',
    message: (days) => `Küçük bir eksik kaldı. ${days} gün sonra kısa bir tekrarla tamamlayacağız.`,
  },
  weak: {
    icon: Clock,
    badge: 'badge-risk',
    tile: 'bg-risk-soft text-risk-ink',
    headline: () => 'İyi Başlangıç! Konuyu Takibe Aldık',
    message: (days) => `Bu konu biraz daha ilgi istiyor. ${days} gün sonra yeniden hatırlatacağız.`,
  },
}

const FIRST_REINFORCEMENT_PERFECT: TierCopy = {
  ...TIER_COPY.perfect,
  headline: () => 'Güzel! Konu Güçlendi',
  message: () => 'Tüm soruları doğru yanıtladın. Konuyu sonraki tekrarlarla pekiştirmeye devam edeceğiz.',
}

interface PersistenceLabel {
  text: string
  tone: string
  icon?: LucideIcon
}

/** Student-friendly persistence wording instead of raw stability days. */
const PERSISTENCE_BEFORE: Record<RiskLevel, PersistenceLabel> = {
  high_risk: { text: 'Kalıcılık: Düşük', tone: 'text-ink-secondary' },
  review_soon: { text: 'Kalıcılık: Orta', tone: 'text-ink-secondary' },
  safe: { text: 'Kalıcılık: Yüksek', tone: 'text-ink-secondary' },
}

const PERSISTENCE_AFTER: Record<ReviewTier, PersistenceLabel> = {
  perfect: { text: 'Kalıcılık: Güçlendirildi', tone: 'font-semibold text-success-ink', icon: TrendingUp },
  partial: { text: 'Kalıcılık: Güçlendirildi', tone: 'font-semibold text-success-ink', icon: TrendingUp },
  weak: { text: 'Kalıcılık: Takip Ediliyor', tone: 'font-semibold text-risk-ink', icon: Clock },
}

function QuizResultView({ outcome, onFinish }: QuizResultViewProps) {
  const { score, total, review, retentionAtNextReview, retentionGain, riskBefore: before, riskAfter: after } = outcome
  const afterPresentation = getRiskPresentation(after)
  // A first review of a topic that needed initial reinforcement never claims the topic is mastered.
  const copy = before.needsInitialReinforcement && review.tier === 'perfect' ? FIRST_REINFORCEMENT_PERFECT : TIER_COPY[review.tier]
  const TierIcon = copy.icon
  const isPerfect = total > 0 && score === total
  // Only a confirmed 0 score: 1/3 shares the weak tier but should not be sent back to the lesson.
  const recommendLessonVideo = score === 0

  return (
    <>
      <header className="relative flex flex-col items-center text-center">
        {isPerfect && <ConfettiBurst sessionId={outcome.sessionId} />}
        <span className={`icon-tile relative z-10 size-14 ${copy.tile} motion-safe:animate-pop-in`} aria-hidden="true">
          <TierIcon size={28} />
        </span>
        <span className={`badge ${copy.badge} mt-3`}>
          {score} / {total} doğru
        </span>
        <h2 id="mini-quiz-title" className="mt-2 text-xl font-bold tracking-tight text-ink">
          {copy.headline(total)}
        </h2>
        <p className="mt-1 text-sm text-ink-secondary">{copy.message(review.nextReviewInDays)}</p>
      </header>

      <section
        className="mt-5 grid grid-cols-[1fr_auto_1fr] items-center gap-2 rounded-card bg-surface-muted p-card-sm"
        aria-label={`Hatırlama oranı %${before.retention} değerinden %${after.retention} değerine çıktı`}
      >
        <RetentionRing
          caption="Önce"
          retention={before.retention}
          presentation={getRiskPresentation(before)}
          persistence={PERSISTENCE_BEFORE[before.riskLevel]}
        />

        <div className="flex flex-col items-center gap-1.5" aria-hidden="true">
          <ArrowRight size={18} className="text-ink-muted" />
          <span className={`badge ${afterPresentation.badge} motion-safe:animate-pop-in`}>+{retentionGain} puan</span>
        </div>

        <RetentionRing
          caption="Sonra"
          retention={after.retention}
          presentation={afterPresentation}
          persistence={PERSISTENCE_AFTER[review.tier]}
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

      {recommendLessonVideo && <LessonVideoRecommendation topicTitle={outcome.updatedTopic.title} />}

      <button
        type="button"
        className={`${recommendLessonVideo ? 'btn-secondary mt-3' : 'btn-primary mt-5'} w-full`}
        onClick={onFinish}
      >
        Ana Sayfaya Dön
      </button>
    </>
  )
}

/** Sessions whose perfect result was already celebrated in this app lifetime; nothing is persisted. */
const celebratedSessions = new Set<string>()

const CONFETTI_MS = 1500

/*
 * Fixed decorative pieces. Offsets (px) keep them beside the result icon, within its height band,
 * so they never cross the score badge, the headline or any control.
 */
const CONFETTI_PIECES = [
  { x: -110, y: -14, r: 220, color: 'bg-brand', delay: 0 },
  { x: -88, y: 12, r: -160, color: 'bg-coin', delay: 60 },
  { x: -70, y: -22, r: 140, color: 'bg-success', delay: 30 },
  { x: -56, y: 20, r: -200, color: 'bg-risk', delay: 90 },
  { x: -96, y: -2, r: 180, color: 'bg-success', delay: 120 },
  { x: -48, y: -8, r: -120, color: 'bg-brand', delay: 150 },
  { x: 110, y: -12, r: -220, color: 'bg-coin', delay: 0 },
  { x: 88, y: 14, r: 160, color: 'bg-brand', delay: 60 },
  { x: 70, y: -22, r: -140, color: 'bg-risk', delay: 30 },
  { x: 56, y: 20, r: 200, color: 'bg-success', delay: 90 },
  { x: 96, y: 2, r: -180, color: 'bg-brand', delay: 120 },
  { x: 48, y: -6, r: 120, color: 'bg-coin', delay: 150 },
]

/**
 * A short, silent burst behind the result icon, at most once per session while the app runs:
 * completion retries and later mounts of the same result skip it. Off for reduced motion.
 */
function ConfettiBurst({ sessionId }: { sessionId: string }) {
  // A pure read decides it for this mount; the effect records the session so it never replays.
  const [isVisible, setIsVisible] = useState(
    () => !celebratedSessions.has(sessionId) && !window.matchMedia('(prefers-reduced-motion: reduce)').matches,
  )

  useEffect(() => {
    if (!isVisible) return
    celebratedSessions.add(sessionId)
    const timer = window.setTimeout(() => setIsVisible(false), CONFETTI_MS)
    return () => window.clearTimeout(timer)
  }, [isVisible, sessionId])

  if (!isVisible) return null

  return (
    <div aria-hidden="true" data-confetti className="pointer-events-none absolute top-7 left-1/2 size-0 motion-reduce:hidden">
      {CONFETTI_PIECES.map(({ x, y, r, color, delay }) => (
        <span
          key={`${x}:${y}`}
          className={`absolute top-0 left-0 h-2.5 w-1.5 rounded-sm ${color} motion-safe:animate-confetti`}
          style={
            {
              '--confetti-x': `${x}px`,
              '--confetti-y': `${y}px`,
              '--confetti-r': `${r}deg`,
              animationDelay: `${delay}ms`,
            } as CSSProperties
          }
        />
      ))}
    </div>
  )
}

/** Points a student who missed every question back to the topic's lesson video. */
function LessonVideoRecommendation({ topicTitle }: { topicTitle: string }) {
  const [showPrototypeNotice, setShowPrototypeNotice] = useState(false)

  // Integration boundary: the real app opens this topic's lesson video page in Doping Hafıza here.
  // The prototype has no such route, so it only explains what would happen.
  const openLessonVideo = () => setShowPrototypeNotice(true)

  return (
    <section className="mt-5 rounded-tile border border-line p-card-sm" aria-labelledby="lesson-video-title">
      <div className="flex items-center gap-3">
        <span className="flex aspect-video w-24 shrink-0 items-center justify-center rounded-tile bg-brand text-white" aria-hidden="true">
          <PlayCircle size={28} />
        </span>
        <div className="min-w-0 flex-1">
          <p className="section-subtitle">Ders Videosu</p>
          <h3 id="lesson-video-title" className="truncate text-sm font-semibold text-ink">
            {topicTitle}
          </h3>
          <p className="text-xs text-ink-secondary">Temelleri yeniden gözden geçirmek için konu videosuna dön.</p>
        </div>
      </div>
      <button type="button" className="btn-primary mt-3 inline-flex w-full items-center justify-center gap-1.5" onClick={openLessonVideo}>
        <PlayCircle size={16} aria-hidden="true" />
        İlgili Ders Videosunu İzle
      </button>
      {/* Always rendered so screen readers announce the message when it appears. */}
      <p role="status" className="mt-2 text-xs text-ink-secondary empty:mt-0">
        {showPrototypeNotice && 'Gerçek uygulamada bu konunun ders videosu sayfası açılacak.'}
      </p>
    </section>
  )
}

interface RetentionRingProps {
  caption: string
  retention: number
  presentation: RiskPresentation
  persistence: PersistenceLabel
}

function RetentionRing({ caption, retention, presentation, persistence }: RetentionRingProps) {
  const PersistenceIcon = persistence.icon

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
      <p className={`mt-1.5 flex items-center gap-1 text-xs ${persistence.tone}`}>
        {PersistenceIcon && <PersistenceIcon size={12} aria-hidden="true" />}
        {persistence.text}
      </p>
    </div>
  )
}

export default QuizResultView
