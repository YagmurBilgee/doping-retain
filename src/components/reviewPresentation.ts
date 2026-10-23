import type { ReviewStatus, TestResult, TestType } from '../api/types'

export interface StatusPresentation {
  label: string
  badge: string
}

/** One supportive label per backend review status, shared by every Retain component. */
const STATUS_PRESENTATION: Record<ReviewStatus, StatusPresentation> = {
  insufficient_data: { label: 'Henüz test verisi yok', badge: 'bg-surface-muted text-ink-secondary' },
  support_needed: { label: 'Pekiştirme öneriliyor', badge: 'badge-brand' },
  due: { label: 'Tekrar zamanı', badge: 'badge-risk' },
  scheduled: { label: 'Şimdilik bekleyebilir', badge: 'badge-success' },
}

export function getStatusPresentation(status: ReviewStatus): StatusPresentation {
  return STATUS_PRESENTATION[status]
}

const TEST_TYPE_LABEL: Record<TestType, string> = {
  normal_test: 'Normal test',
  reminder_mini_quiz: 'Tekrar mini testi',
}

export function testTypeLabel(type: TestType): string {
  return TEST_TYPE_LABEL[type]
}

/** Observed accuracy from the actual counts, e.g. "%80 · 16/20 doğru". Never a model estimate. */
export function formatScore({ correctCount, totalQuestions }: Pick<TestResult, 'correctCount' | 'totalQuestions'>): string {
  return `%${Math.round((correctCount / totalQuestions) * 100)} · ${correctCount}/${totalQuestions} doğru`
}

// The student's calendar, matching the backend's Istanbul week.
const TIME_ZONE = 'Europe/Istanbul'
const DATE_FORMAT = new Intl.DateTimeFormat('tr-TR', { timeZone: TIME_ZONE, day: 'numeric', month: 'long', weekday: 'long' })
const TIME_FORMAT = new Intl.DateTimeFormat('tr-TR', { timeZone: TIME_ZONE, hour: '2-digit', minute: '2-digit' })
const DAY_KEY = new Intl.DateTimeFormat('en-CA', { timeZone: TIME_ZONE, year: 'numeric', month: '2-digit', day: '2-digit' })
const DAY_MS = 24 * 60 * 60 * 1000

/** Istanbul calendar day as a UTC midnight timestamp, so whole-day differences ignore the time of day. */
function calendarDay(iso: string): number {
  return Date.parse(`${DAY_KEY.format(new Date(iso))}T00:00:00Z`)
}

/** Calendar days from <code>from</code> to <code>to</code> in Istanbul time. */
function calendarDaysBetween(from: string, to: string): number {
  return Math.round((calendarDay(to) - calendarDay(from)) / DAY_MS)
}

/** Whole Istanbul calendar days since a test was completed (never negative). */
export function daysSinceTest(completedAt: string, referenceIso: string): number {
  return Math.max(0, calendarDaysBetween(completedAt, referenceIso))
}

/** "21 Ekim Çarşamba", or "bugün 14:30" when it falls on the reference day. */
export function formatDate(iso: string, referenceIso: string): string {
  return calendarDaysBetween(referenceIso, iso) === 0 ? `bugün ${TIME_FORMAT.format(new Date(iso))}` : DATE_FORMAT.format(new Date(iso))
}

/** When a test was completed, relative to the reference instant: "bugün", "dün", "11 gün önce". */
export function formatTestAge(completedAt: string, referenceIso: string): string {
  const days = calendarDaysBetween(completedAt, referenceIso)
  if (days <= 0) return 'bugün'
  return days === 1 ? 'dün' : `${days} gün önce`
}

/**
 * The model's recommendation from the backend status and nextReviewAt; nothing is recalculated here.
 * Null without test data, so no review date is ever invented.
 */
export function scheduleText(status: ReviewStatus, nextReviewAt: string | null, referenceIso: string): string | null {
  switch (status) {
    case 'support_needed':
      return 'Önce ilgili noktaları pekiştir.'
    case 'due':
      return 'Tekrar zamanı geldi.'
    case 'scheduled':
      return nextReviewAt ? `Sonraki tekrar: ${formatDate(nextReviewAt, referenceIso)}` : null
    case 'insufficient_data':
      return null
  }
}
