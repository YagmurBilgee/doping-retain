import { BookOpen, ChevronRight, Mountain, Sigma } from 'lucide-react'
import ItemListSection, { type ListItem } from './ItemListSection'

const DAILY_PROGRAM: ListItem[] = [
  { category: 'TYT Refleksler Matematik', title: 'Temel Bilgiler', meta: '4 saat 13 dakika', icon: Sigma, tint: 'bg-brand text-white' },
  { category: 'TYT Refleksler Matematik', title: 'Sayıların Sınıflandırılması', meta: '1 saat 10 dakika', icon: Sigma, tint: 'bg-brand text-white' },
  { category: 'TYT Refleksler Geometri', title: 'Doğruda Açı', meta: '45 dakika', icon: Mountain, tint: 'bg-success text-white' },
  { category: 'TYT Odaklı Konu Kampı', title: 'Sözcükte Çok Anlamlılık', meta: '32 dakika', icon: BookOpen, tint: 'bg-risk text-white' },
]

function DailyProgram() {
  return (
    <ItemListSection
      title="Günün Programı"
      subtitle="Tahmini Kalan Çalışma Süresi: 7 Saat 20 dakika"
      items={DAILY_PROGRAM}
      actionIcon={ChevronRight}
    />
  )
}

export default DailyProgram
