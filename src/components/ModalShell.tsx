import { useEffect, useRef, type MouseEvent, type ReactNode, type SyntheticEvent } from 'react'
import { X } from 'lucide-react'

interface ModalShellProps {
  isOpen: boolean
  /** Called for Escape, backdrop clicks and any in-modal close action */
  onClose: () => void
  labelledBy: string
  /** 'wide' (880px) for the Retain review flow; other modals keep the default width */
  size?: 'default' | 'wide'
  children: ReactNode
}

/**
 * Shared modal frame built on the native <dialog> element: showModal() provides
 * focus trapping, Escape handling and top-layer rendering without a portal.
 * Open state stays owned by the parent.
 */
function ModalShell({ isOpen, onClose, labelledBy, size = 'default', children }: ModalShellProps) {
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
      className={`m-auto max-h-[calc(100dvh-2rem)] w-[calc(100%-2rem)] overflow-visible bg-transparent p-0 backdrop:bg-ink/40 backdrop:backdrop-blur-xs motion-safe:backdrop:animate-fade-in ${size === 'wide' ? 'max-w-[880px]' : 'max-w-lg'}`}
    >
      {isOpen && (
        // At most the viewport height (16px margin on every side); longer content scrolls inside,
        // while the close button sits outside the scrolling area and stays visible.
        <div className="relative flex max-h-[calc(100dvh-2rem)] flex-col overflow-hidden rounded-card border border-line bg-surface shadow-float motion-safe:animate-modal-in">
          <button
            type="button"
            aria-label="Kapat"
            onClick={onClose}
            className="absolute top-4 right-4 z-10 flex size-8 items-center justify-center rounded-full bg-surface text-ink-secondary transition-colors hover:bg-surface-muted hover:text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand"
          >
            <X size={18} />
          </button>
          <div className="overflow-y-auto overscroll-contain p-card">{children}</div>
        </div>
      )}
    </dialog>
  )
}

export default ModalShell
