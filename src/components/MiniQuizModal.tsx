import { useEffect, useReducer, useRef } from 'react'
import { Check, Lightbulb, RotateCcw, X } from 'lucide-react'
import { ApiError, completeReviewSession, errorMessage, isAbort, startReviewSession, submitAnswer } from '../api/client'
import type { AnswerFeedback, ReviewCompletion, ReviewSession, SessionQuestion } from '../api/types'
import ModalShell from './ModalShell'
import QuizResultView from './QuizResultView'

interface MiniQuizModalProps {
  /** Owner of the session; every request names this student, so a sent request never moves to another one */
  studentId: string
  /** Session started by an explicit user action; mount this component per session. */
  session: ReviewSession
  topicTitle: string
  /** A reinforcement of a support-needed topic ends with its own headline; the quiz itself is the same */
  variant?: 'review' | 'reinforcement'
  /**
   * Closing never completes the review; an unfinished session resumes on the next start.
   * completionUncertain: completion was sent but no outcome confirmed it (for example a lost response),
   * so the server may or may not have stored the review.
   */
  onClose: (completionUncertain: boolean) => void
  /** Called once the server has stored the review */
  onCompleted: (outcome: ReviewCompletion) => void
}

/*
 * Quiz state machine. The server owns correctness, the score and the outcome:
 *   question --select--> pending --ok--> answered (server feedback) --next--> next question
 *                                  \--failure--> failed (same selection kept; retry resends it)
 *   last answered --"Sonucu Gör"--> completing --ok--> result (stored outcome)
 *   missing, abandoned or stale session --> sessionProblem --explicit restart--> resumed or fresh session
 */
type Request = 'answer' | 'complete' | 'restart'

interface QuizState {
  session: ReviewSession
  feedback: Record<string, AnswerFeedback>
  index: number
  /** Option sent for the current question; kept after a failure so a retry resends the same one */
  selected: number | null
  pending: Request | null
  /** Retryable failure of the last request */
  error: { request: Request; message: string } | null
  /** The session can no longer be used; only an explicit restart helps */
  sessionProblem: string | null
  outcome: ReviewCompletion | null
}

type QuizAction =
  | { type: 'request'; request: Request; selected?: number }
  | { type: 'answered'; sessionId: string; feedback: AnswerFeedback }
  | { type: 'completed'; sessionId: string; outcome: ReviewCompletion }
  | { type: 'failed'; sessionId: string; request: Request; message: string }
  | { type: 'session_problem'; sessionId: string; message: string }
  | { type: 'restarted'; session: ReviewSession }
  | { type: 'next' }

/** Server codes after which this session cannot continue as it is. */
const SESSION_PROBLEM_CODES = new Set([
  'session_not_found',
  'session_abandoned',
  'stale_session',
  'session_closed',
  'session_incomplete',
  'answer_conflict',
])

/** Resumes at the first unanswered question, or at the last one when all are answered. */
function initQuiz(session: ReviewSession): QuizState {
  const feedback = Object.fromEntries(session.answers.map((answer) => [answer.questionId, answer]))
  const firstOpen = session.questions.findIndex((question) => !feedback[question.id])

  return {
    session,
    feedback,
    index: firstOpen === -1 ? session.questions.length - 1 : firstOpen,
    selected: null,
    pending: null,
    error: null,
    sessionProblem: null,
    outcome: null,
  }
}

function quizReducer(state: QuizState, action: QuizAction): QuizState {
  // Responses for a session this quiz has since replaced must not touch the current one.
  if ('sessionId' in action && action.sessionId !== state.session.sessionId) return state

  switch (action.type) {
    case 'request':
      return { ...state, pending: action.request, selected: action.selected ?? state.selected, error: null }
    case 'answered':
      return {
        ...state,
        feedback: { ...state.feedback, [action.feedback.questionId]: action.feedback },
        pending: null,
        selected: null,
      }
    case 'completed':
      return { ...state, outcome: action.outcome, pending: null }
    case 'failed':
      return { ...state, pending: null, error: { request: action.request, message: action.message } }
    case 'session_problem':
      return { ...state, pending: null, sessionProblem: action.message }
    case 'restarted':
      return initQuiz(action.session)
    case 'next': {
      const question = state.session.questions[state.index]
      if (!state.feedback[question.id] || state.index + 1 >= state.session.questions.length) return state
      return { ...state, index: state.index + 1, error: null }
    }
  }
}

