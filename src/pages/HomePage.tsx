import { useCallback, useEffect, useRef, useState } from 'react'
import { errorMessage, getTopics, resetDemo } from '../api/client'
import type { LoadState, ReviewSession, TopicsResponse } from '../api/types'
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

function HomePage() {
  const [topics, setTopics] = useState<LoadState<TopicsResponse>>({ status: 'loading' })
  // Modals remember the reset epoch they opened in; their results are dropped once a reset has started.
  const [detail, setDetail] = useState<{ topicId: string; epoch: number } | null>(null)
  const [quiz, setQuiz] = useState<{ session: ReviewSession; topicTitle: string; epoch: number } | null>(null)
  const [isResetting, setIsResetting] = useState(false)
  const [resetError, setResetError] = useState<string>()

  // Each topics request takes a new generation; only the latest may update the UI, so a response
  // that was in flight before a reset can never bring back pre-reset values.
  const topicsGeneration = useRef(0)
  const resetInFlight = useRef(false)
  const resetEpoch = useRef(0)
  const isCurrentEpoch = (epoch: number) => !resetInFlight.current && epoch === resetEpoch.current

  const loadTopics = useCallback(() => {
    const generation = ++topicsGeneration.current
    const isLatest = () => generation === topicsGeneration.current
    getTopics().then(
      (data) => isLatest() && setTopics({ status: 'ready', data }),
      (error: unknown) => isLatest() && setTopics({ status: 'error', message: errorMessage(error) }),
    )
  }, [])

  useEffect(loadTopics, [loadTopics])

  const reloadTopics = useCallback(() => {
    setTopics({ status: 'loading' })
    loadTopics()
  }, [loadTopics])

  const openDetail = (topicId: string) => setDetail({ topicId, epoch: resetEpoch.current })

  const handleSessionStarted = (session: ReviewSession, topicTitle: string) => {
    if (!detail || !isCurrentEpoch(detail.epoch)) return
    setDetail(null)
    setQuiz({ session, topicTitle, epoch: detail.epoch })
  }

  // Without a confirmed outcome the server may or may not have stored the review: show its state, not a guess.
  const handleQuizClosed = (completionUncertain: boolean) => {
    if (quiz && completionUncertain && isCurrentEpoch(quiz.epoch)) reloadTopics()
    setQuiz(null)
  }

  // The server stored the review: reload the evaluated topics, so the due list and weekly count follow it.
  const handleReviewCompleted = () => {
    if (!quiz || !isCurrentEpoch(quiz.epoch)) return
    reloadTopics()
  }

  // Resets the demo student on the backend, then closes any modal and reloads the topics.
  const handleResetDemo = useCallback(async () => {
    if (resetInFlight.current) return
    resetInFlight.current = true
    setIsResetting(true)
    setResetError(undefined)
    topicsGeneration.current++

    try {
      await resetDemo()
      resetEpoch.current++
      setDetail(null)
      setQuiz(null)
    } catch (error) {
      setResetError(errorMessage(error))
    } finally {
      resetInFlight.current = false
      setIsResetting(false)
      // Reload either way: a superseded topics request was discarded above.
      reloadTopics()
    }
  }, [reloadTopics])

  return (
    <div className="flex flex-1">
      <Sidebar />

      <div className="flex min-w-0 flex-1 flex-col gap-5 px-4 py-6 sm:px-7 sm:pt-9">
        <TopBar />

        <div className="grid gap-7 xl:grid-cols-[minmax(0,1fr)_344px]">
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
            <MemoryRiskCard topics={topics} onRetry={reloadTopics} onStartReview={openDetail} />
            <DailyProgram />
            {import.meta.env.DEV && (
              <DemoResetButton onReset={() => void handleResetDemo()} isResetting={isResetting} error={resetError} />
            )}
          </aside>
        </div>
      </div>

      {detail && (
        <TopicDetailModal
          key={detail.topicId}
          topicId={detail.topicId}
          onClose={() => setDetail(null)}
          onSessionStarted={handleSessionStarted}
        />
      )}

      {quiz && (
        <MiniQuizModal
          key={quiz.session.sessionId}
          session={quiz.session}
          topicTitle={quiz.topicTitle}
          onClose={handleQuizClosed}
          onCompleted={handleReviewCompleted}
        />
      )}

    </div>
  )
}

export default HomePage
