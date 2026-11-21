import { useEffect, useRef, useState } from 'react'
import { ArrowLeft, ChevronRight } from 'lucide-react'
import { Link, NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { Database, FileCog, History, Laptop, LogOut, Menu, ScanLine, Settings, Tags, UserRoundCog, X } from 'lucide-react'
import BrandLogo from '../components/BrandLogo.jsx'
import useAuth from '../hooks/useAuth.js'

const navigation = [
  { to: '/dashboard', label: 'Dashboard', icon: Database, roles: ['Admin', 'Supervisor'] },
  { to: '/inspections/new', label: 'New Inspection', icon: ScanLine, roles: ['Admin', 'Supervisor', 'Operator'] },
  { to: '/inspections', label: 'Inspection History', icon: History, roles: ['Admin', 'Supervisor', 'Operator'] },
  { to: '/master-labels', label: 'Master Label', icon: Database, roles: ['Admin', 'Supervisor'] },
  { to: '/label-profiles', label: 'Master Config', icon: FileCog, roles: ['Admin'] },
  { to: '/models', label: 'Model Management', icon: Laptop, roles: ['Admin', 'Supervisor'] },
  { to: '/brands', label: 'Brand Management', icon: Tags, roles: ['Admin'] },
  { to: '/users', label: 'User Management', icon: UserRoundCog, roles: ['Admin', 'Supervisor'] },
  { to: '/settings', label: 'Admin Settings', icon: Settings, roles: ['Admin'] },
]

const pageTrails = {
  '/dashboard': [{ label: 'Dashboard', to: '/dashboard' }],
  '/inspections/new': [
    { label: 'Dashboard', to: '/dashboard' },
    { label: 'Traceability', to: '/inspections' },
    { label: 'Process Traceability' },
  ],
  '/inspections': [
    { label: 'Dashboard', to: '/dashboard' },
    { label: 'Traceability', to: '/inspections' },
    { label: 'Inspection History' },
  ],
  '/master-labels': [
    { label: 'Dashboard', to: '/dashboard' },
    { label: 'Configuration' },
    { label: 'Master Label' },
  ],
  '/label-profiles': [
    { label: 'Dashboard', to: '/dashboard' },
    { label: 'Configuration' },
    { label: 'Master Config' },
  ],
  '/models': [
    { label: 'Dashboard', to: '/dashboard' },
    { label: 'Configuration' },
    { label: 'Model Management' },
  ],
  '/brands': [
    { label: 'Dashboard', to: '/dashboard' },
    { label: 'Configuration' },
    { label: 'Brand Management' },
  ],
  '/users': [
    { label: 'Dashboard', to: '/dashboard' },
    { label: 'Administration' },
    { label: 'User Management' },
  ],
  '/settings': [
    { label: 'Dashboard', to: '/dashboard' },
    { label: 'Administration' },
    { label: 'Admin Settings' },
  ],
  '/profile': [
    { label: 'Dashboard', to: '/dashboard' },
    { label: 'Account' },
    { label: 'My Profile' },
  ],
}

function AppLayout() {
  const [menuOpen, setMenuOpen] = useState(false)
  const location = useLocation()
  const navigate = useNavigate()
  const { user, logout } = useAuth()
  const initialPath = location.pathname === '/dashboard' && user?.role === 'Operator'
    ? '/inspections/new'
    : location.pathname
  const routeHistory = useRef([initialPath])
  const closeMenu = () => setMenuOpen(false)
  const inspectionDetails = location.pathname.startsWith('/inspections/')
    && location.pathname !== '/inspections/new'
  const breadcrumbs = inspectionDetails
    ? [
        { label: 'Dashboard', to: '/dashboard' },
        { label: 'Traceability', to: '/inspections' },
        { label: 'Inspection History', to: '/inspections' },
        { label: 'Inspection Details' },
      ]
    : pageTrails[location.pathname] || [
        { label: 'Dashboard', to: '/dashboard' },
        { label: 'Page' },
      ]
  const pageName = breadcrumbs[breadcrumbs.length - 1].label.toUpperCase()

  useEffect(() => {
    if (location.pathname === '/dashboard' && user?.role === 'Operator') return
    const history = routeHistory.current
    if (history[history.length - 1] !== location.pathname) {
      routeHistory.current = [...history, location.pathname].slice(-20)
    }
  }, [location.pathname, user?.role])

  function goBack() {
    const history = routeHistory.current
    if (history.length > 1) {
      history.pop()
      routeHistory.current = history
      navigate(history[history.length - 1])
      return
    }
    const fallbackPath = user?.role === 'Operator' ? '/inspections' : '/dashboard'
    if (location.pathname !== fallbackPath) navigate(fallbackPath)
  }

  return (
    <div className="mes-shell">
      <button aria-label="Close navigation" className={`mes-overlay${menuOpen ? ' is-visible' : ''}`} onClick={closeMenu} type="button" />
      <header className="mes-header">
        <button aria-label={menuOpen ? 'Close menu' : 'Open menu'} className="mes-menu-button" onClick={() => setMenuOpen((open) => !open)} type="button">
          {menuOpen ? <X size={24} /> : <Menu size={24} />}
        </button>
        <NavLink className="mes-brand" onClick={closeMenu} to="/dashboard">
          <BrandLogo className="mes-brand-logo" />
        </NavLink>
        <div className="mes-header-spacer" />
        <div className="mes-page-ribbon">{user?.role === 'Operator' ? 'OPERATOR WORKSPACE' : pageName}<small>{user?.role || 'USER'}</small></div>
        <div className="mes-header-actions">
          <span className="mes-user"><b>{user?.fullName?.slice(0, 1) || 'U'}</b><span>{user?.fullName || 'User'}</span></span>
          <button aria-label="Sign out" className="mes-icon-button" onClick={logout} title="Sign out" type="button"><LogOut size={21} /></button>
        </div>
      </header>

      <aside className={`mes-drawer${menuOpen ? ' is-open' : ''}`}>
        <div className="mes-drawer-brand">
          <BrandLogo className="mes-drawer-logo" />
        </div>
        <nav aria-label="System navigation">
          <div className="mes-nav-section">Navigation</div>
          {navigation.filter((item) => item.roles.includes(user?.role)).map((item) => {
            const Icon = item.icon
            return <NavLink key={item.to} className={({ isActive }) => `mes-nav-link${isActive ? ' active' : ''}`} onClick={closeMenu} to={item.to} end={item.to === '/dashboard'}><Icon size={19} /><span>{item.label}</span></NavLink>
          })}
        </nav>
        <div className="mes-drawer-footer">
          <span>Signed in as</span><strong>{user?.fullName || 'User'}</strong><small>{user?.role || 'User'}</small>
        </div>
      </aside>

      <main className="mes-content">
        <nav aria-label="Breadcrumb" className="mes-page-navigation">
          <ol className="mes-page-breadcrumbs">
            {breadcrumbs.map((item, index) => (
              <li key={`${item.label}-${index}`}>
                {index > 0 && <ChevronRight aria-hidden="true" size={14} />}
                {item.to && index < breadcrumbs.length - 1
                  ? <Link to={item.to}>{item.label}</Link>
                  : <span aria-current={index === breadcrumbs.length - 1 ? 'page' : undefined}>{item.label}</span>}
              </li>
            ))}
          </ol>
          <button aria-label="Go back" className="mes-back-button" onClick={goBack} title="Go back" type="button">
            <ArrowLeft size={17} />
          </button>
        </nav>
        <Outlet />
      </main>
      <footer className="mes-footer">
        <BrandLogo className="mes-footer-logo" />
        <span>© 2026 Dixon Lens. All rights reserved.</span>
      </footer>
    </div>
  )
}

export default AppLayout
