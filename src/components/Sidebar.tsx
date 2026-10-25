import { useState } from 'react'
import {
  Activity,
  BookOpen,
  ChevronDown,
  CircleHelp,
  CirclePlay,
  FileCheck,
  LayoutGrid,
  Presentation,
  SquareMousePointer,
  Tablet,
  UsersRound,
  Video,
  type LucideIcon,
} from 'lucide-react'
import Logo from './Logo'

interface NavItem {
  label: string
  /* A white line-art icon, or the path of a full-color logo image. */
  icon: LucideIcon | string
  /* Tile gradient (top-left → bottom-right), sampled from the Doping Hafıza web sidebar. */
  tile: [from: string, to: string]
  submenu?: string[]
}

/* Order, labels and tile colors follow the Doping Hafıza web sidebar. */
const NAV_ITEMS: NavItem[] = [
  { label: 'Dersler', icon: BookOpen, tile: ['#dfa565', '#d09352'] },
  { label: 'Ana Sayfa', icon: LayoutGrid, tile: ['#8898e8', '#7284d7'] },
  { label: 'Testler ve Denemeler', icon: FileCheck, tile: ['#6fd192', '#6dc08d'] },
  { label: 'WAY Evreni', icon: '/images/way-bird.png', tile: ['#c5caf9', '#c5caf9'] },
  { label: 'Rehberlik Videoları', icon: SquareMousePointer, tile: ['#80c6e2', '#71b5d2'] },
  { label: 'İstatistikler', icon: Activity, tile: ['#f6d67c', '#edc966'] },
  { label: 'Akıllı Değerlendirme Paneli', icon: Presentation, tile: ['#5461e7', '#3b4fe1'] },
  {
    label: 'Bire Bir Uygulamalar',
    icon: Tablet,
    tile: ['#ac4b70', '#7f417e'],
    submenu: ['Çözücü', 'Şimdi Anladım', 'Koçum Yanımda'],
  },
  { label: 'Yardım Merkezi', icon: CircleHelp, tile: ['#57b194', '#4ca189'] },
  { label: 'Canlı Etkinlikler', icon: Video, tile: ['#d14d4f', '#bb3f44'] },
  { label: 'YKS Butik', icon: UsersRound, tile: ['#5965f5', '#7e89f2'] },
  { label: 'Testimonial', icon: CirclePlay, tile: ['#64bdf4', '#5590e9'] },
]

function NavTile({ icon: Icon, tile: [from, to] }: NavItem) {
  return (
    <span
      className="sidebar-tile"
      style={{ backgroundImage: `linear-gradient(135deg, ${from}, ${to})` }}
      aria-hidden="true"
    >
      {typeof Icon === 'string' ? <img src={Icon} alt="" /> : <Icon strokeWidth={2} />}
    </span>
  )
}

function NavGroup({ item }: { item: NavItem & { submenu: string[] } }) {
  const [isOpen, setIsOpen] = useState(false)
  const submenuId = 'sidebar-submenu'

  return (
    <li>
      <button
        type="button"
        className="sidebar-link"
        aria-expanded={isOpen}
        aria-controls={submenuId}
        onClick={() => setIsOpen((open) => !open)}
      >
        <NavTile {...item} />
        <span className="flex min-w-0 items-center">
          <span className="sidebar-label">{item.label}</span>
          <ChevronDown className="sidebar-chevron" strokeWidth={2} aria-hidden="true" />
        </span>
      </button>
      <ul id={submenuId} className="sidebar-list" hidden={!isOpen}>
        {item.submenu.map((label) => (
          <li key={label}>
            <a href="#" className="sidebar-link sidebar-sublink">
              <span className="sidebar-label">{label}</span>
            </a>
          </li>
        ))}
      </ul>
    </li>
  )
}

interface SidebarProps {
  activeLabel?: string
}

function Sidebar({ activeLabel = 'Ana Sayfa' }: SidebarProps) {
  return (
    <aside className="sidebar sticky top-0 hidden h-screen shrink-0 overflow-y-auto lg:flex">
      <Logo className="sidebar-logo" />

      <nav aria-label="Ana menü">
        <ul className="sidebar-list">
          {NAV_ITEMS.map((item) => {
            if (item.submenu) return <NavGroup key={item.label} item={{ ...item, submenu: item.submenu }} />

            const isActive = item.label === activeLabel
            return (
              <li key={item.label}>
                <a href="#" className="sidebar-link" aria-current={isActive ? 'page' : undefined}>
                  <NavTile {...item} />
                  <span className="sidebar-label">{item.label}</span>
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
