import DesktopTitleBar from './components/DesktopTitleBar'
import HomePage from './pages/HomePage'

function App() {
  return (
    <div className="flex min-h-screen flex-col">
      <DesktopTitleBar />
      <HomePage />
    </div>
  )
}

export default App
