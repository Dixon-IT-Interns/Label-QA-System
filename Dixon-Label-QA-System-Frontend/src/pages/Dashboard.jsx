import {
  ClipboardCheck,
  Database,
  FileCog,
  History,
  Laptop,
  Settings,
  ShieldCheck,
  Tags,
  UserRoundCog,
  Activity,
} from 'lucide-react'
import { Link, Navigate } from 'react-router-dom'
import { useEffect, useState } from 'react'
import { getDashboard } from '../services/adminApi.js'
import useAuth from '../hooks/useAuth.js'

const supervisorCards = [
  { label: 'MASTER LABEL', description: 'Manage approved reference labels', to: '/master-labels', icon: Database, tone: 'blue' },
  { label: 'MODEL MANAGEMENT', description: 'Manage laptop models and mappings', to: '/models', icon: Laptop, tone: 'blue' },
  { label: 'INSPECTION HISTORY', description: 'Search and review QA records', to: '/inspections', icon: History, tone: 'blue' },
  { label: 'USER MANAGEMENT', description: 'Manage Operators assigned to your Brand', to: '/users', icon: UserRoundCog, tone: 'dark' },
  { label: 'OPERATOR DASHBOARD', description: 'Open the operator inspection workspace', to: '/inspections/new', icon: ClipboardCheck, tone: 'dark' },
]

const adminCards = [
  { label: 'BRAND MANAGEMENT', description: 'Create and manage system Brands', to: '/brands', icon: Tags, tone: 'dark' },
  { label: 'MASTER LABEL', description: 'Manage approved reference labels', to: '/master-labels', icon: Database, tone: 'blue' },
  { label: 'MASTER CONFIG', description: 'Configure OCR fields and rules', to: '/label-profiles', icon: FileCog, tone: 'dark' },
  { label: 'MODEL MANAGEMENT', description: 'Manage laptop models and mappings', to: '/models', icon: Laptop, tone: 'blue' },
  { label: 'USER MANAGEMENT', description: 'Manage Supervisor and Operator accounts', to: '/users', icon: UserRoundCog, tone: 'dark' },
  { label: 'INSPECTION HISTORY', description: 'Search and review all QA records', to: '/inspections', icon: History, tone: 'blue' },
  { label: 'ADMIN SETTINGS', description: 'Configure OCR and system settings', to: '/settings', icon: Settings, tone: 'dark' },
  { label: 'NEW INSPECTION', description: 'Create and process a label inspection', to: '/inspections/new', icon: ClipboardCheck, tone: 'blue' },
]

function PortalCard({ card }) {
  const Icon = card.icon
  return (
    <Link className={`portal-card portal-card-${card.tone}`} to={card.to}>
      <span className="portal-card-icon"><Icon size={29} strokeWidth={1.8} /></span>
      <span className="portal-card-copy">
        <strong>{card.label}</strong>
        <small>{card.description}</small>
      </span>
    </Link>
  )
}

function Dashboard() {
  const { user } = useAuth()
  const [activities, setActivities] = useState([])

  if (user?.role === 'Operator') return <Navigate replace to="/inspections/new" />
  const isAdmin = user?.role === 'Admin'
  const cards = isAdmin ? adminCards : supervisorCards

  useEffect(() => {
    let active = true
    const refresh = async () => {
      try {
        const data = await getDashboard()
        if (active) setActivities(data.activities || [])
      } catch { /* dashboard cards remain usable if the activity poll fails */ }
    }
    void refresh()
    const timer = window.setInterval(refresh, 5000)
    return () => { active = false; window.clearInterval(timer) }
  }, [])

  return (
    <section className="mes-dashboard">
      <div className="mes-dashboard-topline">
        <div>
          <h1>{isAdmin ? 'Admin Dashboard' : 'Supervisor Dashboard'}</h1>
          <p>Dixon Lens · Master data, verification and inspection control</p>
        </div>
        <div className="mes-session">
          <ShieldCheck size={17} />
          <span><strong>{user?.fullName || user?.role || 'User'}</strong><small>{user?.role || 'User'} · Active session</small></span>
        </div>
      </div>

      <div className="portal-layout">
        <section className="portal-cards" aria-label={`${user?.role || 'User'} modules`}>
          {cards.map((card) => <PortalCard card={card} key={card.to} />)}
        </section>

        <aside className="portal-status-panel">
          <div className="portal-status-header">
            <div><span>DIXON LENS</span><h2>Live Activity Logs</h2></div>
            <span className="portal-live"><i /> LIVE</span>
          </div>
          <div className={`portal-status-body ${activities.length ? 'has-activity-logs' : ''}`}>
            {activities.length === 0
              ? <><div className="portal-status-icon"><Activity size={42} strokeWidth={1.5} /></div><h3>No recent activity</h3><p>New inspection and administration events will appear here automatically.</p></>
              : <ul className="dashboard-activity-list" aria-live="polite">{activities.map((activity) => (
                <li key={activity.id}>
                  <span className="dashboard-activity-mark"><Activity size={14} /></span>
                  <span className="dashboard-activity-copy">
                    <strong>{activity.action.replaceAll('_', ' ')}</strong>
                    <span>{activity.userName} · {activity.role} · {activity.brandName || 'All brands'}</span>
                    <span>{activity.details}{activity.inspectionNumber ? ` · Inspection ${activity.inspectionNumber}` : ''}</span>
                  </span>
                  <time dateTime={activity.createdAtUtc}>{new Date(activity.createdAtUtc).toLocaleTimeString()}</time>
                </li>
              ))}</ul>}
          </div>
        </aside>
      </div>
    </section>
  )
}

export default Dashboard
