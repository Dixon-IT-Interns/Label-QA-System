import { Navigate, Outlet } from 'react-router-dom'
import useAuth from '../hooks/useAuth.js'

function RequireRole({ role, roles, children }) {
  const { user } = useAuth()
  const allowedRoles = roles ?? [role]
  if (!allowedRoles.includes(user?.role)) return <Navigate replace to="/dashboard" />
  return children ?? <Outlet />
}

export default RequireRole
