import type { ReviewOutcome, ReviewResult, ReviewTier, RiskLevel, Topic, TopicRisk } from '../types'

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

const REVIEW_RULES: Record<ReviewTier, { strengthMultiplier: number; nextReviewInDays: number }> = {
  perfect: { strengthMultiplier: 2, nextReviewInDays: 14 },
  partial: { strengthMultiplier: 1.4, nextReviewInDays: 7 },
  weak: { strengthMultiplier: 1.05, nextReviewInDays: 2 },
}

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

/**
 * Convenience wrapper so UI components never touch the formula directly.
 * A topic flagged for follow-up (weak last review) is never reported as safe:
 * it stays in review_soon until a stronger review clears the flag.
 */
export function getTopicRisk(topic: Topic): TopicRisk {
  const retention = calculateRetention(topic.lastStudiedDaysAgo, topic.currentStrength)
  const curveLevel = getRiskLevel(retention)
  const isFollowUp = topic.needsFollowUp === true && curveLevel === 'safe'

  return { retention, riskLevel: isFollowUp ? 'review_soon' : curveLevel, isFollowUp }
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
  const ratio = getScoreRatio(quizScore, totalQuestions)
  const tier: ReviewTier = ratio === 1 ? 'perfect' : ratio >= 2 / 3 ? 'partial' : 'weak'
  const rule = REVIEW_RULES[tier]

  return {
    newStrength: Math.round(currentStrength * rule.strengthMultiplier * 10) / 10,
    nextReviewInDays: rule.nextReviewInDays,
    tier,
  }
}

/**
 * Applies a finished micro-review to a topic: stability grows per the review
 * rules, the topic counts as studied today (t = 0), the success rate becomes
 * a running average across reviews, and a weak score flags it for follow-up.
 */
export function applyReviewResult(topic: Topic, quizScore: number, totalQuestions: number): ReviewOutcome {
  const review = calculateNextReview(quizScore, totalQuestions, topic.currentStrength)
  const ratio = getScoreRatio(quizScore, totalQuestions)
  const reviewCount = topic.reviewCount + 1
  const previousSuccessRate =
    Math.round(((topic.previousSuccessRate * topic.reviewCount + ratio) / reviewCount) * 100) / 100

  return {
    updatedTopic: {
      ...topic,
      currentStrength: review.newStrength,
      lastStudiedDaysAgo: 0,
      reviewCount,
      previousSuccessRate,
      needsFollowUp: review.tier === 'weak',
    },
    review,
    retentionAtNextReview: calculateRetention(review.nextReviewInDays, review.newStrength),
    retentionGain: calculateRetention(0, review.newStrength) - calculateRetention(topic.lastStudiedDaysAgo, topic.currentStrength),
  }
}

/** The high_risk topic with the lowest retention, i.e. the one to surface first. */
export function findHighestRiskTopic(topics: Topic[]): Topic | undefined {
  return topics
    .map((topic) => ({ topic, ...getTopicRisk(topic) }))
    .filter(({ riskLevel }) => riskLevel === 'high_risk')
    .sort((a, b) => a.retention - b.retention)[0]?.topic
}

export function countHighRiskTopics(topics: Topic[]): number {
  return topics.filter((topic) => getTopicRisk(topic).riskLevel === 'high_risk').length
}

function getScoreRatio(quizScore: number, totalQuestions: number): number {
  return totalQuestions > 0 ? Math.min(1, Math.max(0, quizScore / totalQuestions)) : 0
}