const OPTION_LETTERS = ['A', 'B', 'C', 'D', 'E']

function MiniQuizModal({ studentId, session, topicTitle, variant = 'review', onClose, onCompleted }: MiniQuizModalProps) {
  const [state, dispatch] = useReducer(quizReducer, session, initQuiz)
  // One request at a time; a ref so a double click cannot slip in before the next render.
  const inFlight = useRef(false)
  // Set once a completion request for the current session has actually been sent.
  const completionSent = useRef(false)
  const controller = useRef<AbortController | null>(null)

  // Closing the modal cancels whatever is still in flight; late results are dropped.
  useEffect(() => {
    const current = new AbortController()
    controller.current = current
    return () => current.abort()
  }, [])

  const { sessionId, topicId, questions } = state.session
  const question = questions[state.index]

  function send<T>(request: Request, call: (signal: AbortSignal) => Promise<T>, onSuccess: (value: T) => void, selected?: number) {
    const signal = controller.current?.signal
    if (inFlight.current || !signal || signal.aborted) return
    inFlight.current = true
    dispatch({ type: 'request', request, selected })

    call(signal)
      .then(
        (value) => {
          if (!signal.aborted) onSuccess(value)
        },
        (error: unknown) => {
          if (signal.aborted || isAbort(error)) return
          dispatch(
            error instanceof ApiError && SESSION_PROBLEM_CODES.has(error.code)
              ? { type: 'session_problem', sessionId, message: errorMessage(error) }
              : { type: 'failed', sessionId, request, message: errorMessage(error) },
          )
        },
      )
      .finally(() => {
        inFlight.current = false
      })
  }

  const answer = (option: number) =>
    send(
      'answer',
      (signal) => submitAnswer(studentId, sessionId, question.id, option, signal),
      (feedback) => dispatch({ type: 'answered', sessionId, feedback }),
      option,
    )

  // Completion is only ever sent from the explicit "Sonucu Gör" action.
  const complete = () =>
    send(
      'complete',
      (signal) => {
        completionSent.current = true
        return completeReviewSession(studentId, sessionId, signal)
      },
      (outcome) => {
        dispatch({ type: 'completed', sessionId, outcome })
        onCompleted(outcome)
      },
    )

  const restart = () =>
    send(
      'restart',
      (signal) => startReviewSession(studentId, topicId, signal),
      (next) => {
        completionSent.current = false
        dispatch({ type: 'restarted', session: next })
      },
    )

  const close = () => onClose(completionSent.current && !state.outcome)

  const stepKey = state.outcome ? 'result' : state.sessionProblem ? 'problem' : `${sessionId}-${state.index}`

  return (
    <ModalShell isOpen size="wide" onClose={close} labelledBy="mini-quiz-title">
      {/* Keyed per step so each question and the result fade in on change. */}
      <div key={stepKey} className="motion-safe:animate-fade-in">
        {state.outcome ? (
          <QuizResultView outcome={state.outcome} variant={variant} onFinish={close} />
        ) : state.sessionProblem ? (
          <SessionProblem
            message={state.sessionProblem}
            isRestarting={state.pending === 'restart'}
            error={state.error?.request === 'restart' ? state.error.message : undefined}
            onRestart={restart}
            onClose={close}
          />
        ) : (
          <QuestionStep
            topicTitle={topicTitle}
            question={question}
            index={state.index}
            total={questions.length}
            feedback={state.feedback[question.id]}
            selected={state.selected}
            pending={state.pending}
            error={state.error}
            onSelect={answer}
            onRetryAnswer={() => state.selected !== null && answer(state.selected)}
            onNext={() => dispatch({ type: 'next' })}
            onComplete={complete}
          />
        )}
      </div>
    </ModalShell>
  )
}

