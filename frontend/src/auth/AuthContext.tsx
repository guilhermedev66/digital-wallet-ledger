import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { apiClient, isApiError, type User } from '../api'

export { isApiError }

const SESSION_STORAGE_KEY = 'walletledger.session.v1'

interface StoredSession {
  user: User
  token: string
}

interface AuthContextValue {
  user: User | null
  isAuthenticated: boolean
  isInitializing: boolean
  login(email: string, password: string): Promise<void>
  register(email: string, password: string): Promise<void>
  logout(): void
}

const AuthContext = createContext<AuthContextValue | null>(null)

function readStoredSession(): StoredSession | null {
  try {
    const raw = localStorage.getItem(SESSION_STORAGE_KEY)
    return raw ? (JSON.parse(raw) as StoredSession) : null
  } catch {
    return null
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null)
  const [isInitializing, setIsInitializing] = useState(true)

  useEffect(() => {
    const stored = readStoredSession()
    if (stored) {
      apiClient.setAuthToken(stored.token)
      setUser(stored.user)
    }
    setIsInitializing(false)
  }, [])

  const persist = (session: StoredSession | null) => {
    if (session) {
      localStorage.setItem(SESSION_STORAGE_KEY, JSON.stringify(session))
      apiClient.setAuthToken(session.token)
      setUser(session.user)
    } else {
      localStorage.removeItem(SESSION_STORAGE_KEY)
      apiClient.setAuthToken(null)
      setUser(null)
    }
  }

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      isAuthenticated: user !== null,
      isInitializing,
      async login(email, password) {
        const session = await apiClient.login(email, password)
        persist(session)
      },
      async register(email, password) {
        // The register endpoint only creates the account (returns a userId, no token) -
        // it doesn't issue a session. Log in right after so the product-level "register
        // lands you on the dashboard" flow still holds.
        await apiClient.register(email, password)
        const session = await apiClient.login(email, password)
        persist(session)
      },
      logout() {
        persist(null)
      },
    }),
    [user, isInitializing],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used within an AuthProvider')
  return ctx
}
