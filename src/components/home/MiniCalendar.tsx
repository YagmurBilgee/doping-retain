import { ChevronLeft, ChevronRight } from 'lucide-react'

/* Static week mirroring the reference screen. */
const WEEK_DAYS = [
  { day: 'Pzt', date: '05' },
  { day: 'Sal', date: '06' },
  { day: 'Çar', date: '07' },
  { day: 'Per', date: '08' },
  { day: 'Cum', date: '09' },
  { day: 'Cmt', date: '10' },
  { day: 'Paz', date: '11', isToday: true },
]

function MiniCalendar() {
  return (
    <section>
      <div className="flex items-center justify-between">
        <h2 className="section-title">Ekim 2026</h2>
        <div className="flex items-center gap-1 text-ink-secondary">
          <button type="button" aria-label="Önceki hafta" className="rounded-full p-1 hover:bg-surface">
            <ChevronLeft size={18} />
          </button>
          <button type="button" className="btn-secondary bg-surface px-3 py-1 text-xs">
            Bugün
          </button>
          <button type="button" aria-label="Sonraki hafta" className="rounded-full p-1 hover:bg-surface">
            <ChevronRight size={18} />
          </button>
        </div>
      </div>
      <ol className="mt-3 grid grid-cols-7 gap-1 text-center">
        {WEEK_DAYS.map(({ day, date, isToday }) => (
          <li key={date} className="flex flex-col items-center gap-1">
            <span className="text-xs text-ink-muted">{day}</span>
            <span
              className={`flex size-9 items-center justify-center rounded-full text-sm font-semibold ${
                isToday ? 'bg-danger text-white' : 'text-ink'
              }`}
              aria-current={isToday ? 'date' : undefined}
            >
              {date}
            </span>
          </li>
        ))}
      </ol>
    </section>
  )
}

export default MiniCalendar
