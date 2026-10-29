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

/** Subject with the curriculum unit when there is one, e.g. "Türkçe · Dilimizin Zenginliği": topic names repeat across units. */
export function topicContext({ category, unitName }: { category: string; unitName: string | null }): string {
  return unitName ? `${category} · ${unitName}` : category
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

/** Istanbul clock time, e.g. "14:30". */
export function formatTime(iso: string): string {
  return TIME_FORMAT.format(new Date(iso))
}

// Calendar dates (yyyy-MM-dd) from the backend are already Istanbul days: format them as UTC dates so no shift applies.
const CALENDAR_DAY = new Intl.DateTimeFormat('tr-TR', { timeZone: 'UTC', day: 'numeric', month: 'long' })
const CALENDAR_DAY_YEAR = new Intl.DateTimeFormat('tr-TR', { timeZone: 'UTC', day: 'numeric', month: 'long', year: 'numeric' })
const CALENDAR_WEEKDAY = new Intl.DateTimeFormat('tr-TR', { timeZone: 'UTC', weekday: 'long' })

/** "19 Ekim – 25 Ekim 2026" for a Monday–Sunday pair of calendar dates. */
export function formatDateRange(first: string, last: string): string {
  return `${CALENDAR_DAY.format(new Date(`${first}T00:00:00Z`))} – ${CALENDAR_DAY_YEAR.format(new Date(`${last}T00:00:00Z`))}`
}

/** "Çarşamba, 21 Ekim" for a calendar date. */
export function formatCalendarDay(date: string): string {
  const day = new Date(`${date}T00:00:00Z`)
  return `${CALENDAR_WEEKDAY.format(day)}, ${CALENDAR_DAY.format(day)}`
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

const WALL_CLOCK = new Intl.DateTimeFormat('en-CA', {
  timeZone: TIME_ZONE, year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', hourCycle: 'h23',
})
const DATE_TIME_FORMAT = new Intl.DateTimeFormat('tr-TR', {
  timeZone: TIME_ZONE, day: 'numeric', month: 'long', year: 'numeric', hour: '2-digit', minute: '2-digit',
})

/** "23 Ekim 2026 12:00" in Istanbul time, whatever the browser's time zone is. */
export function formatDateTime(iso: string): string {
  return DATE_TIME_FORMAT.format(new Date(iso))
}

/** Istanbul wall-clock fields of an instant. */
function istanbulParts(epochMs: number): Record<'year' | 'month' | 'day' | 'hour' | 'minute', number> {
  const parts = Object.fromEntries(WALL_CLOCK.formatToParts(new Date(epochMs)).map((part) => [part.type, part.value]))
  return { year: +parts.year, month: +parts.month, day: +parts.day, hour: +parts.hour, minute: +parts.minute }
}

/** Istanbul's UTC offset at an instant, in minutes (180 for UTC+3). */
function istanbulOffsetMinutes(epochMs: number): number {
  const p = istanbulParts(epochMs)
  return Math.round((Date.UTC(p.year, p.month - 1, p.day, p.hour, p.minute) - Math.floor(epochMs / 60000) * 60000) / 60000)
}

/** An instant as Istanbul date ("2026-10-23") and time ("12:00") input values. */
export function toIstanbulInput(iso: string): { date: string; time: string } {
  const p = istanbulParts(Date.parse(iso))
  const pad = (n: number) => String(n).padStart(2, '0')
  return { date: `${p.year}-${pad(p.month)}-${pad(p.day)}`, time: `${pad(p.hour)}:${pad(p.minute)}` }
}

/**
 * Istanbul date and time inputs as ISO 8601 with Istanbul's offset, e.g. "2026-10-23T12:00+03:00", the format
 * the backend requires. Null for incomplete or impossible values.
 */
export function fromIstanbulInput(date: string, time: string): string | null {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(date)
  const clock = /^(\d{2}):(\d{2})$/.exec(time)
  if (!match || !clock) return null
  const [year, month, day, hour, minute] = [+match[1], +match[2], +match[3], +clock[1], +clock[2]]
  const wall = Date.UTC(year, month - 1, day, hour, minute)
  const check = new Date(wall)
  if (check.getUTCMonth() !== month - 1 || check.getUTCDate() !== day || hour > 23 || minute > 59) return null

  // The offset at the resulting instant (computed twice in case the first guess crosses an offset change).
  let offset = istanbulOffsetMinutes(wall - istanbulOffsetMinutes(wall) * 60000)
  offset = istanbulOffsetMinutes(wall - offset * 60000)
  const sign = offset < 0 ? '-' : '+'
  const abs = Math.abs(offset)
  const hh = String(Math.floor(abs / 60)).padStart(2, '0')
  const mm = String(abs % 60).padStart(2, '0')
  return `${date}T${time}${sign}${hh}:${mm}`
}

/**
 * Integration boundary: the real app opens the topic's lesson video page in Doping Hafıza. The prototype has
 * no such route, so every lesson video link only shows this notice.
 */
export const LESSON_VIDEO_NOTICE = 'Gerçek uygulamada bu konunun ders videosu sayfası açılacak.'

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
