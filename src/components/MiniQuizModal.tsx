import { useEffect, useReducer, useRef } from 'react'
import { Check, Lightbulb, X } from 'lucide-react'
import type { ReviewOutcome, Topic } from '../types'
import { applyReviewResult } from '../utils/memoryModel'
import ModalShell from './ModalShell'
import QuizResultView from './QuizResultView'

interface MiniQuizModalProps {
  /** Mount this component per quiz session; unmounting resets the quiz state. */
  topic: Topic
  /** Abandons the quiz without changing the topic */
  onClose: () => void
  /** Called once the student leaves the result view */
  onComplete: (outcome: ReviewOutcome) => void
}

/*
 * Quiz state machine:
 *   answering(index, selected = null) --select--> answering(index, selected)
 *   answering(index, selected)        --next---> answering(index + 1, null)  (more questions)
 *                                              \-> result                     (last question)
 */
type QuizState =
  | { phase: 'answering'; index: number; selected: number | null; score: number }
  | { phase: 'result'; score: number }

type QuizAction =
  | { type: 'select'; option: number; correctIndex: number }
  | { type: 'next'; total: number }

const INITIAL_STATE: QuizState = { phase: 'answering', index: 0, selected: null, score: 0 }

function quizReducer(state: QuizState, action: QuizAction): QuizState {
  if (state.phase !== 'answering') return state

  switch (action.type) {
    case 'select':
      if (state.selected !== null) return state
      return {
        ...state,
        selected: action.option,
        score: action.option === action.correctIndex ? state.score + 1 : state.score,
      }
    case 'next':
      if (state.selected === null) return state
      return state.index + 1 < action.total
        ? { ...state, index: state.index + 1, selected: null }
        : { phase: 'result', score: state.score }
  }
}

const OPTION_LETTERS = ['A', 'B', 'C', 'D', 'E']

function MiniQuizModal({ topic, onClose, onComplete }: MiniQuizModalProps) {
  const [state, dispatch] = useReducer(quizReducer, INITIAL_STATE)
  const questions = topic.quiz ?? []
  const total = questions.length

  const outcome = state.phase === 'result' ? applyReviewResult(topic, state.score, total) : null

  // Once a result exists, any way of leaving the modal keeps the review.
  const handleDismiss = outcome ? () => onComplete(outcome) : onClose

  return (
    <ModalShell isOpen onClose={handleDismiss} labelledBy="mini-quiz-title">
      {/* Keyed per step so each question and the result fade in on change. */}
      <div key={state.phase === 'answering' ? `question-${state.index}` : 'result'} className="motion-safe:animate-fade-in">
        {state.phase === 'answering' ? (
          <QuestionStep
            topic={topic}
            index={state.index}
            selected={state.selected}
            total={total}
            onSelect={(option) =>
              dispatch({ type: 'select', option, correctIndex: questions[state.index].correctIndex })
            }
            onNext={() => dispatch({ type: 'next', total })}
          />
        ) : (
          outcome && (
            <QuizResultView
              topic={topic}
              score={state.score}
              total={total}
              outcome={outcome}
              onFinish={() => onComplete(outcome)}
            />
          )
        )}
      </div>
    </ModalShell>
  )
}

interface QuestionStepProps {
  topic: Topic
  index: number
  selected: number | null
  total: number
  onSelect: (option: number) => void
  onNext: () => void
}

function QuestionStep({ topic, index, selected, total, onSelect, onNext }: QuestionStepProps) {
  const question = (topic.quiz ?? [])[index]
  const isAnswered = selected !== null
  const isLast = index === total - 1
  const questionRef = useRef<HTMLHeadingElement>(null)
  const nextButtonRef = useRef<HTMLButtonElement>(null)

  // Move focus with the flow so keyboard and screen-reader users follow along.
  useEffect(() => {
    if (index > 0) questionRef.current?.focus()
  }, [index])

  useEffect(() => {
    if (isAnswered) nextButtonRef.current?.focus()
  }, [isAnswered])

  if (!question) return null

  const isCorrect = selected === question.correctIndex

  return (
    <>
      <header className="pr-10">
        <div className="flex items-center gap-2">
          <span className="badge badge-brand">{topic.title}</span>
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
        {question.question}
      </h2>

      <ul className="mt-4 flex flex-col gap-2">
        {question.options.map((option, optionIndex) => {
          const optionState = getOptionState(optionIndex, selected, question.correctIndex)
          return (
            <li key={option}>
              <button
                type="button"
                onClick={() => onSelect(optionIndex)}
                disabled={isAnswered}
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
        {isAnswered &&
          (isCorrect ? (
            <p className="text-sm font-semibold text-success-ink">Doğru! Harika gidiyorsun.</p>
          ) : (
            <>
              <p className="text-sm font-semibold text-danger-ink">
                Yanlış. Doğru cevap: {OPTION_LETTERS[question.correctIndex]}) {question.options[question.correctIndex]}
              </p>
              {question.explanation && (
                <p className="mt-2 flex gap-2 rounded-tile bg-brand-soft p-card-sm text-sm text-ink motion-safe:animate-fade-in">
                  <Lightbulb size={16} className="mt-0.5 shrink-0 text-brand" aria-hidden="true" />
                  <span>
                    <strong className="font-semibold text-brand">Çözüm İpucu:</strong> {question.explanation}
                  </span>
                </p>
              )}
            </>
          ))}
      </div>

      <footer className="mt-4 flex justify-end">
        <button ref={nextButtonRef} type="button" className="btn-primary disabled:opacity-50" onClick={onNext} disabled={!isAnswered}>
          {isLast ? 'Sonucu Gör' : 'Sonraki Soru'}
        </button>
      </footer>
    </>
  )
}

type OptionState = 'idle' | 'correct' | 'wrong' | 'dimmed'

const OPTION_STYLES: Record<OptionState, { button: string; tile: string }> = {
  idle: {
    button: 'border-line bg-surface-muted text-ink hover:border-brand hover:bg-brand-soft',
    tile: 'bg-surface text-ink-secondary',
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

function getOptionState(optionIndex: number, selected: number | null, correctIndex: number): OptionState {
  if (selected === null) return 'idle'
  if (optionIndex === correctIndex) return 'correct'
  if (optionIndex === selected) return 'wrong'
  return 'dimmed'
}

export default MiniQuizModal
