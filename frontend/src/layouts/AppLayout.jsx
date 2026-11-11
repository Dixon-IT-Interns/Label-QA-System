import { useState } from 'react'
import { NavLink, Outlet, useLocation } from 'react-router-dom'
import {
  ArrowUpRight,
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
  const [menuOpen, setMenuOpen] = useState(false)
  const location = useLocation()
  const { isAuthenticated, user, logout } = useAuth()
  const isAdmin = user?.role === 'Admin'
  const closeMenu = () => setMenuOpen(false)
  const currentPath = location.pathname === '/' ? 'Dashboard' : 'LabelVerify'

  return (
    <div className="app-shell">
      <button
        aria-label="Close menu"
        className={`mobile-overlay${menuOpen ? ' is-visible' : ''}`}
        onClick={closeMenu}
        type="button"
      />
      <header className="topbar">
        <div className="brand-lockup">
          <NavLink aria-label="LabelVerify dashboard" className="brand-home" onClick={closeMenu} to="/">
            <div className="brand-mark"><ShieldCheck size={19} strokeWidth={2.2} /></div>
            <div className="brand-name">LabelVerify</div>
          </NavLink>
        </div>
        <button
          aria-expanded={menuOpen}
          aria-label={menuOpen ? 'Close navigation menu' : 'Open navigation menu'}
          className="icon-button menu-toggle"
          onClick={() => setMenuOpen((open) => !open)}
          type="button"
        >
          {menuOpen ? <X size={18} /> : <Menu size={18} />}
        </button>
        <nav aria-label="Main navigation" className={`main-navigation${menuOpen ? ' is-open' : ''}`}>
          <ul className="nav-list">
            {primaryNavigation.map((item) => (
              <NavigationLink item={item} key={item.to} onNavigate={closeMenu} />
            ))}
            {isAdmin && configurationNavigation.map((item) => (
              <NavigationLink item={item} key={item.to} onNavigate={closeMenu} />
            ))}
            {isAdmin && <NavigationLink item={{ to: '/users', label: 'Users', icon: UserRoundCog }} onNavigate={closeMenu} />}
            {isAdmin && <NavigationLink item={{ to: '/settings', label: 'Settings', icon: Settings }} onNavigate={closeMenu} />}
          </ul>
        </nav>
        <div className="topbar-actions">
          <span className="environment-tag"><span />{import.meta.env.MODE}</span>
          <div className="user-menu">
            <div className="user-avatar">{isAuthenticated ? user?.fullName?.slice(0, 2) : '?'}</div>
            <div className="user-menu-copy">
              <strong>{isAuthenticated ? user?.fullName : 'Signed-out session'}</strong>
              <span>{isAuthenticated ? user?.role : 'Authentication not configured'}</span>
            </div>
            {isAuthenticated && <button aria-label="Sign out" className="icon-button signout-button" onClick={logout} title="Sign out" type="button"><LogOut size={15} /><span>Sign out</span><ArrowUpRight className="signout-arrow" size={12} /></button>}
          </div>
        </div>
      </header>
      <main className="content-wrap" data-page={currentPath}>
        <Outlet />
      </main>
    </div>
  )
}

export default AppLayout
