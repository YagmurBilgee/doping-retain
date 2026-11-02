import type { SummaryPoint } from '../types'

/** A demo group: a grade with its own students and topics. */
export type DemoGroup = 'yks' | 'sixth_grade'

export interface Student {
  id: string
  displayName: string
  group: DemoGroup
}

/** GET /api/students: every demo student, YKS first, each group in its own order (the first is its default). */
export interface StudentsResponse {
  students: Student[]
  /** Whether the backend maps the demo reset and editor endpoints (Demo:Enabled) */
  demoEnabled: boolean
}

/** Backend retention-model state of a topic */
export type ReviewStatus = 'insufficient_data' | 'scheduled' | 'due' | 'support_needed'

export type TestType = 'normal_test' | 'reminder_mini_quiz'

/** One completed test with its actual score */
export interface TestResult {
  testType: TestType
  /** ISO 8601 UTC timestamp */
  completedAt: string
  correctCount: number
  totalQuestions: number
}

/** One topic as evaluated by the backend; status and review dates are never recalculated in the UI. */
export interface TopicSummary {
  id: string
  title: string
  category: string
  /** Curriculum unit, shown with titles that repeat across units; null for YKS topics */
  unitName: string | null
  reviewStatus: ReviewStatus
  /** Latest relevant completed test (normal test or mini quiz); null without data */
  latestTest: TestResult | null
  /** Effective next review set by the backend (formula or its scheduling policy), ISO 8601 UTC */
  nextReviewAt: string | null
  /** Completion of the latest reminder + mini quiz review; normal tests never set it */
  lastReviewedAt: string | null
}

/** GET /api/students/{studentId}/topics */
export interface TopicsResponse {
  topics: TopicSummary[]
  /** Due topics in review priority order (support_needed and insufficient_data are not included) */
  dueTopicIds: string[]
  /** Distinct topics with a review completed this week (Monday 00:00 Europe/Istanbul) up to evaluatedAt */
  weeklyReviewedTopicCount: number
  /** Instant the backend evaluated every topic at (ISO 8601 UTC) */
  evaluatedAt: string
}

/** GET /api/students/{studentId}/topics/{id}: a topic with its micro-review content, without quiz answers. */
export interface TopicDetail extends TopicSummary {
  questionCount: number
  summaryPoints: SummaryPoint[]
  evaluatedAt: string
}

/** A question as served before answering: no correct option, no explanation. */
export interface SessionQuestion {
  id: string
  text: string
  options: string[]
}

/** POST /api/students/{studentId}/review-sessions/{id}/answers: feedback for one stored answer. */
export interface AnswerFeedback {
  questionId: string
  selectedIndex: number
  isCorrect: boolean
  correctIndex: number
  explanation: string | null
}

/** POST /api/students/{studentId}/topics/{id}/review-sessions: a started or resumed session. */
export interface ReviewSession {
  sessionId: string
  topicId: string
  startedAt: string
  questions: SessionQuestion[]
  /** Feedback for already answered questions only */
  answers: AnswerFeedback[]
}

/** The reviewed topic after completion. */
export interface CompletedTopic {
  id: string
  title: string
  category: string
  lastReviewedAt: string
}


/** POST /api/students/{studentId}/review-sessions/{id}/complete: the stored outcome, identical on every retry. */
export interface ReviewCompletion {
  sessionId: string
  score: number
  total: number
  updatedTopic: CompletedTopic
  completedAt: string
  /** The backend's next review after this quiz; completedAt when support is needed now */
  nextReviewAt: string
  reviewStatus: ReviewStatus
  testResult: TestResult
}

/** One topic in a day of the weekly summary: the day's completions of it, merged into one row. */
export interface WeeklySummaryTopic {
  topicId: string
  title: string
  category: string
  unitName: string | null
  /** Completed review sessions of this topic that day */
  reviewCount: number
  /** The day's latest completion (ISO 8601 UTC) */
  lastCompletedAt: string
  /** Score of that latest completion; null only for a session without a stored result */
  correctCount: number | null
  totalQuestions: number | null
}

/** One Istanbul calendar day of the current week. */
export interface WeeklySummaryDay {
  /** yyyy-MM-dd */
  date: string
  /** Distinct topics completed that day */
  distinctTopicCount: number
  topics: WeeklySummaryTopic[]
}

/** GET /api/students/{studentId}/weekly-summary: the current Monday–Sunday week (Europe/Istanbul), counted by the backend. */
export interface WeeklySummary {
  firstDate: string
  lastDate: string
  today: string
  /** Distinct topics over the whole week, the same number as the homepage counter */
  distinctTopicCount: number
  completedReviewCount: number
  activeDayCount: number
  /** Monday to Sunday */
  days: WeeklySummaryDay[]
  evaluatedAt: string
}

/** One topic row of the demo panel: the topic as the student screen evaluates it, plus its latest normal test. */
export interface DemoTopicRow {
  topic: TopicSummary
  /** The student's latest normal test of the topic; null without one */
  latestNormalTest: TestResult | null
}

/** GET /api/demo/sixth-grade: the five students with their eight topics, evaluated at one instant. */
export interface DemoGroupResponse {
  students: { id: string; displayName: string; topics: DemoTopicRow[] }[]
  evaluatedAt: string
}

/** A normal-test question for the demonstrator, with its answer key. */
export interface DemoNormalQuestion {
  id: string
  text: string
  options: string[]
  correctIndex: number
}

/** GET /api/demo/students/{studentId}/topics/{topicId}/normal-test */
export interface DemoNormalTest {
  studentId: string
  topicId: string
  title: string
  category: string
  unitName: string | null
  questions: DemoNormalQuestion[]
  /** The latest normal test; null without one. Answers are empty when it was recorded without them. */
  saved: {
    completedAt: string
    correctCount: number
    totalQuestions: number
    answers: { questionId: string; selectedIndex: number; isCorrect: boolean }[]
  } | null
}

export type LoadState<T> =
  | { status: 'loading' }
  | { status: 'error'; message: string }
  | { status: 'ready'; data: T }
