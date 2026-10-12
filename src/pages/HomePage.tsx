import { useState } from 'react'
import {
  Atom,
  BookOpen,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  Coins,
  FileCheck2,
  Flame,
  Mountain,
  Play,
  Sigma,
  Trophy,
  UserRound,
  type LucideIcon,
} from 'lucide-react'
import MemoryRiskCard from '../components/MemoryRiskCard'
import Sidebar from '../components/Sidebar'
import TopBar from '../components/TopBar'
import { featuredTopic, mockTopics } from '../data/mockTopics'
import { getTopicRisk } from '../utils/memoryModel'

/* Static shell content mirroring the reference screens; not part of the Retain model. */

const HIGH_RISK_COUNT = mockTopics.filter((topic) => getTopicRisk(topic).riskLevel === 'high_risk').length

const WEEK_DAYS = [
  { day: 'Pzt', date: '05' },
  { day: 'Sal', date: '06' },
  { day: 'Çar', date: '07' },
  { day: 'Per', date: '08' },
  { day: 'Cum', date: '09' },
  { day: 'Cmt', date: '10' },
  { day: 'Paz', date: '11', isToday: true },
]

interface ListItem {
  category: string
  title: string
  meta: string
  icon: LucideIcon
  tint: string
}

const DAILY_PROGRAM: ListItem[] = [
  { category: 'TYT Refleksler Matematik', title: 'Temel Bilgiler', meta: '4 saat 13 dakika', icon: Sigma, tint: 'bg-brand text-white' },
  { category: 'TYT Refleksler Matematik', title: 'Sayıların Sınıflandırılması', meta: '1 saat 10 dakika', icon: Sigma, tint: 'bg-brand text-white' },
  { category: 'TYT Refleksler Geometri', title: 'Doğruda Açı', meta: '45 dakika', icon: Mountain, tint: 'bg-success text-white' },
  { category: 'TYT Odaklı Konu Kampı', title: 'Sözcükte Çok Anlamlılık', meta: '32 dakika', icon: BookOpen, tint: 'bg-risk text-white' },
]

const RECENT_ACTIVITIES: ListItem[] = [
  { category: 'TYT Kimya › Kimya Bilimi', title: 'Simyadan Kimyaya 1', meta: 'Video', icon: Atom, tint: 'bg-brand text-white' },
  { category: 'TYT Türkçe › Sözcükte Anlam', title: 'Sözcüklerde Çok Anlamlılık ve Anlam İlişkileri', meta: 'Konu Anlatımı', icon: BookOpen, tint: 'bg-risk text-white' },
  { category: 'TYT Deneme', title: '2026 TYT Deneme Sınavı 1', meta: 'Deneme', icon: FileCheck2, tint: 'bg-success text-white' },
]

const CHART_LABELS = ['03 Eki', '04 Eki', '05 Eki', '06 Eki', '07 Eki', '08 Eki', '09 Eki', '10 Eki', '11 Eki']

function ProfileBanner() {
  return (
    <section className="card-dark flex flex-1 items-center gap-5">
      <span className="flex size-20 shrink-0 items-center justify-center rounded-card bg-risk-soft text-risk-ink" aria-hidden="true">
        <UserRound size={40} />
      </span>
      <div className="min-w-0 flex-1">
        <div className="flex items-center justify-between gap-3">
          <h1 className="truncate text-2xl font-bold">yagmur akkose</h1>
          <span className="flex items-center gap-1 text-sm font-semibold">
            <Flame size={16} className="text-coin" aria-hidden="true" />1
          </span>
        </div>
        <div className="mt-3 flex flex-wrap items-center gap-2">
          <span className="metric-pill">
            <Coins size={16} className="text-coin" aria-hidden="true" />
            615
          </span>
          {['Başlangıç', 'İlk Video', 'Haftalık Seri'].map((label) => (
            <span key={label} className="badge bg-white/10 text-white">
              <Trophy size={12} aria-hidden="true" />
              {label}
            </span>
          ))}
        </div>
      </div>
    </section>
  )
}

function DailyTasksCard() {
  return (
    <section className="card-brand flex w-full flex-col items-center justify-center gap-2 text-center sm:w-48">
      <h2 className="text-sm font-semibold">Günlük Görevlerim</h2>
      <span className="flex size-14 items-center justify-center rounded-full bg-white text-xl font-extrabold text-brand">
        50
      </span>
      <p className="text-xs text-white/80">5 video izle ve 50 puan kazan</p>
    </section>
  )
}

