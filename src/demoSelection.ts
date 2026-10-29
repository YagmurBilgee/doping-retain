import { useCallback, useEffect, useMemo, useState, type MouseEvent } from 'react'
import { errorMessage, getStudents } from './api/client'
import type { DemoGroup, LoadState, Student, StudentsResponse } from './api/types'

/*
 * The page, demo group (grade) and student live in the URL, so a refresh and the browser's back/forward
 * buttons restore them:
 *   student screen  /?grade=6&student=grade6-elif   (?grade=12 for YKS)
 *   demo panel      /demo?student=grade6-elif       (always the sixth grade)
 * The student comes from the backend's list and must belong to the selected grade; anything else falls back
 * to that grade's default (its first student). This module is the only place that decides which page and
 * student the app shows; there is no routing library, only history.pushState and popstate.
 */

export type Page = 'home' | 'demo'

export interface GradeOption {
  group: DemoGroup
  /** Value of the ?grade= parameter */
  param: string
  /** Menu label */
  label: string
  /** Compact label on the selector button */
  shortLabel: string
}

export const GRADE_OPTIONS: GradeOption[] = [
  { group: 'sixth_grade', param: '6', label: '6. Sınıf', shortLabel: '6. Sınıf' },
  { group: 'yks', param: '12', label: '12. Sınıf / YKS', shortLabel: '12. Sınıf' },
]

/** Sixth grade unless the URL names another valid grade. */
const DEFAULT_GROUP: DemoGroup = 'sixth_grade'

/** The demo panel edits only this group. */
export const DEMO_PANEL_GROUP: DemoGroup = 'sixth_grade'

const DEMO_PATH = '/demo'

export function gradeOption(group: DemoGroup): GradeOption {
  return GRADE_OPTIONS.find((option) => option.group === group)!
}

interface UrlSelection {
  page: Page
  group: DemoGroup
  studentId: string | null
}

function readUrl(): UrlSelection {
  const params = new URLSearchParams(window.location.search)
  const page: Page = window.location.pathname.replace(/\/+$/, '') === DEMO_PATH ? 'demo' : 'home'
  const grade = GRADE_OPTIONS.find((option) => option.param === params.get('grade'))
  return {
    page,
    group: page === 'demo' ? DEMO_PANEL_GROUP : (grade?.group ?? DEFAULT_GROUP),
    studentId: params.get('student'),
  }
}

/** The URL of a selection; also usable as a plain link target. */
export function urlFor(page: Page, group: DemoGroup, studentId: string | null): string {
  const params = new URLSearchParams()
  if (page === 'home') params.set('grade', gradeOption(group).param)
  if (studentId) params.set('student', studentId)
  return `${page === 'demo' ? DEMO_PATH : '/'}?${params}`
}

/** The URL's student when it belongs to the group, otherwise the group's default student. */
export function resolveStudent(students: Student[], group: DemoGroup, studentId: string | null): Student | null {
  const inGroup = students.filter((student) => student.group === group)
  return inGroup.find((student) => student.id === studentId) ?? inGroup[0] ?? null
}

/**
 * Click handler for an in-app link: a plain left click navigates without a reload; modified clicks
 * (new tab, new window) keep the browser's default behavior on the same href.
 */
export function onInAppLinkClick(navigate: () => void) {
  return (event: MouseEvent<HTMLAnchorElement>) => {
    if (event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return
    event.preventDefault()
    navigate()
  }
}

export interface DemoSelection {
  page: Page
  group: DemoGroup
  /** Null while the student list loads or when it failed */
  student: Student | null
  /** Students of the selected group, in the backend's order */
  groupStudents: Student[]
  students: LoadState<StudentsResponse>
  demoEnabled: boolean
  selectGroup: (group: DemoGroup) => void
  selectStudent: (studentId: string) => void
  /** Opens the demo panel with the selected sixth-grade student (or the group's default). */
  openDemo: () => void
  /** Opens the student screen of this student, in the student's own grade. */
  openStudentScreen: (student: Student) => void
  retryStudents: () => void
}

export function useDemoSelection(): DemoSelection {
  const [students, setStudents] = useState<LoadState<StudentsResponse>>({ status: 'loading' })
  const [url, setUrl] = useState<UrlSelection>(readUrl)
  const [attempt, setAttempt] = useState(0)

  useEffect(() => {
    const controller = new AbortController()
    getStudents(controller.signal).then(
      (data) => setStudents({ status: 'ready', data }),
      (error: unknown) => {
        if (!controller.signal.aborted) setStudents({ status: 'error', message: errorMessage(error) })
      },
    )
    return () => controller.abort()
  }, [attempt])

  // Back/forward restores the selection that URL describes.
  useEffect(() => {
    const onPopState = () => setUrl(readUrl())
    window.addEventListener('popstate', onPopState)
    return () => window.removeEventListener('popstate', onPopState)
  }, [])

  const allStudents = useMemo(() => (students.status === 'ready' ? students.data.students : []), [students])
  const groupStudents = useMemo(() => allStudents.filter((s) => s.group === url.group), [allStudents, url.group])
  const student = useMemo(
    () => (students.status === 'ready' ? resolveStudent(allStudents, url.group, url.studentId) : null),
    [students.status, allStudents, url],
  )

  // Writes the resolved selection back without a new history entry: an invalid or missing student id is
  // replaced by the group's default, and the grade parameter is always present on the student screen.
  useEffect(() => {
    if (students.status !== 'ready') return
    const canonical = urlFor(url.page, url.group, student?.id ?? null)
    if (canonical !== `${window.location.pathname}${window.location.search}`) {
      window.history.replaceState(window.history.state, '', canonical)
    }
  }, [students.status, url.page, url.group, student])

  const go = useCallback((next: UrlSelection) => {
    window.history.pushState(window.history.state, '', urlFor(next.page, next.group, next.studentId))
    setUrl(next)
  }, [])

  const selectGroup = useCallback(
    (group: DemoGroup) => {
      if (group === url.group) return
      go({ page: url.page, group, studentId: resolveStudent(allStudents, group, null)?.id ?? null })
    },
    [allStudents, go, url.group, url.page],
  )

  const selectStudent = useCallback(
    (studentId: string) => {
      if (studentId === student?.id) return
      go({ page: url.page, group: url.group, studentId })
    },
    [go, student?.id, url.group, url.page],
  )

  const openDemo = useCallback(() => {
    const current = student?.group === DEMO_PANEL_GROUP ? student.id : null
    go({ page: 'demo', group: DEMO_PANEL_GROUP, studentId: current })
  }, [go, student])

  const openStudentScreen = useCallback((target: Student) => go({ page: 'home', group: target.group, studentId: target.id }), [go])

  const retryStudents = useCallback(() => {
    setStudents({ status: 'loading' })
    setAttempt((current) => current + 1)
  }, [])

  return {
    page: url.page,
    group: url.group,
    student,
    groupStudents,
    students,
    demoEnabled: students.status === 'ready' && students.data.demoEnabled,
    selectGroup,
    selectStudent,
    openDemo,
    openStudentScreen,
    retryStudents,
  }
}
