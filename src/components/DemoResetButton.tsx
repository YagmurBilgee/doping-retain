import { useEffect } from 'react'
import { RotateCcw } from 'lucide-react'

interface DemoResetButtonProps {
  onReset: () => void
}

/** Presentation helper: subtle corner button plus Alt+Shift+R to restore the initial demo state. */
function DemoResetButton({ onReset }: DemoResetButtonProps) {
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
    <button
      type="button"
      onClick={onReset}
      title="Demoyu sıfırla (Alt+Shift+R)"
      className="fixed bottom-4 left-4 z-10 inline-flex items-center gap-1.5 rounded-full bg-surface/80 px-3 py-1.5 text-xs font-medium text-ink-muted opacity-60 shadow-card backdrop-blur-xs transition-opacity hover:opacity-100 focus-visible:opacity-100 focus-visible:outline-2 focus-visible:outline-brand"
    >
      <RotateCcw size={12} aria-hidden="true" />
      Demoyu Sıfırla
    </button>
  )
}

export default DemoResetButton
