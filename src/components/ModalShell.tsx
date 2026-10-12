import { useEffect, useRef, type MouseEvent, type ReactNode, type SyntheticEvent } from 'react'
import { X } from 'lucide-react'

interface ModalShellProps {
  isOpen: boolean
  /** Called for Escape, backdrop clicks and any in-modal close action */
  onClose: () => void
  labelledBy: string
  children: ReactNode
}

/**
 * Shared modal frame built on the native <dialog> element: showModal() provides
 * focus trapping, Escape handling and top-layer rendering without a portal.
 * Open state stays owned by the parent.
 */
function ModalShell({ isOpen, onClose, labelledBy, children }: ModalShellProps) {
  const dialogRef = useRef<HTMLDialogElement>(null)

  useEffect(() => {
    const dialog = dialogRef.current
    if (!dialog) return

    if (isOpen && !dialog.open) dialog.showModal()
    if (!isOpen && dialog.open) dialog.close()
  }, [isOpen])

  useEffect(() => {
    if (!isOpen) return

    const previousOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    return () => {
      document.body.style.overflow = previousOverflow
    }
  }, [isOpen])

  const handleCancel = (event: SyntheticEvent<HTMLDialogElement>) => {
    // Keep open state owned by the parent instead of letting Escape close it natively.
    event.preventDefault()
    onClose()
  }

  const handleBackdropClick = (event: MouseEvent<HTMLDialogElement>) => {
    if (event.target === event.currentTarget) onClose()
  }

  return (
    <dialog
      ref={dialogRef}
      aria-labelledby={labelledBy}
      onCancel={handleCancel}
      onClick={handleBackdropClick}
      className="m-auto w-[calc(100%-2rem)] max-w-lg bg-transparent p-0 backdrop:bg-ink/40 backdrop:backdrop-blur-xs motion-safe:backdrop:animate-fade-in"
    >
      {isOpen && (
        <div className="card relative p-card shadow-float motion-safe:animate-modal-in">
          <button
            type="button"
            aria-label="Kapat"
            onClick={onClose}
            className="absolute top-4 right-4 flex size-8 items-center justify-center rounded-full text-ink-secondary transition-colors hover:bg-surface-muted hover:text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand"
          >
            <X size={18} />
          </button>
          {children}
        </div>
      )}
    </dialog>
  )
}

export default ModalShell
