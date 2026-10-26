import { useEffect, useRef, useState } from 'react'
import { PlayCircle } from 'lucide-react'
import type { TopicDetail } from '../api/types'
import LatestTestSummary from './LatestTestSummary'
import { LESSON_VIDEO_NOTICE } from './reviewPresentation'

interface ReinforcementStepsProps {
  topic: TopicDetail
  isStarting: boolean
  startError?: string
  onStartQuiz: () => void
}

/*
 * Reinforcement of a support-needed topic, before the same three-question mini quiz:
 *   optional lesson video --İlerle--> detailed explanation 1 → 2 → 3 --start--> mini quiz
 * The popup keeps one size on every step (760 × 600px, at most the viewport): long text scrolls in the
 * body only, and the navigation footer stays in view.
 */
function ReinforcementSteps({ topic, isStarting, startError, onStartQuiz }: ReinforcementStepsProps) {
  // -1 is the video step; 0… are the explanations of the topic's recall points.
  const [step, setStep] = useState(-1)
  const headingRef = useRef<HTMLHeadingElement>(null)
  const points = topic.summaryPoints
  const isLast = step === points.length - 1

  // Move focus with the flow so keyboard and screen-reader users follow along.
  useEffect(() => {
    if (step >= 0) headingRef.current?.focus()
  }, [step])

  return (
    // 600px popup = this height + p-card padding (2 × 1.25rem) + 2px border.
    <div className="flex h-[558px] max-h-[calc(100dvh-4.5rem-2px)] flex-col">
      <header className="shrink-0 pr-10">
        <div className="flex items-center gap-2">
          <span className="badge badge-brand">{topic.title}</span>
          <span className="section-subtitle">{step < 0 ? 'Pekiştirme' : `Açıklama ${step + 1} / ${points.length}`}</span>
        </div>
        <ol className="mt-3 flex gap-1.5" aria-hidden="true">
          {points.map((point, index) => (
            <li key={point.tag} className={`h-1.5 flex-1 rounded-full ${index <= step ? 'bg-brand' : 'bg-surface-muted'}`} />
          ))}
        </ol>
      </header>

      {/* Keyed per step so each step fades in; focusable so keyboard users can scroll long text. */}
      <div
        key={step}
        className="mt-4 min-h-0 flex-1 overflow-y-auto overscroll-contain motion-safe:animate-fade-in focus-visible:outline-2 focus-visible:outline-brand"
        tabIndex={0}
        role="region"
        aria-labelledby="topic-detail-title"
      >
        {step < 0 ? (
          <VideoStep topic={topic} />
        ) : (
          <>
            <h2 id="topic-detail-title" ref={headingRef} tabIndex={-1} className="text-lg font-bold tracking-tight text-ink outline-none">
              {points[step].tag}
            </h2>
            {/* The same recall point as in the short review, then the lesson and a worked example. */}
            <div className="mt-3 rounded-tile bg-brand-soft p-card-sm">
              <p className="text-sm font-semibold text-ink">{points[step].text}</p>
              {points[step].note && <p className="text-xs text-ink-secondary">{points[step].note}</p>}
            </div>
            {points[step].detail && (
              <section className="mt-4" aria-label="Ders anlatımı">
                <p className="section-subtitle">Ders anlatımı</p>
                <p className="mt-1 text-sm leading-relaxed text-ink">{points[step].detail}</p>
              </section>
            )}
            {points[step].example && (
              <section className="mt-4 rounded-tile border border-line bg-surface-muted p-card-sm" aria-label="Çözümlü örnek">
                <p className="section-subtitle">Çözümlü örnek</p>
                <p className="mt-1 text-sm leading-relaxed text-ink">{points[step].example}</p>
              </section>
            )}
          </>
        )}
      </div>

      {startError && (
        <p className="mt-3 shrink-0 text-sm text-danger-ink" role="alert">
          Test başlatılamadı. {startError}
        </p>
      )}

      <footer className="mt-4 flex shrink-0 flex-col-reverse gap-2 border-t border-line pt-4 sm:flex-row sm:justify-end">
        {step >= 0 && (
          <button type="button" className="btn-secondary" onClick={() => setStep(step - 1)} disabled={isStarting}>
            Geri
          </button>
        )}
        {isLast ? (
          <button type="button" className="btn-primary" onClick={onStartQuiz} disabled={topic.questionCount === 0 || isStarting}>
            {isStarting ? 'Test hazırlanıyor…' : `${topic.questionCount} Soruluk Teste Başla`}
          </button>
        ) : (
          <button type="button" className="btn-primary" onClick={() => setStep(step + 1)}>
            İlerle
          </button>
        )}
      </footer>
    </div>
  )
}

/** Optional first step: the lesson video, through the same prototype link as the 0/3 result screen. */
function VideoStep({ topic }: { topic: TopicDetail }) {
  const [showNotice, setShowNotice] = useState(false)

  return (
    <div className="flex flex-col items-center text-center">
      {/* Decorative (public/images/mascot-thoughtful.png, 1254×1254, transparent). */}
      <img
        src="/images/mascot-thoughtful.png"
        alt=""
        width={1254}
        height={1254}
        className="-mt-2 -mb-1 size-20 object-contain sm:size-24"
      />
      <h2 id="topic-detail-title" className="mt-1 text-xl font-bold tracking-tight text-ink">
        Önce ders videosunu izlemek ister misin?
      </h2>
      <p className="mt-1.5 max-w-lg text-sm text-ink-secondary">
        Ardından konunun önemli noktalarını ayrıntılı açıklamalarla adım adım gözden geçirip kısa testi çözeceksin.
      </p>
      {topic.latestTest && (
        <div className="mt-1">
          <LatestTestSummary test={topic.latestTest} evaluatedAt={topic.evaluatedAt} />
        </div>
      )}

      {/* About 85% of the body width; on narrow screens the thumbnail and the title stack. */}
      <section className="mt-4 w-[85%] rounded-tile border border-line p-4.5 text-left" aria-labelledby="reinforcement-video-title">
        <div className="flex flex-col items-center gap-3 text-center sm:flex-row sm:text-left">
          <span className="flex aspect-video w-28 shrink-0 items-center justify-center rounded-tile bg-brand text-white" aria-hidden="true">
            <PlayCircle size={30} />
          </span>
          <div className="w-full min-w-0 sm:flex-1">
            <p className="section-subtitle">Ders Videosu · İsteğe bağlı</p>
            <h3 id="reinforcement-video-title" className="truncate text-sm font-semibold text-ink">
              {topic.title}
            </h3>
          </div>
        </div>
        <button type="button" className="btn-secondary mt-2.5 inline-flex w-full items-center justify-center gap-1.5" onClick={() => setShowNotice(true)}>
          <PlayCircle size={16} aria-hidden="true" />
          Videoya Git
        </button>
        {/* Always rendered so screen readers announce the message when it appears. */}
        <p role="status" className="mt-2 text-xs text-ink-secondary empty:mt-0">
          {showNotice && LESSON_VIDEO_NOTICE}
        </p>
      </section>
    </div>
  )
}

export default ReinforcementSteps
