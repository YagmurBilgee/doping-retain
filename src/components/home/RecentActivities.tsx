import { Book, FileCheck, LayoutList, Play, Search, type LucideIcon } from 'lucide-react'
import type { DemoGroup } from '../../api/types'

type ActivityKind = 'exam' | 'test' | 'video'

interface Activity {
  breadcrumb?: string
  title: string
  kind: ActivityKind
}

/* Tile gradient, tile icon and action icon per kind, as in the Doping Hafıza web dashboard. */
const KIND_STYLE: Record<ActivityKind, { tile: string; icon: LucideIcon; action: LucideIcon }> = {
  exam: { tile: 'from-[#6ed392] to-[#86c9a0]', icon: FileCheck, action: Search },
  test: { tile: 'from-[#ed7ac3] to-[#ea83c3]', icon: LayoutList, action: Search },
  video: { tile: 'from-[#e2a867] to-[#d49857]', icon: Book, action: Play },
}

// Frontend demo fixtures: there is no activity history source in the backend yet.
const YKS_ACTIVITIES: Activity[] = [
  { breadcrumb: 'TYT Kimya › Kimya Bilimi', title: 'Simyadan Kimyaya 1', kind: 'video' },
  { breadcrumb: 'TYT Türkçe › Sözcükte Anlam', title: 'Sözcüklerde Çok Anlamlılık ve Anlam İlişkileri', kind: 'video' },
  { breadcrumb: 'TYT Deneme', title: '2026 TYT Deneme Sınavı 1', kind: 'exam' },
]

// Sixth-grade sample items named after catalog materials; no TYT/AYT content in this grade.
const SIXTH_GRADE_ACTIVITIES: Activity[] = [
  { breadcrumb: '6. Sınıf Matematik › Asal Sayılar', title: 'Asal Sayılar ve Asal Çarpanlar Konu Testi 1', kind: 'test' },
  { breadcrumb: '6. Sınıf Fen Bilimleri › Güneş Sistemi', title: 'Gezegenlerimizi Tanıyalım 1', kind: 'video' },
  { breadcrumb: '6. Sınıf Türkçe › Dilimizin Zenginliği', title: 'Gerçek ve Mecaz Anlam', kind: 'video' },
]

function RecentActivities({ group }: { group: DemoGroup }) {
  const activities = group === 'sixth_grade' ? SIXTH_GRADE_ACTIVITIES : YKS_ACTIVITIES
  return (
    <section className="min-w-0">
      <h2 className="panel-title">Son Aktivitelerim</h2>
      {/* Focusable so keyboard users can scroll the list; the row actions are not available yet. */}
      <div
        className="panel-card mt-6 overflow-y-auto focus-visible:outline-2 focus-visible:outline-brand"
        tabIndex={0}
        role="region"
        aria-label="Son aktiviteler"
      >
        <ul className="divide-y divide-[#f0f3f5]">
          {activities.map(({ breadcrumb, title, kind }) => {
            const { tile, icon: Icon, action: ActionIcon } = KIND_STYLE[kind]
            return (
              <li key={title} className="flex items-center gap-2.5 px-3.5 py-3.5">
                <span
                  className={`flex size-[47px] shrink-0 items-center justify-center rounded-[10px] bg-linear-to-b text-white ${tile}`}
                  aria-hidden="true"
                >
                  <Icon size={20} strokeWidth={1.75} />
                </span>
                <div className="min-w-0 flex-1">
                  {breadcrumb && <p className="text-xs leading-tight text-ink-slate">{breadcrumb}</p>}
                  <p className="mt-0.5 text-base leading-snug font-semibold text-[#1e2028]">{title}</p>
                </div>
                <span
                  className="flex size-[31px] shrink-0 items-center justify-center rounded-full bg-field text-[#1e2028]"
                  aria-hidden="true"
                >
                  <ActionIcon size={15} strokeWidth={2.25} fill={ActionIcon === Play ? 'currentColor' : 'none'} />
                </span>
              </li>
            )
          })}
        </ul>
      </div>
    </section>
  )
}

export default RecentActivities
