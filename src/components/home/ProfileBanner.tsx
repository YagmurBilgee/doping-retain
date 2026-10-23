import { Award, Coins, Flame, PlayCircle, UserRound } from 'lucide-react'

// Placeholder badges: the real Doping Hafıza badge artwork is not in the repo yet.
const ACHIEVEMENTS = [
  { label: 'Başlangıç', icon: Award },
  { label: 'İlk Video', icon: PlayCircle },
  { label: 'Haftalık Seri', icon: Flame },
]

function ProfileBanner() {
  return (
    <section className="relative flex flex-1 items-start gap-4 rounded-card bg-neutral-900 p-card text-white shadow-card">
      <div className="flex shrink-0 flex-col items-center gap-2">
        <span className="flex size-20 items-center justify-center rounded-tile bg-risk-soft text-risk-ink" aria-hidden="true">
          <UserRound size={40} />
        </span>
        <span className="metric-pill">
          <Coins size={16} className="text-coin" aria-hidden="true" />
          615
        </span>
      </div>

      <div className="min-w-0 flex-1 pt-1 pr-10">
        <h1 className="truncate text-2xl font-bold">yagmur akkose</h1>
        <ul className="mt-2 flex items-center gap-1.5" aria-label="Rozetler">
          {ACHIEVEMENTS.map(({ label, icon: Icon }) => (
            <li
              key={label}
              title={label}
              aria-label={label}
              className="flex size-7 items-center justify-center rounded-full bg-white/10 text-coin"
            >
              <Icon size={14} aria-hidden="true" />
            </li>
          ))}
        </ul>
      </div>

      <span className="absolute top-card right-card flex items-center gap-1 text-sm font-semibold">
        <Flame size={16} className="text-coin" aria-hidden="true" />1
      </span>
    </section>
  )
}

export default ProfileBanner
