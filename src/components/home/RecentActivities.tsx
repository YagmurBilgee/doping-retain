import { Atom, BookOpen, FileCheck2, Play } from 'lucide-react'
import ItemListSection, { type ListItem } from './ItemListSection'

const RECENT_ACTIVITIES: ListItem[] = [
  { category: 'TYT Kimya › Kimya Bilimi', title: 'Simyadan Kimyaya 1', meta: 'Video', icon: Atom, tint: 'bg-brand text-white' },
  { category: 'TYT Türkçe › Sözcükte Anlam', title: 'Sözcüklerde Çok Anlamlılık ve Anlam İlişkileri', meta: 'Konu Anlatımı', icon: BookOpen, tint: 'bg-risk text-white' },
  { category: 'TYT Deneme', title: '2026 TYT Deneme Sınavı 1', meta: 'Deneme', icon: FileCheck2, tint: 'bg-success text-white' },
]

function RecentActivities() {
  return <ItemListSection title="Son Aktivitelerim" items={RECENT_ACTIVITIES} actionIcon={Play} />
}

export default RecentActivities
