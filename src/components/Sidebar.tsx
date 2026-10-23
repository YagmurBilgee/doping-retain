import {
  Activity,
  Bird,
  BookOpen,
  Bot,
  ChevronDown,
  CircleHelp,
  FileCheck2,
  LayoutGrid,
  MonitorPlay,
  Smartphone,
  Video,
  type LucideIcon,
} from 'lucide-react'
import Logo from './Logo'

interface NavItem {
  label: string
  icon: LucideIcon
  tile: string
  hasSubmenu?: boolean
}

/* Order, labels and tile colors follow the Doping Hafıza Desktop client. */
const NAV_ITEMS: NavItem[] = [
  { label: 'Ana Sayfa', icon: LayoutGrid, tile: 'bg-nav-blue' },
  { label: 'Dersler', icon: BookOpen, tile: 'bg-nav-terracotta' },
  { label: 'Testler ve Denemeler', icon: FileCheck2, tile: 'bg-nav-green' },
  { label: 'WAY Evreni', icon: Bird, tile: 'bg-nav-sky' },
  { label: 'Rehberlik Videoları', icon: MonitorPlay, tile: 'bg-nav-video' },
  { label: 'İstatistikler', icon: Activity, tile: 'bg-nav-amber' },
  { label: 'Akıllı Değerlendirme Paneli', icon: Bot, tile: 'bg-nav-orchid' },
  { label: 'Bire Bir Uygulamalar', icon: Smartphone, tile: 'bg-nav-mauve', hasSubmenu: true },
  { label: 'Yardım Merkezi', icon: CircleHelp, tile: 'bg-nav-teal' },
  { label: 'Canlı Etkinlikler', icon: Video, tile: 'bg-nav-coral' },
]

interface SidebarProps {
  activeLabel?: string
}

function Sidebar({ activeLabel = 'Ana Sayfa' }: SidebarProps) {
  return (
    <aside className="sticky top-7 hidden h-[calc(100vh-1.75rem)] w-[300px] shrink-0 flex-col gap-6 overflow-y-auto bg-surface px-3 py-5 lg:flex">
      <div className="px-2">
        <Logo />
      </div>

      <nav aria-label="Ana menü">
        <ul className="flex flex-col gap-0.5">
          {NAV_ITEMS.map(({ label, icon: Icon, tile, hasSubmenu }) => {
            const isActive = label === activeLabel
            return (
              <li key={label}>
                <a
                  href="#"
                  className={`nav-item ${isActive ? 'nav-item-active' : ''}`}
                  aria-current={isActive ? 'page' : undefined}
                >
                  <span className={`icon-tile size-7 rounded-lg text-white ${tile}`} aria-hidden="true">
                    <Icon size={15} strokeWidth={2.25} />
                  </span>
                  <span className="min-w-0 flex-1 truncate">{label}</span>
                  {hasSubmenu && <ChevronDown size={14} className="shrink-0 text-ink-secondary" aria-hidden="true" />}
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
