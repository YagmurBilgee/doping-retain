import { useState } from 'react'
import { ChevronLeft, ChevronRight } from 'lucide-react'

const DAY_NAME = new Intl.DateTimeFormat('tr-TR', { weekday: 'short' })
const MONTH_TITLE = new Intl.DateTimeFormat('tr-TR', { month: 'long', year: 'numeric' })

/** Monday-to-Sunday dates of the week weekOffset weeks from the current one, in local time. */
function weekDays(today: Date, weekOffset: number): Date[] {
  const monday = new Date(today.getFullYear(), today.getMonth(), today.getDate() - ((today.getDay() + 6) % 7) + weekOffset * 7)
  return Array.from({ length: 7 }, (_, index) => new Date(monday.getFullYear(), monday.getMonth(), monday.getDate() + index))
}

function isSameDay(a: Date, b: Date): boolean {
  return a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate()
}

function MiniCalendar() {
  // Read once per mount so re-renders stay pure.
  const [today] = useState(() => new Date())
  const [weekOffset, setWeekOffset] = useState(0)
  const days = weekDays(today, weekOffset)

  return (
    <section>
      <div className="flex items-center justify-between">
        {/* Thursday decides the month of a week that spans two months. */}
        <h2 className="section-title capitalize">{MONTH_TITLE.format(days[3])}</h2>
        <div className="flex items-center gap-1 text-ink-secondary">
          <button type="button" aria-label="Önceki hafta" className="rounded-full p-1 hover:bg-surface" onClick={() => setWeekOffset((week) => week - 1)}>
            <ChevronLeft size={18} />
          </button>
          <button
            type="button"
            className="rounded-lg bg-field px-3 py-1.5 font-manrope text-xs font-medium text-heading transition-colors hover:bg-line focus-visible:outline-2 focus-visible:outline-brand"
            onClick={() => setWeekOffset(0)}
          >
            Bugün
          </button>
          <button type="button" aria-label="Sonraki hafta" className="rounded-full p-1 hover:bg-surface" onClick={() => setWeekOffset((week) => week + 1)}>
            <ChevronRight size={18} />
          </button>
        </div>
      </div>
      <ol className="mt-3 grid grid-cols-7 gap-1 text-center font-manrope">
        {days.map((day) => {
          const isToday = isSameDay(day, today)
          return (
            // Today is an orange rounded tile holding both the day name and the date, as on the reference.
            <li
              key={day.toISOString()}
              className={`flex flex-col items-center justify-center gap-1.5 rounded-[9px] py-2.5 ${
                isToday ? 'bg-calendar-active text-white shadow-[0_6px_9px_rgb(255_109_67/0.2)]' : ''
              }`}
              aria-current={isToday ? 'date' : undefined}
            >
              <span className={`text-xs font-medium ${isToday ? 'text-white' : 'text-heading-muted'}`}>{DAY_NAME.format(day)}</span>
              <span className={`font-bold ${isToday ? 'text-xl leading-none' : 'text-base leading-none text-heading'}`}>
                {String(day.getDate()).padStart(2, '0')}
              </span>
            </li>
          )
        })}
      </ol>
    </section>
  )
}

export default MiniCalendar
