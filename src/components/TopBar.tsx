import { Bell, CalendarCheck, ChevronDown, Mail, Search, UserRound } from 'lucide-react'

const QUICK_ACTIONS = [
  { label: 'Mesajlar', icon: Mail },
  { label: 'Takvim', icon: CalendarCheck },
  { label: 'Bildirimler', icon: Bell },
]

function TopBar() {
  return (
    <header className="flex items-center gap-4">
      <label className="search-field flex-1">
        <Search size={20} className="shrink-0 text-ink-secondary" aria-hidden="true" />
        <input
          type="search"
          placeholder="Ders, ünite ya da konu ara"
          className="w-full bg-transparent outline-none placeholder:text-ink-muted"
        />
      </label>

      <div className="flex items-center gap-2">
        {QUICK_ACTIONS.map(({ label, icon: Icon }) => (
          <button
            key={label}
            type="button"
            aria-label={label}
            className="flex size-11 items-center justify-center rounded-full bg-surface text-ink-secondary shadow-card transition-colors hover:text-brand"
          >
            <Icon size={20} />
          </button>
        ))}
        <span
          className="flex size-11 items-center justify-center rounded-full bg-risk-soft text-risk-ink"
          aria-hidden="true"
        >
          <UserRound size={20} />
        </span>
        <button type="button" className="btn-secondary hidden bg-surface shadow-card sm:inline-flex">
          12. Sınıf
          <ChevronDown size={16} aria-hidden="true" />
        </button>
      </div>
    </header>
  )
}

export default TopBar
