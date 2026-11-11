import { useState } from 'react'
import { NavLink, Outlet, useLocation } from 'react-router-dom'
import {
  Bell,
  Boxes,
  ClipboardCheck,
  FileStack,
  Gauge,
  Laptop,
  Menu,
  ScanLine,
  LogOut,
  ShieldCheck,
  Settings,
  UserRoundCog,
  X,
} from 'lucide-react'
import useAuth from '../hooks/useAuth.js'

const primaryNavigation = [
  { to: '/', label: 'Dashboard', icon: Gauge, end: true },
  { to: '/inspections/new', label: 'New inspection', icon: ScanLine },
  { to: '/inspections', label: 'Inspection history', icon: ClipboardCheck },
]

const configurationNavigation = [
  { to: '/brands', label: 'Brand management', icon: Boxes },
  { to: '/models', label: 'Model management', icon: Laptop },
  { to: '/label-profiles', label: 'Label profiles', icon: FileStack },
]

function NavigationLink({ item, onNavigate }) {
  const Icon = item.icon

  return (
    <li>
      <NavLink
        className={({ isActive }) => `nav-link${isActive ? ' active' : ''}`}
        end={item.end}
        onClick={onNavigate}
        to={item.to}
      >
        <Icon size={17} strokeWidth={1.8} />
        <span>{item.label}</span>
      </NavLink>
    </li>
  )
}

function AppLayout() {
  const [sidebarOpen, setSidebarOpen] = useState(false)
  const location = useLocation()
  const { isAuthenticated, user, logout } = useAuth()
  const isAdmin = user?.role === 'Admin'
  const currentPath = location.pathname === '/' ? 'Overview' : 'Workspace'

  const closeSidebar = () => setSidebarOpen(false)

  return (
    <div className="app-shell">
      <button
        aria-label="Close navigation"
        className={`mobile-overlay${sidebarOpen ? ' is-visible' : ''}`}
        onClick={closeSidebar}
        type="button"
      />
      <aside className={`sidebar${sidebarOpen ? ' is-open' : ''}`}>
        <div className="brand-lockup">
          <div className="brand-mark"><ShieldCheck size={20} strokeWidth={2.2} /></div>
          <div>
            <div className="brand-name">LabelVerify</div>
            <span className="brand-caption">Manufacturing QA</span>
          </div>
        </div>

        <div className="nav-section-label">Workspace</div>
        <ul className="nav-list">
          {primaryNavigation.map((item) => (
            <NavigationLink item={item} key={item.to} onNavigate={closeSidebar} />
          ))}
        </ul>

        {isAdmin && <>
          <div className="nav-section-label">Configuration</div>
          <ul className="nav-list">
            {configurationNavigation.map((item) => (
              <NavigationLink item={item} key={item.to} onNavigate={closeSidebar} />
            ))}
          </ul>
          <div className="nav-section-label">Administration</div>
          <ul className="nav-list">
            <NavigationLink item={{ to: '/users', label: 'User management', icon: UserRoundCog }} onNavigate={closeSidebar} />
            <NavigationLink item={{ to: '/settings', label: 'System settings', icon: Settings }} onNavigate={closeSidebar} />
          </ul>
        </>}

        <div className="sidebar-footer">
          <div className="user-avatar">{isAuthenticated ? user?.fullName?.slice(0, 2) : '?'}</div>
          <div>
            <div className="user-name">{isAuthenticated ? user?.fullName : 'Signed-out session'}</div>
            <div className="user-role">{isAuthenticated ? user?.role : 'Authentication not configured'}</div>
          </div>
          {isAuthenticated && <button aria-label="Sign out" className="icon-button" onClick={logout} title="Sign out" type="button"><LogOut color="#8c96a6" size={16} /></button>}
        </div>
      </aside>

      <div className="main-shell">
        <header className="topbar">
          <div className="topbar-left">
            <button
              aria-label={sidebarOpen ? 'Close menu' : 'Open menu'}
              className="icon-button menu-toggle"
              onClick={() => setSidebarOpen((open) => !open)}
              type="button"
            >
              {sidebarOpen ? <X size={18} /> : <Menu size={18} />}
            </button>
            <div className="topbar-title"><strong>{currentPath}</strong> / Label verification</div>
          </div>
          <div className="topbar-actions">
            <span className="status-pill status-muted">Environment: {import.meta.env.MODE}</span>
            <div className="topbar-divider" />
            <button aria-label="Notifications" className="icon-button" type="button">
              <Bell size={16} strokeWidth={1.8} />
            </button>
            <div className="user-avatar">{isAuthenticated ? user?.fullName?.slice(0, 2) : '?'}</div>
          </div>
        </header>
        <main className="content-wrap">
          <Outlet />
        </main>
      </div>
    </div>
  )
}

export default AppLayout
