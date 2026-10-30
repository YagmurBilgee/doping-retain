import { useCallback, useEffect, useRef, useState } from 'react'
import { errorMessage, getTopics, resetDemoGroup } from '../api/client'
import type { DemoGroup, LoadState, ReviewSession, Student, StudentsResponse, TopicsResponse } from '../api/types'
import { DEMO_PANEL_GROUP, gradeOption, urlFor } from '../demoSelection'
import DemoResetButton from '../components/DemoResetButton'
import MemoryRiskCard from '../components/MemoryRiskCard'
import MiniQuizModal from '../components/MiniQuizModal'
import Sidebar from '../components/Sidebar'
import TopBar from '../components/TopBar'
import TopicDetailModal from '../components/TopicDetailModal'
import WeeklySummaryModal from '../components/WeeklySummaryModal'
import Announcements from '../components/home/Announcements'
import DailyProgram from '../components/home/DailyProgram'
import DailyTasksCard from '../components/home/DailyTasksCard'
import JourneyCard from '../components/home/JourneyCard'
import MiniCalendar from '../components/home/MiniCalendar'
import PerformanceChart from '../components/home/PerformanceChart'
import ProfileBanner from '../components/home/ProfileBanner'
import RecentActivities from '../components/home/RecentActivities'

/** Due/no-data topics open the short review; support-needed topics open the reinforcement flow. */
type ReviewFlow = 'review' | 'reinforcement'

interface HomePageProps {
  group: DemoGroup
  /** The selected student; null while the student list loads or failed. Fixed for this mount (App keys by it). */
  student: Student | null
  students: LoadState<StudentsResponse>
  /** Students of the selected grade, for the student selector */
  groupStudents: Student[]
  demoEnabled: boolean
  onSelectGroup: (group: DemoGroup) => void
  onSelectStudent: (studentId: string) => void
  onOpenDemoPanel: () => void
  onRetryStudents: () => void
}

