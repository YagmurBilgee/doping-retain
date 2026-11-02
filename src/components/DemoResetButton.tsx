import { useEffect } from 'react'
import { RotateCcw } from 'lucide-react'

interface DemoResetButtonProps {
  /** Names the group the reset applies to, e.g. "Demoyu Sıfırla (6. Sınıf)" */
  label: string
  onReset: () => void
  isResetting: boolean
  /** Shown when the last reset failed */
  error?: string
}

/** Demo helper (when the backend has Demo:Enabled): subtle button plus Alt+Shift+R to reset the selected group's demo data. */
function DemoResetButton({ label, onReset, isResetting, error }: DemoResetButtonProps) {
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
        title={`${label} (Alt+Shift+R)`}
        className="inline-flex items-center gap-1.5 rounded-full bg-surface px-3 py-1.5 text-xs font-medium text-ink-muted opacity-60 shadow-card transition-opacity hover:opacity-100 focus-visible:opacity-100 focus-visible:outline-2 focus-visible:outline-brand disabled:cursor-wait disabled:opacity-40"
      >
        <RotateCcw size={12} aria-hidden="true" className={isResetting ? 'motion-safe:animate-spin' : undefined} />
        {isResetting ? 'Sıfırlanıyor…' : label}
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