interface SessionProblemProps {
  message: string
  isRestarting: boolean
  error?: string
  onRestart: () => void
  onClose: () => void
}

function SessionProblem({ message, isRestarting, error, onRestart, onClose }: SessionProblemProps) {
  return (
    <div className="pr-10" role="alert">
      <h2 id="mini-quiz-title" className="section-title">
        Bu tekrar oturumu devam edemiyor
      </h2>
      <p className="mt-2 text-sm text-ink-secondary">{message}</p>
      <p className="mt-1 text-sm text-ink-secondary">
        Yeniden başlatınca kayıtlı cevapların varsa kaldığın yerden devam edersin, yoksa yeni bir oturum açılır.
      </p>
      {error && <p className="mt-2 text-sm text-danger-ink">{error}</p>}
      <footer className="mt-5 flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
        <button type="button" className="btn-secondary" onClick={onClose}>
          Kapat
        </button>
        <button type="button" className="btn-primary inline-flex items-center justify-center gap-1.5" onClick={onRestart} disabled={isRestarting}>
          <RotateCcw size={14} aria-hidden="true" />
          {isRestarting ? 'Başlatılıyor…' : 'Yeniden Başlat'}
        </button>
      </footer>
    </div>
  )
}

interface QuestionStepProps {
  topicTitle: string
  question: SessionQuestion
  index: number
  total: number
  /** Server feedback once this question is answered */
  feedback: AnswerFeedback | undefined
  selected: number | null
  pending: Request | null
  error: QuizState['error']
  onSelect: (option: number) => void
  onRetryAnswer: () => void
  onNext: () => void
  onComplete: () => void
}

function QuestionStep({
  topicTitle,
  question,
  index,
  total,
  feedback,
  selected,
  pending,
  error,
  onSelect,
  onRetryAnswer,
  onNext,
  onComplete,
}: QuestionStepProps) {
  const isLast = index === total - 1
  // Locked while a request is pending, after a failure (only the same selection may be resent) and once answered.
  const isLocked = feedback !== undefined || selected !== null
  const questionRef = useRef<HTMLHeadingElement>(null)
  const nextButtonRef = useRef<HTMLButtonElement>(null)
  const isAnswered = feedback !== undefined

  // Move focus with the flow so keyboard and screen-reader users follow along.
  useEffect(() => {
    if (index > 0) questionRef.current?.focus()
  }, [index])

  useEffect(() => {
    if (isAnswered) nextButtonRef.current?.focus()
  }, [isAnswered])

  return (
    <>
      <header className="pr-10">
        <div className="flex items-center gap-2">
          <span className="badge badge-brand">{topicTitle}</span>
          <span className="section-subtitle">
            Soru {index + 1} / {total}
          </span>
        </div>
        <ol className="mt-3 flex gap-1.5" aria-hidden="true">
          {Array.from({ length: total }, (_, step) => (
            <li
              key={step}
              className={`h-1.5 flex-1 rounded-full ${step <= index ? 'bg-brand' : 'bg-surface-muted'}`}
            />
          ))}
        </ol>
      </header>

      <h2
        id="mini-quiz-title"
        ref={questionRef}
        tabIndex={-1}
        className="mt-5 text-lg leading-snug font-semibold text-ink outline-none"
      >
        {question.text}
      </h2>

      <ul className="mt-4 flex flex-col gap-2">
        {question.options.map((option, optionIndex) => {
          const optionState = getOptionState(optionIndex, feedback, selected)
          return (
            <li key={option}>
              <button
                type="button"
                onClick={() => onSelect(optionIndex)}
                disabled={isLocked}
                aria-busy={optionState === 'selected' && pending === 'answer'}
                className={`flex w-full items-center gap-3 rounded-tile border p-card-sm text-left text-sm font-medium transition-colors ${OPTION_STYLES[optionState].button}`}
              >
                <span className={`icon-tile size-8 text-xs font-bold ${OPTION_STYLES[optionState].tile}`} aria-hidden="true">
                  {optionState === 'correct' ? <Check size={16} /> : optionState === 'wrong' ? <X size={16} /> : OPTION_LETTERS[optionIndex]}
                </span>
                <span className="flex-1">{option}</span>
              </button>
            </li>
          )
        })}
      </ul>

      <div className="mt-4 min-h-5" role="status">
        {feedback ? (
          <AnswerResult feedback={feedback} options={question.options} />
        ) : pending === 'answer' ? (
          <p className="text-sm text-ink-secondary">Cevabın kontrol ediliyor…</p>
        ) : error?.request === 'answer' ? (
          <div className="flex flex-wrap items-center gap-2">
            <p className="text-sm text-danger-ink">Cevap gönderilemedi. {error.message}</p>
            <button type="button" className="btn-secondary inline-flex items-center gap-1.5" onClick={onRetryAnswer}>
              <RotateCcw size={14} aria-hidden="true" />
              Tekrar Dene
            </button>
          </div>
        ) : null}
      </div>

      {error?.request === 'complete' && (
        <p className="mt-2 text-sm text-danger-ink" role="alert">
          Sonuç alınamadı. {error.message} Tekrar deneyebilirsin.
        </p>
      )}

      <footer className="mt-4 flex justify-end">
        <button
          ref={nextButtonRef}
          type="button"
          className="btn-primary disabled:opacity-50"
          onClick={isLast ? onComplete : onNext}
          disabled={!isAnswered || pending !== null}
        >
          {!isLast ? 'Sonraki Soru' : pending === 'complete' ? 'Sonuç kaydediliyor…' : 'Sonucu Gör'}
        </button>
      </footer>
    </>
  )
}

