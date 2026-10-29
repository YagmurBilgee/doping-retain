import { ChevronDown } from 'lucide-react'
import type { Student } from '../api/types'

interface StudentSelectorProps {
  students: Student[]
  studentId: string | null
  onSelectStudent: (studentId: string) => void
}

/** The demo student of the selected grade; the names come from the backend's student list. */
function StudentSelector({ students, studentId, onSelectStudent }: StudentSelectorProps) {
  return (
    <label className="relative inline-flex h-13 items-center rounded-tile bg-surface font-manrope text-[15px] text-ink-slate shadow-float focus-within:outline-2 focus-within:outline-brand">
      <span className="sr-only">Öğrenci</span>
      <select
        value={studentId ?? ''}
        disabled={students.length < 2}
        onChange={(event) => onSelectStudent(event.target.value)}
        className="h-full max-w-44 cursor-pointer appearance-none truncate rounded-tile bg-transparent pr-9 pl-3.5 outline-none disabled:cursor-default"
      >
        {students.length === 0 && <option value="">Öğrenci yükleniyor…</option>}
        {students.map((student) => (
          <option key={student.id} value={student.id}>
            {student.displayName}
          </option>
        ))}
      </select>
      {students.length > 1 && <ChevronDown size={16} aria-hidden="true" className="pointer-events-none absolute right-3.5" />}
    </label>
  )
}

export default StudentSelector
