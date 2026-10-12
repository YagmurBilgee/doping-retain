import {
  Activity,
  BookOpen,
  Bot,
  ChevronDown,
  CircleHelp,
  FileCheck2,
  LayoutGrid,
  MonitorPlay,
  Orbit,
  Radio,
  UsersRound,
  type LucideIcon,
} from 'lucide-react'

interface NavItem {
  label: string
  icon: LucideIcon
  tint: string
  hasSubmenu?: boolean
}

const NAV_ITEMS: NavItem[] = [
  { label: 'Ana Sayfa', icon: LayoutGrid, tint: 'bg-brand text-white' },
  { label: 'Dersler', icon: BookOpen, tint: 'bg-risk-soft text-risk-ink' },
  { label: 'Testler ve Denemeler', icon: FileCheck2, tint: 'bg-success-soft text-success-ink' },
  { label: 'WAY Evreni', icon: Orbit, tint: 'bg-brand-soft text-brand' },
  { label: 'Rehberlik Videoları', icon: MonitorPlay, tint: 'bg-brand-soft text-brand' },
  { label: 'İstatistikler', icon: Activity, tint: 'bg-risk-soft text-risk-ink' },
  { label: 'Akıllı Değerlendirme Paneli', icon: Bot, tint: 'bg-danger-soft text-danger-ink' },
  { label: 'Bire Bir Uygulamalar', icon: UsersRound, tint: 'bg-brand-soft text-brand', hasSubmenu: true },
  { label: 'Yardım Merkezi', icon: CircleHelp, tint: 'bg-success-soft text-success-ink' },
  { label: 'Canlı Etkinlikler', icon: Radio, tint: 'bg-danger-soft text-danger-ink' },
]

interface SidebarProps {
  activeLabel?: string
}

function Sidebar({ activeLabel = 'Ana Sayfa' }: SidebarProps) {
  return (
    <aside className="sticky top-0 hidden h-screen w-72 shrink-0 flex-col gap-6 overflow-y-auto bg-surface px-4 py-6 shadow-card lg:flex">
      <div className="px-2">
        <p className="text-2xl font-extrabold tracking-tight">
          <span className="text-danger">doping</span>
          <span className="text-brand">hafıza</span>
        </p>
        <p className="section-subtitle">öğrenmenin dijital hali</p>
      </div>

      <nav aria-label="Ana menü">
        <ul className="flex flex-col gap-1">
          {NAV_ITEMS.map(({ label, icon: Icon, tint, hasSubmenu }) => {
            const isActive = label === activeLabel
            return (
              <li key={label}>
                <a
                  href="#"
                  className={`nav-item ${isActive ? 'nav-item-active' : ''}`}
                  aria-current={isActive ? 'page' : undefined}
                >
                  <span className={`icon-tile size-8 rounded-lg ${tint}`} aria-hidden="true">
                    <Icon size={16} />
                  </span>
                  <span className="flex-1">{label}</span>
                  {hasSubmenu && <ChevronDown size={16} aria-hidden="true" />}
                </a>
              </li>
            )
          })}
        </ul>
      </nav>
    </aside>
  )
}

export default Sidebar
