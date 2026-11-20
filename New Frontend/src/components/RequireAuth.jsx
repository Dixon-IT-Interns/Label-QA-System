import { Navigate, Outlet } from 'react-router-dom'
import useAuth from '../hooks/useAuth.js'

function RequireAuth({ children }) {
  const { isAuthenticated, isLoading } = useAuth()
  if (isLoading) return <div className="configuration-loading" role="status">Restoring secure session…</div>
  if (!isAuthenticated) return <Navigate replace to="/login" />
  return children ?? <Outlet />
}

export default RequireAuth
