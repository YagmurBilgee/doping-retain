import type { ReviewResult, RiskLevel, Topic, TopicRisk } from '../types'

/*
 * Rule-based MVP memory model built on the Ebbinghaus forgetting curve:
 *   Retention = e^(-t / S)
 *   t = days since last study, S = memory stability in days.
 *
 * The thresholds and multipliers below are hand-picked MVP approximations,
 * not values fitted to real student data. They are meant to be replaced by
 * a calibrated model (e.g. SM-2 / FSRS style) once review history exists.
 */

export const SAFE_THRESHOLD = 70
export const HIGH_RISK_THRESHOLD = 50

const REVIEW_RULES = {
  perfect: { strengthMultiplier: 2, nextReviewInDays: 14 },
  partial: { strengthMultiplier: 1.4, nextReviewInDays: 7 },
  weak: { strengthMultiplier: 1.05, nextReviewInDays: 2 },
} as const

/** Returns retention as an integer percentage (0–100). */
export function calculateRetention(daysAgo: number, strength: number): number {
  if (strength <= 0) return 0

  const t = Math.max(0, daysAgo)
  const retention = Math.exp(-t / strength) * 100

  return Math.min(100, Math.max(0, Math.round(retention)))
}

export function getRiskLevel(retentionPercentage: number): RiskLevel {
  if (retentionPercentage >= SAFE_THRESHOLD) return 'safe'
  if (retentionPercentage >= HIGH_RISK_THRESHOLD) return 'review_soon'
  return 'high_risk'
}

/** Convenience wrapper so UI components never touch the formula directly. */
export function getTopicRisk(topic: Topic): TopicRisk {
  const retention = calculateRetention(topic.lastStudiedDaysAgo, topic.currentStrength)
  return { retention, riskLevel: getRiskLevel(retention) }
}

/**
 * Updates stability after a micro-review quiz.
 * Rules are defined on the score ratio, so they apply to any quiz length:
 * full score (3/3), at least two thirds (2/3), or below (1/3, 0/3).
 */
export function calculateNextReview(
  quizScore: number,
  totalQuestions: number,
  currentStrength: number,
): ReviewResult {
  const ratio = totalQuestions > 0 ? Math.min(1, Math.max(0, quizScore / totalQuestions)) : 0

  const rule =
    ratio === 1
      ? REVIEW_RULES.perfect
      : ratio >= 2 / 3
        ? REVIEW_RULES.partial
        : REVIEW_RULES.weak

  return {
    newStrength: Math.round(currentStrength * rule.strengthMultiplier * 10) / 10,
    nextReviewInDays: rule.nextReviewInDays,
  }
}
