import { useEffect, useState, type CSSProperties } from 'react'
import { CalendarCheck, PlayCircle } from 'lucide-react'
import type { ReviewCompletion } from '../api/types'
import { formatScore, getStatusPresentation, scheduleText, testTypeLabel } from './reviewPresentation'

interface QuizResultViewProps {
  /** Outcome stored by the server; nothing here is recalculated */
  outcome: ReviewCompletion
  onFinish: () => void
}

interface ScoreCopy {
  badge: string
  message: (total: number) => string
}

/** Simple score-based feedback; it describes this result only and promises nothing about memory. */
const SCORE_COPY: Record<'full' | 'partial' | 'zero', ScoreCopy> = {
  full: {
    badge: 'badge-success',
    message: (total) => `Harika! ${total} sorunun tamamını doğru yanıtladın.`,
  },
  partial: {
    badge: 'badge-brand',
    message: () => 'Kaçırdığın noktaları hızlı hatırlatmadan bir kez daha gözden geçirerek pekiştirebilirsin.',
  },
  zero: {
    badge: 'badge-risk',
    message: () => 'Bu konuyu temelden yeniden gözden geçirmek iyi olabilir.',
  },
}

function QuizResultView({ outcome, onFinish }: QuizResultViewProps) {
  const { score, total, testResult } = outcome
  const isPerfect = total > 0 && score === total
  // Only a confirmed 0 score is sent back to the lesson video.
  const recommendLessonVideo = score === 0
  const copy = SCORE_COPY[isPerfect ? 'full' : recommendLessonVideo ? 'zero' : 'partial']
  const status = getStatusPresentation(outcome.reviewStatus)
  const schedule = scheduleText(outcome.reviewStatus, outcome.nextReviewAt, outcome.completedAt)

  return (
    <>
      <header className="relative flex flex-col items-center text-center">
        {isPerfect && <ConfettiBurst sessionId={outcome.sessionId} />}
        {/*
         * Decorative (public/images/mascot-happy.png, 1254×1254, transparent): shown for every completed
         * quiz to mark the effort, not mastery. Negative margins absorb the empty transparent edges.
         */}
        <img
          src="/images/mascot-happy.png"
          alt=""
          width={1254}
          height={1254}
          className="relative z-10 -mt-2 -mb-1 size-[88px] object-contain sm:size-[120px]"
        />
        <span className={`badge ${copy.badge} mt-2`}>
          {score} / {total} doğru
        </span>
        <h2 id="mini-quiz-title" className="mt-2 text-xl font-bold tracking-tight text-ink">
          Tekrar tamamlandı
        </h2>
        <p className="mt-1 text-sm text-ink-secondary">{copy.message(total)}</p>
      </header>

      <div className="mt-5 grid gap-2 sm:grid-cols-2">
        <div className="rounded-tile bg-surface-muted p-card-sm">
          <p className="section-subtitle">Test sonucun</p>
          <p className="mt-1 text-base font-bold text-ink">{formatScore(testResult)}</p>
          <p className="text-xs text-ink-secondary">{testTypeLabel(testResult.testType)}</p>
        </div>
        <div className="flex gap-3 rounded-tile border border-brand/20 bg-brand-soft p-card-sm">
          <span className="icon-tile size-9 bg-brand text-white" aria-hidden="true">
            <CalendarCheck size={18} />
          </span>
          <div className="min-w-0 flex-1">
            <p className="section-subtitle">Öneri</p>
            <span className={`badge ${status.badge} mt-1`}>{status.label}</span>
            {schedule && <p className="mt-1 text-sm font-semibold text-brand">{schedule}</p>}
          </div>
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
 * Fixed decorative pieces. Offsets (px) keep them beside the mascot, within its height band,
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
 * A short, silent burst behind the mascot, at most once per session while the app runs:
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
    <div aria-hidden="true" data-confetti className="pointer-events-none absolute top-11 left-1/2 size-0 motion-reduce:hidden sm:top-13">
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

export default QuizResultView
