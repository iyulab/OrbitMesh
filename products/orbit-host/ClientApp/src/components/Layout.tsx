import { Outlet, NavLink } from 'react-router-dom'
import {
  LayoutDashboard,
  Server,
  PlayCircle,
  GitBranch,
  Settings,
  Orbit,
  Sun,
  Moon,
  Monitor,
  Wifi,
  WifiOff,
  RefreshCw,
  Search,
  Menu,
  X,
  LogOut,
} from 'lucide-react'
import { useState, useEffect } from 'react'
import { useTheme } from '@/contexts/ThemeContext'
import { useAuth } from '@/contexts/AuthContext'
import { useSignalRContext } from '@/contexts/SignalRContext'
import { NotificationCenter } from '@/components/NotificationCenter'

const navItems = [
  { to: '/', icon: LayoutDashboard, label: 'Dashboard' },
  { to: '/agents', icon: Server, label: 'Agents' },
  { to: '/jobs', icon: PlayCircle, label: 'Jobs' },
  { to: '/workflows', icon: GitBranch, label: 'Workflows' },
  { to: '/settings', icon: Settings, label: 'Settings' },
]

function ThemeToggle() {
  const { theme, setTheme } = useTheme()

  const themes = [
    { value: 'light' as const, icon: Sun, label: 'Light' },
    { value: 'dark' as const, icon: Moon, label: 'Dark' },
    { value: 'system' as const, icon: Monitor, label: 'System' },
  ]

  return (
    <div className="flex items-center gap-1 p-1 rounded-lg bg-slate-200 dark:bg-slate-700">
      {themes.map(({ value, icon: Icon, label }) => (
        <button
          key={value}
          onClick={() => setTheme(value)}
          className={`p-1.5 rounded-md transition-colors ${
            theme === value
              ? 'bg-white dark:bg-slate-600 text-orbit-600 dark:text-orbit-400 shadow-sm'
              : 'text-slate-500 dark:text-slate-400 hover:text-slate-700 dark:hover:text-slate-200'
          }`}
          title={label}
        >
          <Icon className="w-4 h-4" />
        </button>
      ))}
    </div>
  )
}

function ConnectionStatus() {
  const { connectionStatus } = useSignalRContext()

  const statusConfig = {
    connected: {
      icon: Wifi,
      color: 'bg-green-500',
      text: 'Server Online',
      animate: 'animate-pulse',
    },
    connecting: {
      icon: RefreshCw,
      color: 'bg-yellow-500',
      text: 'Connecting...',
      animate: 'animate-spin',
    },
    reconnecting: {
      icon: RefreshCw,
      color: 'bg-yellow-500',
      text: 'Reconnecting...',
      animate: 'animate-spin',
    },
    disconnected: {
      icon: WifiOff,
      color: 'bg-red-500',
      text: 'Disconnected',
      animate: '',
    },
  }

  const config = statusConfig[connectionStatus]
  const Icon = config.icon

  return (
    <div className="flex items-center gap-2">
      {connectionStatus === 'connected' ? (
        <div className={`w-2 h-2 ${config.color} rounded-full ${config.animate}`} />
      ) : (
        <Icon className={`w-4 h-4 text-slate-500 ${config.animate}`} />
      )}
      <span className="text-sm text-slate-500 dark:text-slate-400">{config.text}</span>
    </div>
  )
}

function SearchButton() {
  const handleClick = () => {
    // Trigger Ctrl+K event to open command palette
    const event = new KeyboardEvent('keydown', {
      key: 'k',
      ctrlKey: true,
      bubbles: true,
    })
    document.dispatchEvent(event)
  }

  return (
    <button
      onClick={handleClick}
      className="flex items-center gap-2 w-full px-3 py-2 text-sm text-slate-500 dark:text-slate-400 bg-slate-100 dark:bg-slate-900 rounded-lg hover:bg-slate-200 dark:hover:bg-slate-800 transition-colors"
      aria-label="Open search"
    >
      <Search className="w-4 h-4" />
      <span className="flex-1 text-left">Search...</span>
      <kbd className="hidden sm:inline-flex h-5 select-none items-center gap-1 rounded border border-slate-200 dark:border-slate-600 bg-white dark:bg-slate-700 px-1.5 font-mono text-[10px] font-medium text-slate-500">
        Ctrl K
      </kbd>
    </button>
  )
}

function LogoutButton() {
  const { logout } = useAuth()

  return (
    <button
      onClick={logout}
      className="flex items-center gap-2 w-full px-3 py-2 text-sm text-slate-500 dark:text-slate-400 hover:text-red-600 dark:hover:text-red-400 hover:bg-slate-100 dark:hover:bg-slate-700 rounded-lg transition-colors"
    >
      <LogOut className="w-4 h-4" />
      <span>Sign Out</span>
    </button>
  )
}

