import { Routes, Route, Link } from 'react-router-dom'
import { Home, AlertCircle, Loader2 } from 'lucide-react'
import Layout from './components/Layout'
import Dashboard from './pages/Dashboard'
import Agents from './pages/Agents'
import AgentDetail from './pages/AgentDetail'
import Jobs from './pages/Jobs'
import JobDetail from './pages/JobDetail'
import Workflows from './pages/Workflows'
import WorkflowCreate from './pages/WorkflowCreate'
import WorkflowEdit from './pages/WorkflowEdit'
import Settings from './pages/Settings'
import Login from './pages/Login'
import { SignalRProvider } from './contexts/SignalRContext'
import { useAuth } from './contexts/AuthContext'
import { CommandPalette, useCommandPalette } from './components/CommandPalette'

function NotFound() {
  return (
    <div className="min-h-screen flex items-center justify-center bg-slate-100 dark:bg-slate-900">
      <div className="text-center">
        <AlertCircle className="w-16 h-16 text-slate-400 mx-auto mb-4" />
        <h1 className="text-4xl font-bold text-slate-900 dark:text-white mb-2">404</h1>
        <p className="text-slate-600 dark:text-slate-400 mb-6">Page not found</p>
        <Link
          to="/"
          className="btn-primary inline-flex items-center gap-2"
        >
          <Home className="w-4 h-4" />
          Go Home
        </Link>
      </div>
    </div>
  )
}

function AppContent() {
  const { open, setOpen } = useCommandPalette()

  return (
    <>
      <CommandPalette open={open} onOpenChange={setOpen} />
      <Routes>
        {/* Full-screen workflow editor (outside Layout) */}
        <Route path="/workflows/new" element={<WorkflowCreate />} />
        <Route path="/workflows/:workflowId/edit" element={<WorkflowEdit />} />

        {/* Main layout routes */}
        <Route path="/" element={<Layout />}>
          <Route index element={<Dashboard />} />
          <Route path="agents" element={<Agents />} />
          <Route path="agents/:agentId" element={<AgentDetail />} />
          <Route path="jobs" element={<Jobs />} />
          <Route path="jobs/:jobId" element={<JobDetail />} />
          <Route path="workflows" element={<Workflows />} />
          <Route path="workflows/:workflowId" element={<Workflows />} />
          <Route path="settings" element={<Settings />} />
        </Route>
        <Route path="*" element={<NotFound />} />
      </Routes>
    </>
  )
}

function LoadingScreen() {
  return (
    <div className="min-h-screen flex items-center justify-center bg-slate-900">
      <div className="text-center">
        <Loader2 className="w-8 h-8 text-blue-500 animate-spin mx-auto mb-4" />
        <p className="text-slate-400">Loading...</p>
      </div>
    </div>
  )
}

function App() {
  const { isAuthenticated, isLoading } = useAuth()

  if (isLoading) {
    return <LoadingScreen />
  }

  if (!isAuthenticated) {
    return <Login />
  }

  return (
    <SignalRProvider>
      <AppContent />
    </SignalRProvider>
  )
}

export default App
