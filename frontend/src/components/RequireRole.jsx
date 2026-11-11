import { Navigate, Outlet } from 'react-router-dom'
import useAuth from '../hooks/useAuth.js'

function RequireRole({ role, children }) {
  const { user } = useAuth()
  if (user?.role !== role) return <Navigate replace to="/" />
  return children ?? <Outlet />
}

export default RequireRole
