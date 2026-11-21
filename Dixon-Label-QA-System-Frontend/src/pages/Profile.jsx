import { ChevronRight, CircleHelp, Info, LogOut, Settings, ShieldCheck } from 'lucide-react'
import { Link, useNavigate } from 'react-router-dom'
import BrandLogo from '../components/BrandLogo.jsx'
import useAuth from '../hooks/useAuth.js'

function Profile() {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const initials = user?.fullName?.trim().split(/\s+/).slice(0, 2).map((part) => part[0]).join('').toUpperCase() || 'U'
  const isAdmin = user?.role === 'Admin'

  function signOut() {
    logout()
    navigate('/', { replace: true })
  }

  return (
    <section className="profile-page">
      <header className="profile-page-heading">
        <div><h1>Profile</h1><p>Your account and app options</p></div>
        {isAdmin && <Link aria-label="System settings" className="icon-button" to="/settings"><Settings size={17} /></Link>}
      </header>
      <section className="panel profile-account-card">
        <div aria-hidden="true" className="profile-avatar">{initials}</div>
        <h2>{user?.fullName || 'Dixon Lens user'}</h2>
        <p>{user?.email || user?.username}</p>
        <span className="profile-role"><ShieldCheck size={13} /> {user?.role || 'User'}</span>
      </section>
      <nav aria-label="Profile options" className="panel profile-options">
        {isAdmin && <Link to="/settings"><Settings size={17} /><span>Settings</span><ChevronRight size={15} /></Link>}
        <details>
          <summary><Info size={17} /><span>About App</span><ChevronRight size={15} /></summary>
          <p>Dixon Lens helps teams inspect product labels with OCR extraction and field-by-field comparison.</p>
        </details>
        <details>
          <summary><CircleHelp size={17} /><span>Help &amp; Support</span><ChevronRight size={15} /></summary>
          <p>For access or inspection issues, contact your Dixon Lens administrator.</p>
        </details>
        <button className="profile-logout" onClick={signOut} type="button"><LogOut size={17} /><span>Log out</span><ChevronRight size={15} /></button>
      </nav>
      <footer className="profile-version">
        <BrandLogo className="profile-version-logo" />
        <span>Label Evaluation and Verification System · v1.0.0</span>
      </footer>
    </section>
  )
}

export default Profile
