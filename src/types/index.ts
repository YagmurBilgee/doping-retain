export type RiskLevel = 'safe' | 'review_soon' | 'high_risk'

export interface QuizQuestion {
  id: string
  question: string
  options: string[]
  correctIndex: number
}

export interface Topic {
  id: string
  title: string
  category: string
  lastStudiedDaysAgo: number
  /** 0–1 ratio of correct answers from previous reviews */
  previousSuccessRate: number
  reviewCount: number
  /** Memory stability S in days, used in Retention = e^(-t / S) */
  currentStrength: number
  /** Key recall points shown before the micro-review quiz */
  summaryPoints?: string[]
  /** Mini-quiz for the micro-review flow */
  quiz?: QuizQuestion[]
  /** Set after a weak review: keeps the topic out of the safe band until reviewed again */
  needsFollowUp?: boolean
}

export interface TopicRisk {
  /** Integer retention percentage (0–100) */
  retention: number
  riskLevel: RiskLevel
  /** True when the curve says safe but a weak last review holds it in review_soon */
  isFollowUp: boolean
}

/** Score band of a micro-review: 3/3, at least 2/3, or below */
export type ReviewTier = 'perfect' | 'partial' | 'weak'

export interface ReviewResult {
  /** Updated memory stability S in days */
  newStrength: number
  nextReviewInDays: number
  tier: ReviewTier
}

export interface ReviewOutcome {
  /** Topic after the review: new stability, studied today, review counted */
  updatedTopic: Topic
  review: ReviewResult
  /** Predicted retention on the day of the next scheduled review */
  retentionAtNextReview: number
  /** Retention points gained by the review (after minus before) */
  retentionGain: number
}
