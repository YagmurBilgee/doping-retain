import { useEffect, useRef, useState, type KeyboardEvent } from 'react'
import { CalendarCheck, RotateCcw } from 'lucide-react'
import { errorMessage, getWeeklySummary } from '../api/client'
import type { LoadState, WeeklySummary, WeeklySummaryDay } from '../api/types'
import ModalShell from './ModalShell'
import { formatCalendarDay, formatDateRange, formatTime, topicContext } from './reviewPresentation'

interface WeeklySummaryModalProps {
  /** The student whose week is shown; fixed for this mount */
  studentId: string
  onClose: () => void
}

const WEEKDAY_SHORT = ['Pzt', 'Sal', 'Çar', 'Per', 'Cum', 'Cmt', 'Paz']

/**
 * "Bu Hafta Neleri Tekrar Ettin?": the backend's summary of this Monday–Sunday week (Europe/Istanbul).
 * Loaded on every open, so it always reflects the latest completions; nothing is counted here.
 * Same frame as the reinforcement steps: 760 × 600 on desktop, at most the viewport, the body scrolls.
 */
function WeeklySummaryModal({ studentId, onClose }: WeeklySummaryModalProps) {
  const [summary, setSummary] = useState<LoadState<WeeklySummary>>({ status: 'loading' })
  const [attempt, setAttempt] = useState(0)

  useEffect(() => {
    const controller = new AbortController()
    getWeeklySummary(studentId, controller.signal).then(
      (data) => setSummary({ status: 'ready', data }),
      (error: unknown) => {
        if (!controller.signal.aborted) setSummary({ status: 'error', message: errorMessage(error) })
      },
    )
    return () => controller.abort()
  }, [studentId, attempt])

  const retry = () => {
    setSummary({ status: 'loading' })
    setAttempt((current) => current + 1)
  }

  return (
    <ModalShell isOpen size="medium" onClose={onClose} labelledBy="weekly-summary-title">
      {/* 600px popup = this height + p-card padding (2 × 1.25rem) + 2px border. */}
      <div className="flex h-[558px] max-h-[calc(100dvh-4.5rem-2px)] flex-col">
        <header className="shrink-0 pr-10">
          <h2 id="weekly-summary-title" className="text-xl font-bold tracking-tight text-ink">
            Bu Hafta Neleri Tekrar Ettin?
          </h2>
          <p className="mt-0.5 text-sm text-ink-secondary">
            {summary.status === 'ready' ? formatDateRange(summary.data.firstDate, summary.data.lastDate) : 'Bu hafta'}
          </p>
        </header>

        {summary.status === 'ready' ? (
          <SummaryBody data={summary.data} />
        ) : (
          <div className="flex flex-1 flex-col items-center justify-center text-center" aria-busy={summary.status === 'loading'}>
            {summary.status === 'loading' ? (
              <p className="text-sm text-ink-secondary">Haftalık özet yükleniyor…</p>
            ) : (
              <div role="alert">
                <p className="text-sm text-ink-secondary">Haftalık özet yüklenemedi. {summary.message}</p>
                <button type="button" className="btn-secondary mt-3 inline-flex items-center gap-1.5" onClick={retry}>
                  <RotateCcw size={14} aria-hidden="true" />
                  Tekrar Dene
                </button>
              </div>
            )}
          </div>
        )}
      </div>
    </ModalShell>
  )
}

function SummaryBody({ data }: { data: WeeklySummary }) {
  // Today is selected first; it always lies inside the current week.
  const [selected, setSelected] = useState(() => Math.max(0, data.days.findIndex((day) => day.date === data.today)))
  const isEmptyWeek = data.completedReviewCount === 0

  return (
    <div className="mt-4 min-h-0 flex-1 overflow-y-auto overscroll-contain">
      <dl className="grid grid-cols-3 gap-2">
        <Stat label="Farklı konu" value={data.distinctTopicCount} />
        <Stat label="Tamamlanan tekrar" value={data.completedReviewCount} />
        <Stat label="Tekrar yapılan gün" value={data.activeDayCount} />
      </dl>

      <DayChart days={data.days} today={data.today} selected={selected} onSelect={setSelected} />

      {isEmptyWeek ? (
        <p className="mt-4 rounded-tile bg-brand-soft p-card-sm text-center text-sm font-semibold text-brand">
          Bu haftanın ilk tekrarına hazır mısın?
        </p>
      ) : (
        <DayTopics day={data.days[selected]} isToday={data.days[selected].date === data.today} />
      )}
    </div>
  )
}

function Stat({ label, value }: { label: string; value: number }) {
  return (
    <div className="rounded-tile bg-surface-muted px-1.5 py-card-sm text-center sm:px-card-sm">
      <dt className="text-xs leading-tight text-ink-secondary">{label}</dt>
      <dd className="mt-1 text-2xl font-bold text-ink">{value}</dd>
    </div>
  )
}

interface DayChartProps {
  days: WeeklySummaryDay[]
  today: string
  selected: number
  onSelect: (index: number) => void
}

/** Integer y-axis ticks from 0 up to at least the highest day (at most five labels). */
function integerTicks(max: number): number[] {
  const step = Math.max(1, Math.ceil(max / 4))
  const top = Math.max(1, Math.ceil(max / step) * step)
  return Array.from({ length: top / step + 1 }, (_, index) => index * step)
}

