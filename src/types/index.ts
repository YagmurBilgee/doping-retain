export type RiskLevel = 'safe' | 'review_soon' | 'high_risk'

export interface Topic {
  id: string
  title: string
  category: string
  lastStudiedDaysAgo: number
  /** 0–1 ratio of correct answers from previous reviews */
  previousSuccessRate: number
  reviewCount: number
  /** 0–100 estimated memory strength */
  currentStrength: number
}

export interface QuizQuestion {
  id: string
  question: string
  options: string[]
  correctIndex: number
}
