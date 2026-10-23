import { useEffect } from 'react'
import { RotateCcw } from 'lucide-react'

interface DemoResetButtonProps {
  onReset: () => void
  isResetting: boolean
  /** Shown when the last reset failed */
  error?: string
}

/** Development helper: subtle button at the end of the right column plus Alt+Shift+R to reset the demo data on the backend. */
function DemoResetButton({ onReset, isResetting, error }: DemoResetButtonProps) {
  useEffect(() => {
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.altKey && event.shiftKey && event.code === 'KeyR') {
        event.preventDefault()
        onReset()
      }
    }

    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [onReset])

  return (
    <div className="flex flex-col items-end gap-1.5">
      <button
        type="button"
        onClick={onReset}
        disabled={isResetting}
        title="Demoyu sıfırla (Alt+Shift+R)"
        className="inline-flex items-center gap-1.5 rounded-full bg-surface px-3 py-1.5 text-xs font-medium text-ink-muted opacity-60 shadow-card transition-opacity hover:opacity-100 focus-visible:opacity-100 focus-visible:outline-2 focus-visible:outline-brand disabled:cursor-wait disabled:opacity-40"
      >
        <RotateCcw size={12} aria-hidden="true" className={isResetting ? 'motion-safe:animate-spin' : undefined} />
        {isResetting ? 'Sıfırlanıyor…' : 'Demoyu Sıfırla'}
      </button>
      {error && (
        <p role="alert" className="text-xs text-danger-ink">
          Demo sıfırlanamadı. {error}
        </p>
      )}
    </div>
  )
}

export default DemoResetButton
