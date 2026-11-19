import { lazy, Suspense } from 'react'
import { Navigate, Route, Routes } from 'react-router-dom'
import { CosmicBackground } from './components/CosmicBackground'
import { SkeletonCard, SkeletonList } from './components/Skeleton'
import { RequireAuth } from './auth/RequireAuth'
import { AppShell } from './layout/AppShell'

const lazyPage = <K extends string>(load: () => Promise<Record<K, React.ComponentType>>, name: K) =>
  lazy(() => load().then((m) => ({ default: m[name] })))

const LoginPage = lazyPage(() => import('./pages/auth/LoginPage'), 'LoginPage')
const RegisterPage = lazyPage(() => import('./pages/auth/RegisterPage'), 'RegisterPage')
const DashboardPage = lazyPage(() => import('./pages/dashboard/DashboardPage'), 'DashboardPage')
const TransferPage = lazyPage(() => import('./pages/transfer/TransferPage'), 'TransferPage')
const ActivityPage = lazyPage(() => import('./pages/activity/ActivityPage'), 'ActivityPage')
const ReconciliationPage = lazyPage(
  () => import('./pages/reconciliation/ReconciliationPage'),
  'ReconciliationPage',
)

function PageFallback() {
  return (
    <div style={{ padding: 'var(--space-5)', width: 'min(560px, 100%)' }}>
      <SkeletonCard />
      <div style={{ height: 'var(--space-5)' }} />
      <SkeletonList rows={3} />
    </div>
  )
}

function App() {
  return (
    <>
    <CosmicBackground />
    <Suspense fallback={<PageFallback />}>
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/register" element={<RegisterPage />} />
      <Route
        element={
          <RequireAuth>
            <AppShell />
          </RequireAuth>
        }
      >
        <Route path="/" element={<DashboardPage />} />
        <Route path="/transfer" element={<TransferPage />} />
        <Route path="/activity" element={<ActivityPage />} />
        <Route path="/reconciliation" element={<ReconciliationPage />} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
    </Suspense>
    </>
  )
}

export default App