function AnswerResult({ feedback, options }: { feedback: AnswerFeedback; options: string[] }) {
  if (feedback.isCorrect) {
    return <p className="text-sm font-semibold text-success-ink">Doğru! Harika gidiyorsun.</p>
  }

  return (
    <>
      <p className="text-sm font-semibold text-danger-ink">
        Yanlış. Doğru cevap: {OPTION_LETTERS[feedback.correctIndex]}) {options[feedback.correctIndex]}
      </p>
      {feedback.explanation && (
        <p className="mt-2 flex gap-2 rounded-tile bg-brand-soft p-card-sm text-sm text-ink motion-safe:animate-fade-in">
          <Lightbulb size={16} className="mt-0.5 shrink-0 text-brand" aria-hidden="true" />
          <span>
            <strong className="font-semibold text-brand">Çözüm İpucu:</strong> {feedback.explanation}
          </span>
        </p>
      )}
    </>
  )
}

type OptionState = 'idle' | 'selected' | 'correct' | 'wrong' | 'dimmed'

const OPTION_STYLES: Record<OptionState, { button: string; tile: string }> = {
  idle: {
    button: 'border-line bg-surface-muted text-ink hover:border-brand hover:bg-brand-soft',
    tile: 'bg-surface text-ink-secondary',
  },
  selected: {
    button: 'border-brand bg-brand-soft text-ink',
    tile: 'bg-brand text-white',
  },
  correct: {
    button: 'border-success bg-success-soft text-success-ink',
    tile: 'bg-success text-white',
  },
  wrong: {
    button: 'border-danger bg-danger-soft text-danger-ink',
    tile: 'bg-danger text-white',
  },
  dimmed: {
    button: 'border-line bg-surface-muted text-ink-muted',
    tile: 'bg-surface text-ink-muted',
  },
}

/** Correct and wrong come only from server feedback; before that only the sent selection is marked. */
function getOptionState(optionIndex: number, feedback: AnswerFeedback | undefined, selected: number | null): OptionState {
  if (feedback) {
    if (optionIndex === feedback.correctIndex) return 'correct'
    if (optionIndex === feedback.selectedIndex) return 'wrong'
    return 'dimmed'
  }
  if (selected === null) return 'idle'
  return optionIndex === selected ? 'selected' : 'dimmed'
}

export default MiniQuizModal