function PerformanceChart() {
  return (
    <section>
      <div className="mb-4 flex items-center justify-between">
        <h2 className="section-title">Günlük Çalışma Performansı</h2>
        <button type="button" className="btn-secondary bg-surface px-3 py-1.5 text-xs shadow-card">
          Video İzleme
          <ChevronDown size={14} aria-hidden="true" />
        </button>
      </div>
      <div className="card">
        <svg viewBox="0 0 600 180" preserveAspectRatio="none" className="h-44 w-full text-brand" role="img" aria-label="Günlük çalışma süresi grafiği (örnek)">
          {[20, 60, 100, 140].map((y) => (
            <line key={y} x1="0" x2="600" y1={y} y2={y} className="stroke-line" strokeDasharray="4 6" vectorEffect="non-scaling-stroke" />
          ))}
          <path
            d="M0 120 C40 60 70 150 110 110 S180 40 220 90 S300 150 340 100 S410 30 450 80 S530 140 600 60"
            fill="none"
            stroke="currentColor"
            strokeWidth="2.5"
            strokeLinecap="round"
            vectorEffect="non-scaling-stroke"
          />
        </svg>
        <div className="mt-2 flex justify-between text-xs text-ink-muted">
          {CHART_LABELS.map((label) => (
            <span key={label}>{label}</span>
          ))}
        </div>
      </div>
    </section>
  )
}

function ListSection({ title, subtitle, items, actionIcon: ActionIcon }: {
  title: string
  subtitle?: string
  items: ListItem[]
  actionIcon: LucideIcon
}) {
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

function JourneyCard() {
  return (
    <section className="card-dark">
      <h2 className="text-lg font-bold">YKS Yolculuğum!</h2>
      <p className="mt-1 text-sm text-white/70">Hedefine ulaşman için sana özel hazırlanmış bir yolculuk.</p>
      <button type="button" className="btn-primary mt-4">
        Yolculuğa Git
      </button>
    </section>
  )
}

function MiniCalendar() {
  return (
    <section>
      <div className="flex items-center justify-between">
        <h2 className="section-title">Ekim 2026</h2>
        <div className="flex items-center gap-1 text-ink-secondary">
          <button type="button" aria-label="Önceki hafta" className="rounded-full p-1 hover:bg-surface">
            <ChevronLeft size={18} />
          </button>
          <button type="button" className="btn-secondary bg-surface px-3 py-1 text-xs">
            Bugün
          </button>
          <button type="button" aria-label="Sonraki hafta" className="rounded-full p-1 hover:bg-surface">
            <ChevronRight size={18} />
          </button>
        </div>
      </div>
      <ol className="mt-3 grid grid-cols-7 gap-1 text-center">
        {WEEK_DAYS.map(({ day, date, isToday }) => (
          <li key={date} className="flex flex-col items-center gap-1">
            <span className="text-xs text-ink-muted">{day}</span>
            <span
              className={`flex size-9 items-center justify-center rounded-full text-sm font-semibold ${
                isToday ? 'bg-danger text-white' : 'text-ink'
              }`}
              aria-current={isToday ? 'date' : undefined}
            >
              {date}
            </span>
          </li>
        ))}
      </ol>
    </section>
  )
}

function HomePage() {
  // Phase 6 will open the micro-review modal for this topic.
  const [pendingReviewTopicId, setPendingReviewTopicId] = useState<string | null>(null)

  const handleStartReview = () => {
    console.info('[Doping Retain] Micro-review requested:', featuredTopic.id)
    setPendingReviewTopicId(featuredTopic.id)
  }

  return (
    <div className="flex min-h-screen">
      <Sidebar />

      <div className="flex min-w-0 flex-1 flex-col gap-6 p-4 sm:p-6">
        <TopBar />

        <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_360px]">
          <main className="flex min-w-0 flex-col gap-6">
            <div className="flex flex-col gap-4 sm:flex-row">
              <ProfileBanner />
              <DailyTasksCard />
            </div>
            <PerformanceChart />
            <ListSection title="Son Aktivitelerim" items={RECENT_ACTIVITIES} actionIcon={Play} />
          </main>

          <aside className="flex flex-col gap-6" aria-label="Günlük plan">
            <JourneyCard />
            <MiniCalendar />
            <MemoryRiskCard
              topic={featuredTopic}
              highRiskCount={HIGH_RISK_COUNT}
              onStartReview={handleStartReview}
            />
            {pendingReviewTopicId && (
              <p className="badge badge-brand self-start" role="status">
                Tekrar oturumu hazırlanıyor…
              </p>
            )}
            <ListSection
              title="Günün Programı"
              subtitle="Tahmini Kalan Çalışma Süresi: 7 Saat 20 dakika"
              items={DAILY_PROGRAM}
              actionIcon={ChevronRight}
            />
          </aside>
        </div>
      </div>
    </div>
  )
}

export default HomePage
