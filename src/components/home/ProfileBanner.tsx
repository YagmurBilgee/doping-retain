import { Coins, Flame, Trophy, UserRound } from 'lucide-react'

const ACHIEVEMENTS = ['Başlangıç', 'İlk Video', 'Haftalık Seri']

function ProfileBanner() {
  return (
    <section className="card-dark flex flex-1 items-center gap-5">
      <span className="flex size-20 shrink-0 items-center justify-center rounded-card bg-risk-soft text-risk-ink" aria-hidden="true">
        <UserRound size={40} />
      </span>
      <div className="min-w-0 flex-1">
        <div className="flex items-center justify-between gap-3">
          <h1 className="truncate text-2xl font-bold">yagmur akkose</h1>
          <span className="flex items-center gap-1 text-sm font-semibold">
            <Flame size={16} className="text-coin" aria-hidden="true" />1
          </span>
        </div>
        <div className="mt-3 flex flex-wrap items-center gap-2">
          <span className="metric-pill">
            <Coins size={16} className="text-coin" aria-hidden="true" />
            615
          </span>
          {ACHIEVEMENTS.map((label) => (
            <span key={label} className="badge bg-white/10 text-white">
              <Trophy size={12} aria-hidden="true" />
              {label}
            </span>
          ))}
        </div>
      </div>
    </section>
  )
}

export default ProfileBanner