function HomePage({
  group,
  student,
  students,
  groupStudents,
  demoEnabled,
  onSelectGroup,
  onSelectStudent,
  onOpenDemoPanel,
  onRetryStudents,
}: HomePageProps) {
  const studentId = student?.id ?? null
  const [topics, setTopics] = useState<LoadState<TopicsResponse>>({ status: 'loading' })
  // Modals remember the reset epoch they opened in; their results are dropped once a reset has started.
  const [detail, setDetail] = useState<{ topicId: string; flow: ReviewFlow; epoch: number } | null>(null)
  const [quiz, setQuiz] = useState<{ session: ReviewSession; topicTitle: string; flow: ReviewFlow; epoch: number } | null>(null)
  // Mounted per open, so the summary is loaded fresh every time it opens.
  const [isWeeklySummaryOpen, setIsWeeklySummaryOpen] = useState(false)
  const [isResetting, setIsResetting] = useState(false)
  const [resetError, setResetError] = useState<string>()

  // Each topics request takes a new generation; only the latest may update the UI, so a response
  // that was in flight before a reset can never bring back pre-reset values.
  const topicsGeneration = useRef(0)
  // Aborted on unmount, i.e. when another grade or student is selected.
  const topicsRequest = useRef<AbortController | null>(null)
  const resetInFlight = useRef(false)
  const resetEpoch = useRef(0)
  const isCurrentEpoch = (epoch: number) => !resetInFlight.current && epoch === resetEpoch.current

  const loadTopics = useCallback(() => {
    if (!studentId) return
    const generation = ++topicsGeneration.current
    const isLatest = () => generation === topicsGeneration.current
    topicsRequest.current?.abort()
    const controller = new AbortController()
    topicsRequest.current = controller
    getTopics(studentId, controller.signal).then(
      (data) => isLatest() && setTopics({ status: 'ready', data }),
      (error: unknown) => isLatest() && !controller.signal.aborted && setTopics({ status: 'error', message: errorMessage(error) }),
    )
  }, [studentId])

  useEffect(() => {
    loadTopics()
    return () => topicsRequest.current?.abort()
  }, [loadTopics])

  const reloadTopics = useCallback(() => {
    setTopics({ status: 'loading' })
    loadTopics()
  }, [loadTopics])

  const openDetail = (topicId: string, flow: ReviewFlow = 'review') => setDetail({ topicId, flow, epoch: resetEpoch.current })

  const handleSessionStarted = (session: ReviewSession, topicTitle: string) => {
    if (!detail || !isCurrentEpoch(detail.epoch)) return
    setDetail(null)
    setQuiz({ session, topicTitle, flow: detail.flow, epoch: detail.epoch })
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
      // Only the selected grade's demo group is reset.
      await resetDemoGroup(group)
      resetEpoch.current++
      setDetail(null)
      setQuiz(null)
      setIsWeeklySummaryOpen(false)
    } catch (error) {
      setResetError(errorMessage(error))
    } finally {
      resetInFlight.current = false
      setIsResetting(false)
      // Reload either way: a superseded topics request was discarded above.
      reloadTopics()
    }
  }, [group, reloadTopics])

  // Until a student is known, the card shows the student list's state instead of topics.
  const topicsView: LoadState<TopicsResponse> = studentId
    ? topics
    : students.status === 'error'
      ? students
      : students.status === 'ready'
        ? { status: 'error', message: 'Bu sınıf için demo öğrencisi bulunamadı.' }
        : { status: 'loading' }
  const isSixthGrade = group === 'sixth_grade'

  return (
    <div className="flex flex-1">
      <Sidebar hideYksShortcuts={isSixthGrade} />

      <div className="flex min-w-0 flex-1 flex-col gap-6 px-4 py-6 sm:px-5 sm:pt-8">
        <TopBar
          group={group}
          onSelectGroup={onSelectGroup}
          students={groupStudents}
          studentId={studentId}
          onSelectStudent={onSelectStudent}
          demoPanelHref={demoEnabled && group === DEMO_PANEL_GROUP ? urlFor('demo', DEMO_PANEL_GROUP, studentId) : null}
          onOpenDemoPanel={onOpenDemoPanel}
        />

        {/* The right column grows with the viewport but stays wide enough for the Dopingle Hatırla rows. */}
        <div className="grid grid-cols-1 gap-5 xl:grid-cols-[minmax(0,1fr)_clamp(19rem,22vw,22.5rem)]">
          <main className="flex min-w-0 flex-col gap-6">
            <div className="flex flex-col gap-[11px] sm:flex-row">
              <ProfileBanner name={student?.displayName} showDecorativeScores={!isSixthGrade} />
              <DailyTasksCard />
            </div>
            <PerformanceChart hasData={!isSixthGrade} />
            <div className="grid gap-6 md:grid-cols-2">
              <Announcements />
              <RecentActivities group={group} />
            </div>
          </main>

          <aside className="flex min-w-0 flex-col gap-5" aria-label="Günlük plan">
            {!isSixthGrade && <JourneyCard />}
            <MiniCalendar />
            {/* The product's place for the daily program: right below the calendar. */}
            <DailyProgram group={group} />
            <MemoryRiskCard
              topics={topicsView}
              onRetry={studentId ? reloadTopics : onRetryStudents}
              onStartReview={(topicId) => openDetail(topicId)}
              onStartReinforcement={(topicId) => openDetail(topicId, 'reinforcement')}
              onOpenWeeklySummary={() => setIsWeeklySummaryOpen(true)}
            />
            {demoEnabled && (
              <DemoResetButton
                label={`Demoyu Sıfırla (${gradeOption(group).shortLabel})`}
                onReset={() => void handleResetDemo()}
                isResetting={isResetting}
                error={resetError}
              />
            )}
          </aside>
        </div>
      </div>

      {studentId && detail && (
        <TopicDetailModal
          studentId={studentId}
          key={`${detail.flow}:${detail.topicId}`}
          topicId={detail.topicId}
          flow={detail.flow}
          onClose={() => setDetail(null)}
          onSessionStarted={handleSessionStarted}
        />
      )}

      {studentId && isWeeklySummaryOpen && (
        <WeeklySummaryModal studentId={studentId} onClose={() => setIsWeeklySummaryOpen(false)} />
      )}

      {studentId && quiz && (
        <MiniQuizModal
          studentId={studentId}
          key={quiz.session.sessionId}
          session={quiz.session}
          topicTitle={quiz.topicTitle}
          variant={quiz.flow}
          onClose={handleQuizClosed}
          onCompleted={handleReviewCompleted}
        />
      )}

    </div>
  )
}

export default HomePage
