import { BookOpen, ChevronRight, FlaskConical, Mountain, Sigma } from 'lucide-react'
import type { DemoGroup } from '../../api/types'
import ItemListSection, { type ListItem } from './ItemListSection'

const YKS_PROGRAM: ListItem[] = [
  { category: 'TYT Refleksler Matematik', title: 'Temel Bilgiler', meta: '4 saat 13 dakika', icon: Sigma, tint: 'bg-brand text-white' },
  { category: 'TYT Refleksler Matematik', title: 'Sayıların Sınıflandırılması', meta: '1 saat 10 dakika', icon: Sigma, tint: 'bg-brand text-white' },
  { category: 'TYT Refleksler Geometri', title: 'Doğruda Açı', meta: '45 dakika', icon: Mountain, tint: 'bg-success text-white' },
  { category: 'TYT Odaklı Konu Kampı', title: 'Sözcükte Çok Anlamlılık', meta: '32 dakika', icon: BookOpen, tint: 'bg-risk text-white' },
]

// Sample plan from the sixth-grade catalog (course and topic names), not a student's own program.
const SIXTH_GRADE_PROGRAM: ListItem[] = [
  { category: '6. Sınıf Reflekslerle Matematik', title: 'Katlar', meta: '35 dakika', icon: Sigma, tint: 'bg-brand text-white' },
  { category: '6. Sınıf Reflekslerle Matematik', title: 'Kalansız Bölünebilme', meta: '40 dakika', icon: Sigma, tint: 'bg-brand text-white' },
  { category: '6. Sınıf Serüvenlerle Fen Bilimleri', title: 'Bileşke Kuvvet', meta: '30 dakika', icon: FlaskConical, tint: 'bg-success text-white' },
  { category: '6. Sınıf Türkçe', title: 'Paragrafta Soru', meta: '25 dakika', icon: BookOpen, tint: 'bg-risk text-white' },
]

function DailyProgram({ group }: { group: DemoGroup }) {
  const isSixthGrade = group === 'sixth_grade'
  return (
    <ItemListSection
      title="Günün Programı"
      subtitle={`Tahmini Kalan Çalışma Süresi: ${isSixthGrade ? '2 Saat 10 dakika' : '7 Saat 20 dakika'}`}
      items={isSixthGrade ? SIXTH_GRADE_PROGRAM : YKS_PROGRAM}
      actionIcon={ChevronRight}
    />
  )
}

export default DailyProgram
