import type { SummaryPoint } from '../types'

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
  reviewStatus: ReviewStatus
  /** Latest relevant completed test (normal test or mini quiz); null without data */
  latestTest: TestResult | null
  /** When estimated recall reaches the review threshold (ISO 8601 UTC) */
  nextReviewAt: string | null
  /** Completion of the latest reminder + mini quiz review; normal tests never set it */
  lastReviewedAt: string | null
}

/** GET /api/topics */
export interface TopicsResponse {
  topics: TopicSummary[]
  /** Due topics in review priority order (support_needed and insufficient_data are not included) */
  dueTopicIds: string[]
  /** Distinct topics with a review completed this week (Monday 00:00 Europe/Istanbul) up to evaluatedAt */
  weeklyReviewedTopicCount: number
  /** Instant the backend evaluated every topic at (ISO 8601 UTC) */
  evaluatedAt: string
}

/** GET /api/topics/{id}: a topic with its micro-review content, without quiz answers. */
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

/** POST /api/review-sessions/{id}/answers: feedback for one stored answer. */
export interface AnswerFeedback {
  questionId: string
  selectedIndex: number
  isCorrect: boolean
  correctIndex: number
  explanation: string | null
}

/** POST /api/topics/{id}/review-sessions: a started or resumed session. */
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


/** POST /api/review-sessions/{id}/complete: the stored outcome, identical on every retry. */
export interface ReviewCompletion {
  sessionId: string
  score: number
  total: number
  updatedTopic: CompletedTopic
  completedAt: string
  /** Where this quiz's curve reaches the threshold; completedAt when support is needed now */
  nextReviewAt: string
  reviewStatus: ReviewStatus
  testResult: TestResult
}

export type LoadState<T> =
  | { status: 'loading' }
  | { status: 'error'; message: string }
  | { status: 'ready'; data: T }
