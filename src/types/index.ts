export type RiskLevel = 'safe' | 'review_soon' | 'high_risk'

export interface QuizQuestion {
  id: string
  question: string
  options: string[]
  correctIndex: number
  /** One-line solution hint shown after a wrong answer */
  explanation?: string
}

/** A recall bullet in the micro-review, tagged with a Doping-style memory hint (served by GET /api/topics/{id}) */
export interface SummaryPoint {
  /** Short emoji marker, e.g. 📌 */
  icon: string
  /** Memory tag, e.g. "Temel Özdeşlik" */
  tag: string
  /** The formula or fact itself */
  text: string
  /** Optional plain-language reminder */
  note?: string
}

export interface Topic {
  id: string
  title: string
  category: string
  lastStudiedDaysAgo: number
  /** 0–1 ratio of correct answers from previous reviews */
  previousSuccessRate: number
  reviewCount: number
  /** Memory stability S in days, used in Retention = R0 · e^(-t / S) */
  currentStrength: number
  /** Initial recall R0 (0–1) right after the last study session; defaults to 1 */
  initialRecall?: number
  /** Mini-quiz for the micro-review flow */
  quiz?: QuizQuestion[]
  /** Set after a weak review: keeps the topic out of the safe band until reviewed again */
  needsFollowUp?: boolean
}

export interface TopicRisk {
  /** Integer retention percentage (0–100) */
  retention: number
  riskLevel: RiskLevel
  /** True when a weak last review holds the topic in review_soon ("Takipte") */
  isFollowUp: boolean
  /** No completed micro-review and a first-test R0 below 0.70: low initial mastery, not forgetting ("Pekiştirme Gerekli") */
  needsInitialReinforcement: boolean
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
