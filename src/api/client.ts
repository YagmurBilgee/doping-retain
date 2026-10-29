import type {
  AnswerFeedback,
  DemoGroup,
  DemoGroupResponse,
  DemoNormalTest,
  DemoTopicRow,
  ReviewCompletion,
  ReviewSession,
  StudentsResponse,
  TopicDetail,
  TopicsResponse,
  WeeklySummary,
} from './types'

/** A failed API call: the backend's { code, message } body when it sent one. */
export class ApiError extends Error {
  readonly status: number
  readonly code: string

  constructor(status: number, code: string, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.code = code
  }
}

/** Requests go to /api; the Vite dev server proxies them to the backend. */
async function send(path: string, init?: RequestInit): Promise<Response> {
  let response: Response
  try {
    response = await fetch(`/api${path}`, init)
  } catch (error) {
    if (isAbort(error)) throw error
    throw new ApiError(0, 'network_error', 'Sunucuya ulaşılamadı.')
  }

  if (!response.ok) throw await toApiError(response)
  return response
}

async function toApiError(response: Response): Promise<ApiError> {
  try {
    const body: unknown = await response.json()
    if (body && typeof body === 'object' && 'code' in body && 'message' in body) {
      return new ApiError(response.status, String(body.code), String(body.message))
    }
  } catch {
    // Not a JSON error body (for example the dev proxy's 502 when the API is down).
  }
  return new ApiError(response.status, 'http_error', `Sunucu hatası (${response.status}).`)
}

async function getJson<T>(path: string, signal?: AbortSignal): Promise<T> {
  const response = await send(path, { signal })
  return (await response.json()) as T
}

async function postJson<T>(path: string, body?: unknown, signal?: AbortSignal): Promise<T> {
  const response = await send(path, {
    method: 'POST',
    signal,
    ...(body === undefined ? {} : { headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) }),
  })
  return (await response.json()) as T
}

async function sendJson<T>(method: 'PUT' | 'DELETE', path: string, body?: unknown, signal?: AbortSignal): Promise<T> {
  const response = await send(path, {
    method,
    signal,
    ...(body === undefined ? {} : { headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) }),
  })
  return (await response.json()) as T
}

/** For endpoints that answer 204 No Content; any other success status is unexpected. */
async function postNoContent(path: string): Promise<void> {
  const response = await send(path, { method: 'POST' })
  if (response.status !== 204) {
    throw new ApiError(response.status, 'unexpected_response', `Beklenmeyen yanıt (${response.status}).`)
  }
}

export const getStudents = (signal?: AbortSignal) => getJson<StudentsResponse>('/students', signal)

/*
 * Every student-specific call takes the student id explicitly and puts it in the URL, so a request that is
 * already sent stays bound to the student it was made for, whatever the screen selects afterwards.
 */
const studentPath = (studentId: string) => `/students/${encodeURIComponent(studentId)}`

export const getTopics = (studentId: string, signal?: AbortSignal) =>
  getJson<TopicsResponse>(`${studentPath(studentId)}/topics`, signal)

export const getTopic = (studentId: string, topicId: string, signal?: AbortSignal) =>
  getJson<TopicDetail>(`${studentPath(studentId)}/topics/${encodeURIComponent(topicId)}`, signal)

/** The current week's completed reviews, grouped by Istanbul day. */
export const getWeeklySummary = (studentId: string, signal?: AbortSignal) =>
  getJson<WeeklySummary>(`${studentPath(studentId)}/weekly-summary`, signal)

/** Starts a review session, or resumes the open one with its recorded answers. */
export const startReviewSession = (studentId: string, topicId: string, signal?: AbortSignal) =>
  postJson<ReviewSession>(`${studentPath(studentId)}/topics/${encodeURIComponent(topicId)}/review-sessions`, undefined, signal)

/** Stores the first answer to a question; an identical retry returns the stored feedback. */
export const submitAnswer = (studentId: string, sessionId: string, questionId: string, selectedIndex: number, signal?: AbortSignal) =>
  postJson<AnswerFeedback>(`${studentPath(studentId)}/review-sessions/${sessionId}/answers`, { questionId, selectedIndex }, signal)

