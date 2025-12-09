import { Routes, Route, Link } from 'react-router-dom'
import { Home, AlertCircle } from 'lucide-react'
import Layout from './components/Layout'
import Dashboard from './pages/Dashboard'
import Agents from './pages/Agents'
import Jobs from './pages/Jobs'
import Workflows from './pages/Workflows'
import Settings from './pages/Settings'
import { useSignalR } from './hooks/useSignalR'

function SignalRProvider({ children }: { children: React.ReactNode }) {
  useSignalR()
  return <>{children}</>
}

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

function App() {
  return (
    <SignalRProvider>
      <Routes>
        <Route path="/" element={<Layout />}>
          <Route index element={<Dashboard />} />
          <Route path="agents" element={<Agents />} />
          <Route path="jobs" element={<Jobs />} />
          <Route path="jobs/:jobId" element={<Jobs />} />
          <Route path="workflows" element={<Workflows />} />
          <Route path="workflows/:workflowId" element={<Workflows />} />
          <Route path="settings" element={<Settings />} />
        </Route>
        <Route path="*" element={<NotFound />} />
      </Routes>
    </SignalRProvider>
  )
}

export default App
