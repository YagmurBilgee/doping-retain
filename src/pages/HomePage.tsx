import { useState } from 'react'
import MemoryRiskCard from '../components/MemoryRiskCard'
import Sidebar from '../components/Sidebar'
import TopBar from '../components/TopBar'
import TopicDetailModal from '../components/TopicDetailModal'
import DailyProgram from '../components/home/DailyProgram'
import DailyTasksCard from '../components/home/DailyTasksCard'
import JourneyCard from '../components/home/JourneyCard'
import MiniCalendar from '../components/home/MiniCalendar'
import PerformanceChart from '../components/home/PerformanceChart'
import ProfileBanner from '../components/home/ProfileBanner'
import RecentActivities from '../components/home/RecentActivities'
import { featuredTopic, mockTopics } from '../data/mockTopics'
import { getTopicRisk } from '../utils/memoryModel'

const HIGH_RISK_COUNT = mockTopics.filter((topic) => getTopicRisk(topic).riskLevel === 'high_risk').length

function HomePage() {
  const [pendingReviewTopicId, setPendingReviewTopicId] = useState<string | null>(null)
  // Phase 7 will open the quiz modal for this topic.
  const [quizTopicId, setQuizTopicId] = useState<string | null>(null)

  const pendingReviewTopic = mockTopics.find((topic) => topic.id === pendingReviewTopicId)

  const handleStartQuiz = (topicId: string) => {
    console.info('[Doping Retain] Quiz requested:', topicId)
    setPendingReviewTopicId(null)
    setQuizTopicId(topicId)
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
            <RecentActivities />
          </main>

          <aside className="flex flex-col gap-6" aria-label="Günlük plan">
            <JourneyCard />
            <MiniCalendar />
            <MemoryRiskCard
              topic={featuredTopic}
              highRiskCount={HIGH_RISK_COUNT}
              onStartReview={() => setPendingReviewTopicId(featuredTopic.id)}
            />
            {quizTopicId && (
              <p className="badge badge-brand self-start" role="status">
                Pekiştirme testi hazırlanıyor…
              </p>
            )}
            <DailyProgram />
          </aside>
        </div>
      </div>

      <TopicDetailModal
        isOpen={pendingReviewTopicId !== null}
        topic={pendingReviewTopic}
        onClose={() => setPendingReviewTopicId(null)}
        onStartQuiz={handleStartQuiz}
      />
    </div>
  )
}

export default HomePage
