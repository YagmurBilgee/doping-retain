import { Bell, CalendarCheck, ChevronDown, Mail, Search, UserRound } from 'lucide-react'

const QUICK_ACTIONS = [
  { label: 'Mesajlar', icon: Mail },
  { label: 'Takvim', icon: CalendarCheck },
  { label: 'Bildirimler', icon: Bell },
]

function TopBar() {
  return (
    <header className="flex items-center gap-4">
      <label className="search-field min-w-0 flex-1">
        <Search size={22} className="shrink-0 text-ink-secondary" aria-hidden="true" />
        <span className="sr-only">Ara</span>
        <input
          type="search"
          placeholder="Ders, ünite ya da konu ara"
          className="w-full min-w-0 bg-transparent outline-none placeholder:text-ink-secondary"
        />
      </label>

      <div className="flex shrink-0 items-center gap-2.5">
        {QUICK_ACTIONS.map(({ label, icon: Icon }) => (
          <button
            key={label}
            type="button"
            aria-label={label}
            className="flex size-12 items-center justify-center rounded-full bg-field text-ink transition-colors hover:text-brand"
          >
            <Icon size={22} strokeWidth={1.75} />
          </button>
        ))}
        <span
          className="flex size-12 items-center justify-center rounded-full bg-risk-soft text-risk-ink"
          aria-hidden="true"
        >
          <UserRound size={22} />
        </span>
        <button
          type="button"
          className="hidden h-12 items-center gap-2 rounded-tile bg-surface px-3.5 text-sm text-ink-secondary shadow-float sm:inline-flex"
        >
          12. Sınıf
          <ChevronDown size={16} aria-hidden="true" />
        </button>
      </div>
    </header>
  )
}

export default TopBar