/**
 * Seven columns, Monday to Sunday: distinct topics reviewed that day. A radio group: click or tap a column, or use
 * the arrow, Home and End keys; only the selected column is in the tab order.
 */
function DayChart({ days, today, selected, onSelect }: DayChartProps) {
  const ticks = integerTicks(Math.max(...days.map((day) => day.distinctTopicCount)))
  const top = ticks[ticks.length - 1]
  const buttons = useRef<(HTMLButtonElement | null)[]>([])

  const move = (index: number) => {
    onSelect(index)
    buttons.current[index]?.focus()
  }

  const handleKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    const next = { ArrowRight: selected + 1, ArrowDown: selected + 1, ArrowLeft: selected - 1, ArrowUp: selected - 1, Home: 0, End: days.length - 1 }[event.key]
    if (next === undefined) return
    event.preventDefault()
    move((next + days.length) % days.length)
  }

  return (
    <figure className="mt-4">
      <figcaption className="section-subtitle">Gün gün tekrar edilen farklı konu</figcaption>
      <div className="mt-4 flex gap-2">
        {/* Y axis: integer labels only, aligned with the gridlines of the plot area. */}
        <div className="relative h-36 w-5 shrink-0 text-right text-[11px] text-ink-muted" aria-hidden="true">
          {ticks.map((tick) => (
            <span key={tick} className="absolute right-0 -translate-y-1/2" style={{ bottom: `calc(${(tick / top) * 100}% - 0.5rem)` }}>
              {tick}
            </span>
          ))}
        </div>
        <div className="min-w-0 flex-1">
          <div className="relative h-36">
            {ticks.map((tick) => (
              <span key={tick} className="absolute inset-x-0 border-t border-line" style={{ bottom: `${(tick / top) * 100}%` }} aria-hidden="true" />
            ))}
            <div role="radiogroup" aria-label="Haftanın günleri" className="absolute inset-0 grid grid-cols-7 gap-1.5" onKeyDown={handleKeyDown}>
              {days.map((day, index) => {
                const isSelected = index === selected
                const count = day.distinctTopicCount
                return (
                  <button
                    key={day.date}
                    ref={(element) => {
                      buttons.current[index] = element
                    }}
                    type="button"
                    role="radio"
                    aria-checked={isSelected}
                    tabIndex={isSelected ? 0 : -1}
                    aria-label={`${formatCalendarDay(day.date)}${day.date === today ? ' (bugün)' : ''}: ${count} farklı konu`}
                    onClick={() => move(index)}
                    className={`flex h-full items-end justify-center rounded-t-md transition-colors focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-brand ${isSelected ? 'bg-brand-soft' : 'hover:bg-surface-muted'}`}
                  >
                    <span
                      className={`w-3/5 max-w-10 rounded-t-md transition-colors ${isSelected ? 'bg-brand' : 'bg-[#b9c4f3]'}`}
                      style={{ height: `${(count / top) * 100}%` }}
                    />
                  </button>
                )
              })}
            </div>
          </div>
          <div className="mt-1.5 grid grid-cols-7 gap-1.5 text-center text-xs" aria-hidden="true">
            {days.map((day, index) => (
              <span key={day.date} className={index === selected ? 'font-bold text-brand' : day.date === today ? 'font-semibold text-ink' : 'text-ink-secondary'}>
                {WEEKDAY_SHORT[index]}
                <span className="block text-[11px] font-normal text-ink-muted">{Number(day.date.slice(8))}</span>
              </span>
            ))}
          </div>
        </div>
      </div>
    </figure>
  )
}

/** The selected day's topics: one row per topic, with its latest completion and how many reviews it had. */
function DayTopics({ day, isToday }: { day: WeeklySummaryDay; isToday: boolean }) {
  return (
    <section className="mt-4" aria-labelledby="weekly-day-title" aria-live="polite">
      <h3 id="weekly-day-title" className="text-sm font-semibold text-ink">
        {formatCalendarDay(day.date)}
        {isToday && <span className="ml-1.5 font-normal text-ink-muted">· bugün</span>}
      </h3>
      {day.topics.length === 0 ? (
        <p className="mt-2 text-sm text-ink-secondary">Bu gün tekrar yapılmadı.</p>
      ) : (
        <ul className="mt-2 flex flex-col gap-2">
          {day.topics.map((topic) => (
            <li key={topic.topicId} className="flex items-center gap-3 rounded-tile border border-line p-card-sm">
              <span className="icon-tile size-9 shrink-0 bg-brand-soft text-brand" aria-hidden="true">
                <CalendarCheck size={16} />
              </span>
              <div className="min-w-0 flex-1">
                <p className="section-subtitle">{topicContext(topic)}</p>
                <p className="truncate text-sm font-semibold text-ink">{topic.title}</p>
              </div>
              <div className="shrink-0 text-right text-xs text-ink-secondary">
                <p>
                  {formatTime(topic.lastCompletedAt)}
                  {topic.correctCount !== null && topic.totalQuestions !== null && (
                    <span className="ml-1.5 font-semibold text-ink">
                      {topic.correctCount}/{topic.totalQuestions}
                    </span>
                  )}
                </p>
                {topic.reviewCount > 1 && <span className="badge badge-brand mt-1">{topic.reviewCount} tekrar</span>}
              </div>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}

export default WeeklySummaryModal
