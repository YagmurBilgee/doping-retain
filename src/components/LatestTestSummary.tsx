import type { TestResult } from '../api/types'
import { formatScore, formatTestAge, testTypeLabel } from './reviewPresentation'

interface LatestTestSummaryProps {
  test: TestResult | null
  /** Backend evaluation instant the test age is measured from */
  evaluatedAt: string
}

/** The latest observed test result as it happened: score, type and age. States plainly when there is none. */
function LatestTestSummary({ test, evaluatedAt }: LatestTestSummaryProps) {
  // Spans, so the summary is also valid inside a button.
  if (!test) return <span className="block text-xs text-ink-muted">Henüz test verisi yok</span>

  return (
    <span className="block text-xs">
      <span className="block font-semibold text-ink">Son test: {formatScore(test)}</span>
      <span className="block text-ink-muted">
        {testTypeLabel(test.testType)} · {formatTestAge(test.completedAt, evaluatedAt)}
      </span>
    </span>
  )
}

export default LatestTestSummary