function Sidebar({ mobile = false, onClose }: { mobile?: boolean; onClose?: () => void }) {
  return (
    <aside
      className={`${
        mobile ? 'w-full' : 'w-64 hidden md:flex'
      } bg-white dark:bg-slate-800 border-r border-slate-200 dark:border-slate-700 flex-col transition-colors h-full`}
    >
      {/* Logo */}
      <div className="p-4 border-b border-slate-200 dark:border-slate-700">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="p-2 bg-orbit-600 rounded-lg">
              <Orbit className="w-6 h-6 text-white" />
            </div>
            <div>
              <h1 className="text-lg font-bold text-slate-900 dark:text-white">OrbitMesh</h1>
              <p className="text-xs text-slate-500 dark:text-slate-400">Control Center</p>
            </div>
          </div>
          {mobile && onClose && (
            <button
              onClick={onClose}
              className="p-2 text-slate-500 hover:text-slate-700 dark:hover:text-slate-300"
              aria-label="Close menu"
            >
              <X className="w-5 h-5" />
            </button>
          )}
        </div>
      </div>

      {/* Search */}
      <div className="p-4 border-b border-slate-200 dark:border-slate-700">
        <SearchButton />
      </div>

      {/* Navigation */}
      <nav className="flex-1 p-4">
        <ul className="space-y-1">
          {navItems.map(({ to, icon: Icon, label }) => (
            <li key={to}>
              <NavLink
                to={to}
                end={to === '/'}
                onClick={mobile ? onClose : undefined}
                className={({ isActive }) =>
                  `flex items-center gap-3 px-3 py-2 rounded-lg transition-colors ${
                    isActive
                      ? 'bg-orbit-600 text-white'
                      : 'text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white hover:bg-slate-100 dark:hover:bg-slate-700'
                  }`
                }
              >
                <Icon className="w-5 h-5" />
                <span>{label}</span>
              </NavLink>
            </li>
          ))}
        </ul>
      </nav>

      {/* Theme Toggle & Server Status */}
      <div className="p-4 border-t border-slate-200 dark:border-slate-700 space-y-3">
        <div className="flex items-center justify-between">
          <span className="text-sm text-slate-600 dark:text-slate-400">Theme</span>
          <ThemeToggle />
        </div>
        <div className="flex items-center justify-between">
          <ConnectionStatus />
          <NotificationCenter />
        </div>
        <LogoutButton />
      </div>
    </aside>
  )
}

export default function Layout() {
  const [sidebarOpen, setSidebarOpen] = useState(false)

  // Close sidebar on route change (for mobile)
  useEffect(() => {
    setSidebarOpen(false)
  }, [])

  return (
    <div className="flex h-screen bg-slate-100 dark:bg-slate-900 transition-colors">
      {/* Desktop Sidebar */}
      <Sidebar />

      {/* Mobile Sidebar Overlay */}
      {sidebarOpen && (
        <div className="fixed inset-0 z-40 md:hidden">
          <div
            className="fixed inset-0 bg-black/50"
            onClick={() => setSidebarOpen(false)}
          />
          <div className="fixed inset-y-0 left-0 w-64 z-50">
            <Sidebar mobile onClose={() => setSidebarOpen(false)} />
          </div>
        </div>
      )}

      {/* Main Content */}
      <div className="flex-1 flex flex-col overflow-hidden">
        {/* Mobile Header */}
        <header className="md:hidden flex items-center justify-between p-4 bg-white dark:bg-slate-800 border-b border-slate-200 dark:border-slate-700">
          <button
            onClick={() => setSidebarOpen(true)}
            className="p-2 text-slate-500 hover:text-slate-700 dark:hover:text-slate-300"
            aria-label="Open menu"
          >
            <Menu className="w-6 h-6" />
          </button>
          <div className="flex items-center gap-2">
            <div className="p-1.5 bg-orbit-600 rounded-lg">
              <Orbit className="w-5 h-5 text-white" />
            </div>
            <span className="font-bold text-slate-900 dark:text-white">OrbitMesh</span>
          </div>
          <div className="flex items-center gap-1">
            <NotificationCenter />
            <button
              onClick={() => {
                const event = new KeyboardEvent('keydown', {
                  key: 'k',
                  ctrlKey: true,
                  bubbles: true,
                })
                document.dispatchEvent(event)
              }}
              className="p-2 text-slate-500 hover:text-slate-700 dark:hover:text-slate-300"
              aria-label="Search"
            >
              <Search className="w-5 h-5" />
            </button>
          </div>
        </header>

        <main className="flex-1 overflow-auto">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
