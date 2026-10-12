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
}

export interface TopicRisk {
  /** Integer retention percentage (0–100) */
  retention: number
  riskLevel: RiskLevel
}

export interface ReviewResult {
  /** Updated memory stability S in days */
  newStrength: number
  nextReviewInDays: number
}
