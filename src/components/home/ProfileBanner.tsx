import { Award, Flame, PlayCircle, Star, Sun } from 'lucide-react'

// Placeholder badges: the real Doping Hafıza badge artwork is not in the repo yet.
const ACHIEVEMENTS = [
  { label: 'Başlangıç', icon: Award },
  { label: 'İlk Video', icon: PlayCircle },
  { label: 'Haftalık Seri', icon: Flame },
]

interface ProfileBannerProps {
  /** The selected student's name; empty while it loads */
  name?: string
  /**
   * Shows the sample point and streak numbers. They are shared decoration, so they are hidden where several
   * demo students would otherwise appear to have the same measured values.
   */
  showDecorativeScores: boolean
}

function ProfileBanner({ name, showDecorativeScores }: ProfileBannerProps) {
  return (
    <section className="relative min-h-[193px] min-w-0 flex-1 rounded-tile bg-linear-to-b from-[#303e4f] to-[#1e2b3c] p-[21px] font-manrope text-white shadow-card">
      <div className="flex gap-4">
        <div className="flex shrink-0 flex-col items-center gap-1.5">
          <span className="avatar-surface block size-[102px] overflow-hidden rounded-[12px]">
            <img src="/images/student-avatar.png" alt="" className="size-full object-contain" />
          </span>
          {showDecorativeScores && (
            <span className="inline-flex h-6 items-center gap-1.5 rounded-full bg-linear-to-b from-white to-[#eceff3] pr-3.5 pl-1 text-base font-extrabold text-[#977a34] shadow-card">
              <span
                className="flex size-6 items-center justify-center rounded-full bg-linear-to-b from-[#f8cd52] to-[#e09b1c]"
                aria-hidden="true"
              >
                <Star size={13} fill="#fde68a" stroke="#b9770e" strokeWidth={2} />
              </span>
              615
            </span>
          )}
        </div>

        {/* Room for the streak number only where it is shown. */}
        <div className={`flex min-w-0 flex-1 flex-col pt-2.5 ${showDecorativeScores ? 'pr-16' : ''}`}>
          <h1 className="truncate text-2xl font-extrabold">{name ?? '\u00a0'}</h1>
          <ul className="mt-auto flex items-center gap-2" aria-label="Rozetler">
            {ACHIEVEMENTS.map(({ label, icon: Icon }) => (
              <li
                key={label}
                title={label}
                aria-label={label}
                className="flex size-[35px] items-center justify-center rounded-full bg-white/10 text-coin ring-1 ring-white/30"
              >
                <Icon size={17} aria-hidden="true" />
              </li>
            ))}
          </ul>
        </div>
      </div>

      {showDecorativeScores && (
        <span className="absolute top-[29px] right-[29px] flex items-center gap-2 text-[26px] leading-none font-extrabold text-[#fbe57a]">
          <Sun size={26} fill="#fcd34d" stroke="#f08a24" strokeWidth={2} aria-hidden="true" />1
        </span>
      )}
    </section>
  )
}

export default ProfileBanner
