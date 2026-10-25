import { Bell, CalendarCheck, ChevronDown, Mail, Search, Zap } from 'lucide-react'

const QUICK_ACTIONS = [
  { label: 'Mesajlar', icon: Mail },
  { label: 'Takvim', icon: CalendarCheck },
  { label: 'Bildirimler', icon: Bell },
]

const ROUND_BUTTON = 'flex size-13 shrink-0 items-center justify-center rounded-full'

function TopBar() {
  return (
    <header className="flex flex-wrap items-center gap-[15px] sm:flex-nowrap">
      <label className="search-field w-full min-w-0 sm:w-auto sm:flex-1">
        <Search size={22} className="shrink-0 text-ink-slate" aria-hidden="true" />
        <span className="sr-only">Ara</span>
        <input
          type="search"
          placeholder="Ders, ünite ya da konu ara"
          className="w-full min-w-0 bg-transparent outline-none placeholder:text-ink-slate"
        />
      </label>

      <div className="flex shrink-0 items-center gap-[11px]">
        <button
          type="button"
          aria-label="Hızlı erişim"
          className={`${ROUND_BUTTON} bg-linear-to-b from-[#ea727e] to-[#ce34a5] text-white transition-opacity hover:opacity-90`}
        >
          <Zap size={26} strokeWidth={1.75} />
        </button>
        {QUICK_ACTIONS.map(({ label, icon: Icon }) => (
          <button
            key={label}
            type="button"
            aria-label={label}
            className={`${ROUND_BUTTON} bg-field text-ink transition-colors hover:text-brand`}
          >
            <Icon size={24} strokeWidth={1.75} />
          </button>
        ))}
        <span className={`${ROUND_BUTTON} avatar-surface overflow-hidden`} aria-hidden="true">
          <img src="/images/student-avatar.png" alt="" className="size-full object-contain" />
        </span>
        <button
          type="button"
          className="hidden h-13 items-center gap-1 rounded-tile bg-surface px-3.5 font-manrope text-[15px] text-ink-slate shadow-float sm:inline-flex"
        >
          12. Sınıf
          <ChevronDown size={16} aria-hidden="true" />
        </button>
      </div>
    </header>
  )
}

export default TopBar
