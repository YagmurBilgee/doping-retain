/*
 * Records expected outputs of the frontend memory model (src/utils/memoryModel.ts) so the
 * C# port can be checked against it. Every expected value comes from calling the actual
 * TypeScript functions; nothing is computed here.
 *
 * Regenerate from the repository root after changing the TypeScript model:
 *   node --experimental-strip-types --no-warnings backend/DopingRetain.Api.Tests/Fixtures/generate-memory-model-fixtures.ts
 */
import { writeFileSync } from 'node:fs'
import type { Topic } from '../../../src/types/index.ts'
import { featuredTopic, mockTopics } from '../../../src/data/mockTopics.ts'
import {
  applyReviewResult,
  calculateRetention,
  countHighRiskTopics,
  findHighestRiskTopic,
  getRiskLevel,
  getTopicRisk,
} from '../../../src/utils/memoryModel.ts'

type MemoryFields = Pick<
  Topic,
  'id' | 'lastStudiedDaysAgo' | 'currentStrength' | 'initialRecall' | 'reviewCount' | 'previousSuccessRate' | 'needsFollowUp'
>

const memoryFields = ({ id, lastStudiedDaysAgo, currentStrength, initialRecall, reviewCount, previousSuccessRate, needsFollowUp }: Topic): MemoryFields =>
  ({ id, lastStudiedDaysAgo, currentStrength, initialRecall, reviewCount, previousSuccessRate, needsFollowUp })

const topic = (id: string, overrides: Partial<Topic>): Topic => ({
  id,
  title: id,
  category: 'fixture',
  lastStudiedDaysAgo: 0,
  currentStrength: 10,
  reviewCount: 1,
  previousSuccessRate: 0.5,
  ...overrides,
})

// t = 0 so the raw retention is exactly R0 · 100.
const atRecall = (id: string, initialRecall: number, needsFollowUp?: boolean) => topic(id, { initialRecall, needsFollowUp })

// A topic initialized from a first test: no completed review, S = 14, R0 = test accuracy, t = 0.
const firstTest = (id: string, initialRecall: number) =>
  topic(id, { initialRecall, reviewCount: 0, previousSuccessRate: 0, currentStrength: 14 })

const riskLevels = [69.7, 49.7, 70, 50, 69.5, 49.5, 0, 100].map((retention) => ({
  retention,
  expected: getRiskLevel(retention),
}))

const retentionInputs: [name: string, daysAgo: number, strength: number, initialRecall: number][] = [
  // R0 · 100 lands exactly on a .5 midpoint, where JS and C# default rounding differ.
  ['midpoint 12.5', 0, 10, 0.125],
  ['midpoint 37.5', 0, 10, 0.375],
  ['midpoint 62.5', 0, 10, 0.625],
  ['midpoint 87.5', 0, 10, 0.875],
  ['decay t=11 S=14', 11, 14, 1],
  ['decay t=14 S=28', 14, 28, 1],
  ['decay t=7 S=19.6 R0=0.8', 7, 19.6, 0.8],
  ['decay t=2 S=14.7 R0=0.58', 2, 14.7, 0.58],
  ['zero strength', 5, 0, 1],
  ['negative days clamp', -3, 10, 1],
  ['recall above 1 clamps', 0, 10, 1.2],
]
const retention = retentionInputs.map(([name, daysAgo, strength, initialRecall]) => ({
  name,
  daysAgo,
  strength,
  initialRecall,
  expected: calculateRetention(daysAgo, strength, initialRecall),
}))

