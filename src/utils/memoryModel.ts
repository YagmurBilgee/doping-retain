import type { ReviewOutcome, ReviewResult, ReviewTier, RiskLevel, Topic, TopicRisk } from '../types'

/*
 * Rule-based MVP memory model built on the Ebbinghaus forgetting curve:
 *   Retention = R0 · e^(-t / S)
 *   t  = days since last study, S = memory stability in days,
 *   R0 = initial recall right after that study session (1 by default; after a
 *        micro-review it reflects the quiz score, so a weak review does not
 *        pretend the topic is fully remembered).
 *
 * The thresholds, multipliers and recall targets below are hand-picked MVP
 * approximations, not values fitted to real student data. They are meant to be
 * replaced by a calibrated model (e.g. SM-2 / FSRS style) once review history exists.
 */

export const SAFE_THRESHOLD = 70
export const HIGH_RISK_THRESHOLD = 50

/**
 * Product rule: a topic with no completed micro-review whose R0 (its first-test accuracy) is below
 * this value needs initial reinforcement. Its low retention reflects low initial mastery, not forgetting.
 */
export const INITIAL_REINFORCEMENT_RECALL = 0.7

interface ReviewRule {
  strengthMultiplier: number
  nextReviewInDays: number
  /** Initial recall R0 right after the review */
  postReviewRecall: number
}

const REVIEW_RULES: Record<ReviewTier, ReviewRule> = {
  perfect: { strengthMultiplier: 2, nextReviewInDays: 14, postReviewRecall: 1 },
  partial: { strengthMultiplier: 1.4, nextReviewInDays: 7, postReviewRecall: 0.8 },
  weak: { strengthMultiplier: 1.05, nextReviewInDays: 2, postReviewRecall: 0.58 },
}

/** Unrounded retention percentage (0–100); every business decision uses this value. */
function calculateRawRetention(daysAgo: number, strength: number, initialRecall = 1): number {
  if (strength <= 0) return 0

  const t = Math.max(0, daysAgo)
  const retention = initialRecall * Math.exp(-t / strength) * 100

  return Math.min(100, Math.max(0, retention))
}

/** Returns retention as an integer percentage (0–100), for display only. */
export function calculateRetention(daysAgo: number, strength: number, initialRecall = 1): number {
  return Math.round(calculateRawRetention(daysAgo, strength, initialRecall))
}

function getRawTopicRetention(topic: Topic): number {
  return calculateRawRetention(topic.lastStudiedDaysAgo, topic.currentStrength, topic.initialRecall)
}

/** Classifies an unrounded retention percentage; pass raw values, not display values. */
export function getRiskLevel(retentionPercentage: number): RiskLevel {
  if (retentionPercentage >= SAFE_THRESHOLD) return 'safe'
  if (retentionPercentage >= HIGH_RISK_THRESHOLD) return 'review_soon'
  return 'high_risk'
}

function needsInitialReinforcement(topic: Topic): boolean {
  return topic.reviewCount === 0 && (topic.initialRecall ?? 1) < INITIAL_REINFORCEMENT_RECALL
}

/**
 * Convenience wrapper so UI components never touch the formula directly.
 * A topic flagged for follow-up (weak last review) is never reported as safe:
 * it is held in review_soon until a stronger review clears the flag, and only
 * falls to high_risk if the curve itself drops that far.
 * Retention and risk level are unchanged for topics that need initial reinforcement; the flag only
 * keeps them out of the forgetting signals (highest-risk topic, high-risk count).
 */
export function getTopicRisk(topic: Topic): TopicRisk {
  const rawRetention = getRawTopicRetention(topic)
  const curveLevel = getRiskLevel(rawRetention)
  const isFollowUp = topic.needsFollowUp === true && curveLevel !== 'high_risk'

  return {
    retention: Math.round(rawRetention),
    riskLevel: isFollowUp ? 'review_soon' : curveLevel,
    isFollowUp,
    needsInitialReinforcement: needsInitialReinforcement(topic),
  }
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
 * rules, the topic counts as studied today (t = 0) with an initial recall set by
 * the score, the success rate becomes a running average across reviews, and a
 * weak score flags it for follow-up.
 */
export function applyReviewResult(topic: Topic, quizScore: number, totalQuestions: number): ReviewOutcome {
  const review = calculateNextReview(quizScore, totalQuestions, topic.currentStrength)
  const ratio = getScoreRatio(quizScore, totalQuestions)
  const reviewCount = topic.reviewCount + 1
  const previousSuccessRate =
    Math.round(((topic.previousSuccessRate * topic.reviewCount + ratio) / reviewCount) * 100) / 100

  // A review never leaves the student worse off than before it.
  const initialRecall = Math.max(REVIEW_RULES[review.tier].postReviewRecall, getRawTopicRetention(topic) / 100)

  const updatedTopic: Topic = {
    ...topic,
    currentStrength: review.newStrength,
    lastStudiedDaysAgo: 0,
    initialRecall,
    reviewCount,
    previousSuccessRate,
    needsFollowUp: review.tier === 'weak',
  }

  return {
    updatedTopic,
    review,
    retentionAtNextReview: calculateRetention(review.nextReviewInDays, review.newStrength, initialRecall),
    retentionGain: getTopicRisk(updatedTopic).retention - getTopicRisk(topic).retention,
  }
}

/** A high_risk topic that is being forgotten; topics needing initial reinforcement are not. */
function isForgettingHighRisk(risk: TopicRisk): boolean {
  return risk.riskLevel === 'high_risk' && !risk.needsInitialReinforcement
}

/** The forgetting high_risk topic with the lowest retention, i.e. the one to surface first. */
export function findHighestRiskTopic(topics: Topic[]): Topic | undefined {
  return topics
    .map((topic) => ({ topic, risk: getTopicRisk(topic), rawRetention: getRawTopicRetention(topic) }))
    .filter(({ risk }) => isForgettingHighRisk(risk))
    .sort((a, b) => a.rawRetention - b.rawRetention)[0]?.topic
}

export function countHighRiskTopics(topics: Topic[]): number {
  return topics.filter((topic) => isForgettingHighRisk(getTopicRisk(topic))).length
}

function getScoreRatio(quizScore: number, totalQuestions: number): number {
  return totalQuestions > 0 ? Math.min(1, Math.max(0, quizScore / totalQuestions)) : 0
}
