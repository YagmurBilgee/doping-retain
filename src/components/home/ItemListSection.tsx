import type { LucideIcon } from 'lucide-react'

export interface ListItem {
  category: string
  title: string
  meta: string
  icon: LucideIcon
  tint: string
}

interface ItemListSectionProps {
  title: string
  subtitle?: string
  items: ListItem[]
  actionIcon: LucideIcon
}

/** Shared "section title + list-row card" pattern used across the home screen. */
function ItemListSection({ title, subtitle, items, actionIcon: ActionIcon }: ItemListSectionProps) {
  return (
    <section>
      <h2 className="section-title">{title}</h2>
      {subtitle && <p className="section-subtitle mt-0.5">{subtitle}</p>}
      <ul className="card mt-3 flex flex-col gap-2">
        {items.map(({ category, title: itemTitle, meta, icon: Icon, tint }) => (
          <li key={itemTitle} className="list-row">
            <span className={`icon-tile ${tint}`} aria-hidden="true">
              <Icon size={18} />
            </span>
            <div className="min-w-0 flex-1">
              <p className="section-subtitle truncate">{category}</p>
              <p className="truncate text-sm font-semibold text-ink">{itemTitle}</p>
              <p className="text-xs text-ink-muted">{meta}</p>
            </div>
            <ActionIcon size={18} className="shrink-0 text-ink-secondary" aria-hidden="true" />
          </li>
        ))}
      </ul>
    </section>
  )
}

export default ItemListSection
