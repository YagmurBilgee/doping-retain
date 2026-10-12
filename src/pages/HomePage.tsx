import { useCallback, useState } from 'react'
import DemoResetButton from '../components/DemoResetButton'
import MemoryRiskCard from '../components/MemoryRiskCard'
import MiniQuizModal from '../components/MiniQuizModal'
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
import { mockTopics } from '../data/mockTopics'
import type { ReviewOutcome } from '../types'
import { countHighRiskTopics, findHighestRiskTopic } from '../utils/memoryModel'

function HomePage() {
  const [topics, setTopics] = useState(mockTopics)
  const [pendingReviewTopicId, setPendingReviewTopicId] = useState<string | null>(null)
  const [quizTopicId, setQuizTopicId] = useState<string | null>(null)
  const [lastReview, setLastReview] = useState<ReviewOutcome>()

  const riskTopic = findHighestRiskTopic(topics)
  const pendingReviewTopic = topics.find((topic) => topic.id === pendingReviewTopicId)
  const quizTopic = topics.find((topic) => topic.id === quizTopicId)

  const handleStartQuiz = (topicId: string) => {
    setPendingReviewTopicId(null)
    setQuizTopicId(topicId)
  }

  const handleQuizComplete = (outcome: ReviewOutcome) => {
    const { updatedTopic } = outcome
    setTopics((current) => current.map((topic) => (topic.id === updatedTopic.id ? updatedTopic : topic)))
    setLastReview(outcome)
    setQuizTopicId(null)
  }

  // Restores the initial high-risk demo state, closing any open modal.
  const resetDemo = useCallback(() => {
    setTopics(mockTopics)
    setLastReview(undefined)
    setPendingReviewTopicId(null)
    setQuizTopicId(null)
  }, [])

  return (
    <div className="flex flex-1">
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
              topic={riskTopic}
              highRiskCount={countHighRiskTopics(topics)}
              lastReview={lastReview}
              onStartReview={setPendingReviewTopicId}
            />
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

      {quizTopic && (
        <MiniQuizModal
          key={quizTopic.id}
          topic={quizTopic}
          onClose={() => setQuizTopicId(null)}
          onComplete={handleQuizComplete}
        />
      )}

      <DemoResetButton onReset={resetDemo} />
    </div>
  )
}

export default HomePage
