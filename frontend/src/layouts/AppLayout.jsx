import { useState } from 'react'
import { NavLink, Outlet, useLocation } from 'react-router-dom'
import {
  ArrowUpRight,
  Boxes,
  ClipboardCheck,
  Database,
  FileStack,
  Gauge,
  History,
  Home,
  Laptop,
  Menu,
  ScanLine,
  LogOut,
  ShieldCheck,
  SlidersHorizontal,
  Settings,
  UserRoundCog,
  X,
} from 'lucide-react'
import useAuth from '../hooks/useAuth.js'

const primaryNavigation = [
  { to: '/dashboard', label: 'Dashboard', icon: Gauge, end: true },
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
  const isSupervisor = user?.role === 'Supervisor'
  const canCreateInspection = isSupervisor || user?.role === 'Operator'
  const closeMenu = () => setMenuOpen(false)
  const currentPath = location.pathname === '/dashboard' ? 'Dashboard' : 'LabelVerify'

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
          <NavLink aria-label="LabelVerify dashboard" className="brand-home" onClick={closeMenu} to="/dashboard">
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
            {primaryNavigation.filter((item) => canCreateInspection || item.to !== '/inspections/new').map((item) => (
              <NavigationLink item={item} key={item.to} onNavigate={closeMenu} />
            ))}
            {isSupervisor && configurationNavigation.map((item) => (
              <NavigationLink item={item} key={item.to} onNavigate={closeMenu} />
            ))}
            {isSupervisor && <NavigationLink item={{ to: '/master-labels', label: 'Master labels', icon: Database }} onNavigate={closeMenu} />}
            {isSupervisor && <NavigationLink item={{ to: '/users', label: 'Users', icon: UserRoundCog }} onNavigate={closeMenu} />}
            {isSupervisor && <NavigationLink item={{ to: '/settings', label: 'Settings', icon: Settings }} onNavigate={closeMenu} />}
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
      <nav aria-label="Mobile navigation" className="mobile-bottom-nav">
        <NavLink end className={({ isActive }) => `mobile-bottom-nav-link${isActive ? ' active' : ''}`} to="/dashboard"><Home size={17} /><span>Home</span></NavLink>
        <NavLink end className={({ isActive }) => `mobile-bottom-nav-link${isActive ? ' active' : ''}`} to="/inspections"><History size={17} /><span>History</span></NavLink>
        {isSupervisor && <NavLink className={({ isActive }) => `mobile-bottom-nav-link${isActive ? ' active' : ''}`} to="/label-profiles"><SlidersHorizontal size={17} /><span>Config</span></NavLink>}
        <NavLink className={({ isActive }) => `mobile-bottom-nav-link${isActive ? ' active' : ''}`} to="/profile"><UserRoundCog size={17} /><span>Profile</span></NavLink>
      </nav>
    </div>
  )
}

export default AppLayout