const riskTopics = [
  atRecall('raw 69.7', 0.697),
  atRecall('raw 49.7', 0.497),
  atRecall('raw 70', 0.7),
  atRecall('raw 50', 0.5),
  atRecall('midpoint 62.5', 0.625),
  atRecall('follow-up safe curve', 0.9, true),
  atRecall('follow-up review_soon curve', 0.6, true),
  atRecall('follow-up high_risk curve', 0.4, true),
  firstTest('first test 0%', 0),
  firstTest('first test 30%', 0.3),
  firstTest('first test 69%', 0.69),
  firstTest('first test 70%', 0.7),
  firstTest('first test 80%', 0.8),
  topic('no review, default recall', { reviewCount: 0 }),
  atRecall('reviewed once at 30%', 0.3),
  ...mockTopics,
]
const topicRisks = riskTopics.map((riskTopic) => ({
  name: riskTopic.id,
  topic: memoryFields(riskTopic),
  expected: getTopicRisk(riskTopic),
}))

const reviewInputs: [name: string, topic: Topic, score: number, total: number][] = [
  ['Trigonometri 0/3', featuredTopic, 0, 3],
  ['Trigonometri 1/3', featuredTopic, 1, 3],
  ['Trigonometri 2/3', featuredTopic, 2, 3],
  ['Trigonometri 3/3', featuredTopic, 3, 3],
  // S · 2 · 10 = 2.5 → JS rounds the new strength up to 0.3.
  ['strength midpoint', topic('strength midpoint', { currentStrength: 0.125, lastStudiedDaysAgo: 1 }), 3, 3],
  // (0.25 · 1 + 0) / 2 · 100 = 12.5 → JS rounds the success rate up to 0.13.
  ['success rate midpoint', topic('success rate midpoint', { previousSuccessRate: 0.25, lastStudiedDaysAgo: 4 }), 0, 3],
  // Pre-review retention 90% is above the weak recall target, so R0 keeps 0.9.
  ['recall floor', atRecall('recall floor', 0.9), 0, 3],
  // First review of a topic that needed initial reinforcement: weak keeps the follow-up behavior.
  ['first test 30% then 0/3', firstTest('first test 30%', 0.3), 0, 3],
  ['first test 30% then 3/3', firstTest('first test 30%', 0.3), 3, 3],
  ['no questions', featuredTopic, 0, 0],
  ['score above total', featuredTopic, 4, 3],
]
const reviews = reviewInputs.map(([name, reviewTopic, score, total]) => {
  const outcome = applyReviewResult(reviewTopic, score, total)
  return {
    name,
    topic: memoryFields(reviewTopic),
    score,
    total,
    expected: {
      review: outcome.review,
      updatedTopic: memoryFields(outcome.updatedTopic),
      riskAfter: getTopicRisk(outcome.updatedTopic),
      retentionAtNextReview: outcome.retentionAtNextReview,
      retentionGain: outcome.retentionGain,
    },
  }
})

const highestRiskInputs: [name: string, topics: Topic[]][] = [
  ['mock topics', mockTopics],
  // Both display 50%; priority must follow the unrounded value.
  ['rounded tie', [atRecall('raw 49.7', 0.497), atRecall('raw 49.6', 0.496)]],
  ['identical states keep order', [atRecall('first', 0.3), atRecall('second', 0.3)]],
  ['no high risk', [atRecall('safe', 0.9), atRecall('review soon', 0.6)]],
  // Low initial mastery is not forgetting: it never displaces a forgetting topic or counts as high risk.
  ['reinforcement beside forgetting', [firstTest('first test 0%', 0), firstTest('first test 30%', 0.3), atRecall('forgetting', 0.45)]],
  ['mock topics with reinforcement', [firstTest('first test 0%', 0), ...mockTopics]],
  ['only reinforcement', [firstTest('first test 30%', 0.3), firstTest('first test 69%', 0.69)]],
]
const highestRisk = highestRiskInputs.map(([name, topics]) => ({
  name,
  topics: topics.map(memoryFields),
  expectedId: findHighestRiskTopic(topics)?.id ?? null,
  expectedHighRiskCount: countHighRiskTopics(topics),
}))

const fixture = { source: 'src/utils/memoryModel.ts', riskLevels, retention, topicRisks, reviews, highestRisk }

writeFileSync(new URL('./memory-model-parity.json', import.meta.url), `${JSON.stringify(fixture, null, 2)}\n`)
console.log('Wrote memory-model-parity.json')
