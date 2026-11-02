import { useEffect, useId, useRef, useState } from 'react'
import { Bell, CalendarCheck, ChevronDown, Mail, Search, SlidersHorizontal, Zap } from 'lucide-react'
import type { DemoGroup, Student } from '../api/types'
import { GRADE_OPTIONS, gradeOption, onInAppLinkClick } from '../demoSelection'
import StudentSelector from './StudentSelector'

const QUICK_ACTIONS = [
  { label: 'Mesajlar', icon: Mail },
  { label: 'Takvim', icon: CalendarCheck },
  { label: 'Bildirimler', icon: Bell },
]

const ROUND_BUTTON = 'flex size-13 shrink-0 items-center justify-center rounded-full'

interface GradeSelectorProps {
  group: DemoGroup
  onSelectGroup: (group: DemoGroup) => void
}

/** The grade menu in the top-right corner: switches the demo group, never a student's own grade. */
function GradeSelector({ group, onSelectGroup }: GradeSelectorProps) {
  const [isOpen, setIsOpen] = useState(false)
  const rootRef = useRef<HTMLDivElement>(null)
  const buttonRef = useRef<HTMLButtonElement>(null)
  const menuId = useId()
  const current = gradeOption(group)

  // Closes on a click outside or Escape (focus returns to the button).
  useEffect(() => {
    if (!isOpen) return
    const onPointerDown = (event: PointerEvent) => {
      if (!rootRef.current?.contains(event.target as Node)) setIsOpen(false)
    }
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key !== 'Escape') return
      setIsOpen(false)
      buttonRef.current?.focus()
    }
    document.addEventListener('pointerdown', onPointerDown)
    document.addEventListener('keydown', onKeyDown)
    return () => {
      document.removeEventListener('pointerdown', onPointerDown)
      document.removeEventListener('keydown', onKeyDown)
    }
  }, [isOpen])

  return (
    <div ref={rootRef} className="relative">
      <button
        ref={buttonRef}
        type="button"
        aria-haspopup="menu"
        aria-expanded={isOpen}
        aria-controls={menuId}
        aria-label={`Sınıf: ${current.label}`}
        onClick={() => setIsOpen((open) => !open)}
        className="inline-flex h-13 items-center gap-1 rounded-tile bg-surface px-3.5 font-manrope text-[15px] text-ink-slate shadow-float focus-visible:outline-2 focus-visible:outline-brand"
      >
        {current.shortLabel}
        <ChevronDown size={16} aria-hidden="true" className={`transition-transform ${isOpen ? 'rotate-180' : ''}`} />
      </button>
      {isOpen && (
        <ul
          id={menuId}
          role="menu"
          aria-label="Sınıf seç"
          className="absolute top-full right-0 z-30 mt-1 min-w-full overflow-hidden rounded-tile bg-surface py-1 font-manrope text-[15px] whitespace-nowrap shadow-float"
        >
          {GRADE_OPTIONS.map((option) => {
            const isSelected = option.group === group
            return (
              <li key={option.group} role="none">
                <button
                  type="button"
                  role="menuitemradio"
                  aria-checked={isSelected}
                  onClick={() => {
                    setIsOpen(false)
                    onSelectGroup(option.group)
                  }}
                  className={`block w-full px-4 py-3 text-center transition-colors hover:bg-field focus-visible:bg-field focus-visible:outline-none ${
                    isSelected ? 'bg-field font-bold text-heading' : 'text-ink-slate'
                  }`}
                >
                  {option.label}
                </button>
              </li>
            )
          })}
        </ul>
      )}
    </div>
  )
}

interface TopBarProps {
  group: DemoGroup
  onSelectGroup: (group: DemoGroup) => void
  students: Student[]
  studentId: string | null
  onSelectStudent: (studentId: string) => void
  /** Link target of the demo panel; null hides the link (other grades, or Demo:Enabled off) */
  demoPanelHref: string | null
  onOpenDemoPanel: () => void
}

function TopBar({ group, onSelectGroup, students, studentId, onSelectStudent, demoPanelHref, onOpenDemoPanel }: TopBarProps) {
  return (
    // One row from xl up; below it the search takes its own row so the selectors never squeeze it.
    <header className="flex flex-wrap items-center gap-[15px] xl:flex-nowrap">
      <label className="search-field w-full min-w-0 xl:w-auto xl:flex-1">
        <Search size={22} className="shrink-0 text-ink-slate" aria-hidden="true" />
        <span className="sr-only">Ara</span>
        <input
          type="search"
          placeholder="Ders, ünite ya da konu ara"
          className="w-full min-w-0 bg-transparent outline-none placeholder:text-ink-slate"
        />
      </label>

      {/* Wraps on narrow screens so the grade menu stays reachable on phones. */}
      <div className="flex flex-wrap items-center gap-[11px] xl:shrink-0 xl:flex-nowrap">
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
        <GradeSelector group={group} onSelectGroup={onSelectGroup} />
        <StudentSelector students={students} studentId={studentId} onSelectStudent={onSelectStudent} />
        {demoPanelHref && (
          <a
            href={demoPanelHref}
            onClick={onInAppLinkClick(onOpenDemoPanel)}
            className="inline-flex items-center gap-1.5 rounded-full px-2 py-1 text-xs font-semibold whitespace-nowrap text-brand hover:underline focus-visible:outline-2 focus-visible:outline-brand"
          >
            <SlidersHorizontal size={14} aria-hidden="true" />
            Demo paneli
          </a>
        )}
      </div>
    </header>
  )
}

export default TopBar
