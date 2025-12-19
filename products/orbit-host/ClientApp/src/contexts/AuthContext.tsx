import { createContext, useContext, useState, useCallback, useEffect, type ReactNode } from 'react'

interface AuthContextType {
  isAuthenticated: boolean
  isLoading: boolean
  login: (password: string) => Promise<{ success: boolean; notConfigured?: boolean }>
  logout: () => void
  getAuthHeader: () => string | null
}

const AuthContext = createContext<AuthContextType | null>(null)

const AUTH_STORAGE_KEY = 'orbitmesh_auth'

export function AuthProvider({ children }: { children: ReactNode }) {
  const [isAuthenticated, setIsAuthenticated] = useState(false)
  const [isLoading, setIsLoading] = useState(true)
  const [password, setPassword] = useState<string | null>(null)

  // Check for existing auth on mount
  useEffect(() => {
    const stored = sessionStorage.getItem(AUTH_STORAGE_KEY)
    if (stored) {
      // Verify stored credentials are still valid
      verifyAuth(stored).then((valid) => {
        if (valid) {
          setPassword(stored)
          setIsAuthenticated(true)
        } else {
          sessionStorage.removeItem(AUTH_STORAGE_KEY)
        }
        setIsLoading(false)
      })
    } else {
      setIsLoading(false)
    }
  }, [])

  const verifyAuth = async (pwd: string): Promise<{ success: boolean; notConfigured?: boolean }> => {
    try {
      const response = await fetch('/api/status', {
        headers: {
          'Accept': 'application/json',
          'Content-Type': 'application/json',
          'X-Admin-Password': pwd,
        },
      })
      if (response.ok) {
        return { success: true }
      }
      // Check if password is not configured
      const text = await response.text()
      if (text.includes('not configured')) {
        return { success: false, notConfigured: true }
      }
      return { success: false }
    } catch {
      return { success: false }
    }
  }

  const login = useCallback(async (pwd: string): Promise<{ success: boolean; notConfigured?: boolean }> => {
    const result = await verifyAuth(pwd)
    if (result.success) {
      setPassword(pwd)
      setIsAuthenticated(true)
      sessionStorage.setItem(AUTH_STORAGE_KEY, pwd)
    }
    return result
  }, [])

  const logout = useCallback(() => {
    setPassword(null)
    setIsAuthenticated(false)
    sessionStorage.removeItem(AUTH_STORAGE_KEY)
  }, [])

  const getAuthHeader = useCallback((): string | null => {
    return password
  }, [password])

  return (
    <AuthContext.Provider value={{ isAuthenticated, isLoading, login, logout, getAuthHeader }}>
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth() {
  const context = useContext(AuthContext)
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider')
  }
  return context
}
