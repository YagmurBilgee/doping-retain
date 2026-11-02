import { useDemoSelection } from './demoSelection'
import DemoPage from './pages/DemoPage'
import HomePage from './pages/HomePage'

function App() {
  const selection = useDemoSelection()

  return (
    <div className="flex min-h-screen flex-col">
      {/*
        Both pages are keyed by the selection: switching page, grade or student remounts them, which closes
        every dialog, drops answers and transient state, and aborts the old student's requests (late results
        are ignored). Requests already sent stay bound to the student named in their URL.
      */}
      {selection.page === 'demo' ? (
        <DemoPage
          key={`demo:${selection.student?.id ?? ''}`}
          student={selection.student}
          groupStudents={selection.groupStudents}
          students={selection.students}
          demoEnabled={selection.demoEnabled}
          onSelectStudent={selection.selectStudent}
          onOpenStudentScreen={selection.openStudentScreen}
          onRetryStudents={selection.retryStudents}
        />
      ) : (
        <HomePage
          key={`home:${selection.group}:${selection.student?.id ?? ''}`}
          group={selection.group}
          student={selection.student}
          students={selection.students}
          groupStudents={selection.groupStudents}
          demoEnabled={selection.demoEnabled}
          onSelectGroup={selection.selectGroup}
          onSelectStudent={selection.selectStudent}
          onOpenDemoPanel={selection.openDemo}
          onRetryStudents={selection.retryStudents}
        />
      )}
    </div>
  )
}

export default App
