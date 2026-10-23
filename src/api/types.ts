import type { ReviewResult, RiskLevel, SummaryPoint, TopicRisk } from '../types'

/** One topic as evaluated by the backend; retention and risk are never recalculated in the UI. */
export interface TopicSummary {
  id: string
  title: string
  category: string
  /** Display retention percentage (0–100) */
  retention: number
  riskLevel: RiskLevel
  isFollowUp: boolean
  /** Low first-test accuracy and no completed review: needs initial reinforcement, not a review */
  needsInitialReinforcement: boolean
  /** ISO 8601 UTC timestamp */
  lastStudiedAt: string
  reviewCount: number
  successRate: number
}

/** GET /api/topics */
export interface TopicsResponse {
  topics: TopicSummary[]
  highestRiskTopicId: string | null
  highRiskCount: number
  /** Instant the backend evaluated every topic at (ISO 8601 UTC) */
  evaluatedAt: string
}

/** GET /api/topics/{id}: a topic with its micro-review content, without quiz answers. */
export interface TopicDetail extends TopicSummary {
  questionCount: number
  /** First-test accuracy percentage (a test score, not measured recall); set only with needsInitialReinforcement */
  initialTestAccuracy: number | null
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

/** The reviewed topic after completion, in the frontend Topic shape (memory fields only). */
export interface CompletedTopic {
  id: string
  title: string
  category: string
  lastStudiedDaysAgo: number
  lastStudiedAt: string
  previousSuccessRate: number
  reviewCount: number
  currentStrength: number
  initialRecall: number
  needsFollowUp: boolean
}

/** POST /api/review-sessions/{id}/complete: the stored outcome, identical on every retry. */
export interface ReviewCompletion {
  sessionId: string
  score: number
  total: number
  updatedTopic: CompletedTopic
  review: ReviewResult
  retentionAtNextReview: number
  /** riskAfter.retention − riskBefore.retention */
  retentionGain: number
  riskBefore: TopicRisk
  riskAfter: TopicRisk
  completedAt: string
  nextReviewAt: string
}

export type LoadState<T> =
  | { status: 'loading' }
  | { status: 'error'; message: string }
  | { status: 'ready'; data: T }

const DAY_MS = 24 * 60 * 60 * 1000

/** Whole days between the last study and the backend's evaluation instant, for display. */
export function daysSinceStudied(topic: { lastStudiedAt: string }, evaluatedAt: string): number {
  return Math.max(0, Math.floor((Date.parse(evaluatedAt) - Date.parse(topic.lastStudiedAt)) / DAY_MS))
}