/** Completes a fully answered session; a retry returns the stored outcome. */
export const completeReviewSession = (studentId: string, sessionId: string, signal?: AbortSignal) =>
  postJson<ReviewCompletion>(`${studentPath(studentId)}/review-sessions/${sessionId}/complete`, undefined, signal)

/** Resets one demo group only; mapped by the backend only when Demo:Enabled is true. */
export const resetDemoGroup = (group: DemoGroup) => postNoContent(`/demo/groups/${group}/reset`)

/*
 * Demo panel (Demo:Enabled only). Like the student screen, every call names its student in the URL, so a
 * save that is already sent can only ever change the student it was made for.
 */
const demoTopicPath = (studentId: string, topicId: string) =>
  `/demo/students/${encodeURIComponent(studentId)}/topics/${encodeURIComponent(topicId)}`

export const getDemoGroup = (signal?: AbortSignal) => getJson<DemoGroupResponse>('/demo/sixth-grade', signal)

export const getDemoNormalTest = (studentId: string, topicId: string, signal?: AbortSignal) =>
  getJson<DemoNormalTest>(`${demoTopicPath(studentId, topicId)}/normal-test`, signal)

/** Replaces all of the student's history of the topic with one normal test; the backend scores the answers. */
export const saveDemoNormalTest = (
  studentId: string,
  topicId: string,
  body: { answers: { questionId: string; selectedIndex: number }[]; completedAt: string },
  signal?: AbortSignal,
) => sendJson<DemoTopicRow>('PUT', `${demoTopicPath(studentId, topicId)}/normal-test`, body, signal)

/** "Test geçmişi yok": removes all of the student's history of the topic. */
export const clearDemoHistory = (studentId: string, topicId: string, signal?: AbortSignal) =>
  sendJson<DemoTopicRow>('DELETE', `${demoTopicPath(studentId, topicId)}/history`, undefined, signal)

/** True for a request cancelled because its modal closed; such results are dropped silently. */
export function isAbort(error: unknown): boolean {
  return error instanceof DOMException && error.name === 'AbortError'
}

/** Turkish copy for the backend's error codes; its English messages stay on the ApiError for debugging. */
const ERROR_MESSAGES: Record<string, string> = {
  topic_not_found: 'Konu bulunamadı.',
  topic_not_in_group: 'Bu konu seçili sınıfa ait değil.',
  student_not_found: 'Öğrenci bulunamadı.',
  invalid_request: 'İstek eksik: her soru için bir cevap ve tarih gerekli.',
  invalid_answer_count: 'Her soru için tam olarak bir cevap seçilmeli.',
  duplicate_question: 'Bir soru birden fazla kez cevaplanmış.',
  question_not_in_test: 'Cevaplardan biri bu testin sorusu değil.',
  option_out_of_range: 'Geçersiz bir seçenek gönderildi.',
  invalid_completed_at: 'Tarih ve saat geçersiz.',
  future_completion: 'Test tarihi gelecekte olamaz.',
  group_not_editable: 'Yalnızca 6. sınıf demo verileri düzenlenebilir.',
  topic_has_no_normal_test: 'Bu konunun normal testi yok.',
  topic_has_no_questions: 'Bu konu için henüz soru yok.',
  session_not_found: 'Bu tekrar oturumu artık yok; demo sıfırlanmış olabilir.',
  session_abandoned: 'Konunun hafıza durumu değiştiği için bu oturumun yerini yenisi aldı.',
  stale_session: 'Konunun hafıza durumu bu oturum başladıktan sonra değişti.',
  session_closed: 'Bu tekrar oturumu kapanmış.',
  session_incomplete: 'Tüm sorular yanıtlanmadan sonuç alınamaz.',
  answer_conflict: 'Bu soru farklı bir cevapla zaten yanıtlanmış.',
}

/** Short Turkish reason for the UI. */
export function errorMessage(error: unknown): string {
  if (!(error instanceof ApiError)) return 'Beklenmeyen bir hata oluştu.'
  const known = ERROR_MESSAGES[error.code]
  if (known) return known
  // 502–504 come from the dev proxy when the API is not running.
  const unreachable = error.status === 0 || (error.status >= 502 && error.status <= 504)
  return unreachable ? 'Sunucuya ulaşılamadı.' : `Sunucu hatası (${error.status}).`
}
