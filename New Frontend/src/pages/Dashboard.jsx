import { useEffect, useState } from 'react'
import {
  ArrowRight,
  BarChart3,
  CheckCircle2,
  CircleX,
  Clock3,
  ClipboardCheck,
  Database,
  ScanLine,
  ShieldCheck,
  Users,
} from 'lucide-react'
import { Link } from 'react-router-dom'
import dashboardBanner from '../assets/dashboard-banner.png'
import EmptyState from '../components/EmptyState.jsx'
import StatusPill from '../components/StatusPill.jsx'
import useAuth from '../hooks/useAuth.js'
import { getAdminErrorMessage, getDashboard } from '../services/adminApi.js'

function formatInspectionDate(dateValue) {
  return new Date(dateValue).toLocaleString(undefined, {
    month: 'short',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
  })
}

function Dashboard() {
  const [data, setData] = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const { user } = useAuth()
  const isSupervisor = user?.role === 'Supervisor'
  const canCreateInspection = isSupervisor || user?.role === 'Operator'

  useEffect(() => {
    let active = true
    getDashboard()
      .then((result) => { if (active) setData(result) })
      .catch((requestError) => { if (active) setError(getAdminErrorMessage(requestError)) })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [])

  const firstName = user?.fullName?.trim().split(/\s+/)[0] || 'there'
  const failedRate = data?.totalInspections
    ? `${((data.failCount / data.totalInspections) * 100).toFixed(1)}% of inspections`
    : 'Failed inspections'

  const metrics = [
    { label: 'Total inspections', value: data?.totalInspections, note: isSupervisor ? 'All recorded inspections' : 'Your recorded inspections', icon: ClipboardCheck, tone: 'primary' },
    { label: 'Pass rate', value: data ? `${data.passRatePercent}%` : null, note: data ? `${data.passCount} passed` : 'Overall results', icon: CheckCircle2, tone: 'success' },
    { label: 'Failed', value: data?.failCount, note: data ? failedRate : 'Failed inspections', icon: CircleX, tone: 'danger' },
    { label: 'Pending review', value: data?.reviewRequiredCount, note: 'Awaiting review', icon: Clock3, tone: 'neutral' },
  ]

  const recent = data?.recentInspections?.slice(0, 5) || []

  return (
    <section className={`dashboard-page ${isSupervisor ? 'dashboard-supervisor' : 'dashboard-operator'}`}>
      <section className="dashboard-hero">
        <div className="dashboard-hero-copy">
          <div className="dashboard-role-badge">
            <span className="dashboard-role-dot" />
            {isSupervisor ? 'Supervisor control center' : 'Operator workspace'}
          </div>
          <p className="dashboard-eyebrow">LabelVerify / Dashboard</p>
          <h1>{isSupervisor ? <>Quality control,<br /><em>at a glance.</em></> : <>Ready for your<br /><em>next inspection.</em></>}</h1>
          <p className="dashboard-hero-description">
            {isSupervisor
              ? 'Monitor inspection performance, manage reference labels and keep the QA workflow under control.'
              : 'Run a label inspection, verify the result and keep track of your own inspection history.'}
          </p>
          <div className="dashboard-hero-actions">
            {canCreateInspection && <Link className="button button-primary" to="/inspections/new"><ScanLine size={15} /> New inspection <ArrowRight size={14} /></Link>}
            <Link className="button button-hero-ghost" to="/inspections">View history <ArrowRight size={14} /></Link>
          </div>
        </div>
        <div className="dashboard-hero-visual" aria-hidden="true">
          <div className="dashboard-hero-orbit orbit-one" />
          <div className="dashboard-hero-orbit orbit-two" />
          <div className="dashboard-hero-image-wrap">
            <img src={dashboardBanner} alt="" />
          </div>
          <div className="dashboard-hero-float dashboard-float-top"><ShieldCheck size={14} /><span>QA protected</span></div>
          <div className="dashboard-hero-float dashboard-float-bottom"><span className="dashboard-live-dot" />Live workspace</div>
        </div>
      </section>

      {error && <div className="configuration-alert" role="alert">{error}</div>}

      <section aria-label="Inspection summary" className="dashboard-stat-grid">
        {metrics.map(({ label, value, note, icon: Icon, tone }) => (
          <article className={`panel dashboard-stat-card is-${tone}`} key={label}>
            <div className="dashboard-stat-top"><span>{label}</span><Icon size={17} strokeWidth={1.8} /></div>
            <strong>{loading ? '…' : value ?? '—'}</strong>
            <small>{note}</small>
          </article>
        ))}
      </section>

      {isSupervisor ? (
        <section className="dashboard-control-grid">
          <article className="panel dashboard-control-panel dashboard-master-panel">
            <div className="dashboard-control-icon"><Database size={18} /></div>
            <div className="dashboard-control-copy">
              <span className="dashboard-card-kicker">Reference library</span>
              <h2>Master labels</h2>
              <p>Manage the saved COO and Config references used for model-level validation.</p>
              <Link className="dashboard-inline-link" to="/master-labels">Open master labels <ArrowRight size={13} /></Link>
            </div>
          </article>
          <article className="panel dashboard-control-panel">
            <div className="dashboard-control-icon"><Users size={18} /></div>
            <div className="dashboard-control-copy">
              <span className="dashboard-card-kicker">Administration</span>
              <h2>Operator access</h2>
              <p>Manage operator accounts, access status and system-level permissions.</p>
              <Link className="dashboard-inline-link" to="/users">Manage users <ArrowRight size={13} /></Link>
            </div>
          </article>
          <article className="panel dashboard-control-panel">
            <div className="dashboard-control-icon"><BarChart3 size={18} /></div>
            <div className="dashboard-control-copy">
              <span className="dashboard-card-kicker">Quality activity</span>
              <h2>Inspection history</h2>
              <p>Review every inspection, result and operator activity from one place.</p>
              <Link className="dashboard-inline-link" to="/inspections">Review history <ArrowRight size={13} /></Link>
            </div>
          </article>
        </section>
      ) : (
        <section className="dashboard-operator-workflow panel">
          <div className="dashboard-workflow-copy">
            <span className="dashboard-card-kicker">Inspection workflow</span>
            <h2>Three steps to a verified label</h2>
            <p>Select the model, upload the actual COO and Config labels, then review the comparison result.</p>
          </div>
          <div className="dashboard-workflow-steps">
            <div><span>01</span><strong>Identify</strong><small>Brand · Model · PO</small></div>
            <div><span>02</span><strong>Upload</strong><small>COO + Config</small></div>
            <div><span>03</span><strong>Verify</strong><small>OCR + comparison</small></div>
          </div>
        </section>
      )}

      <section className="panel dashboard-recent">
        <header className="dashboard-section-heading">
          <div>
            <span className="dashboard-card-kicker">Activity</span>
            <h2>{isSupervisor ? 'Latest system inspections' : 'Your latest inspections'}</h2>
            <p>{isSupervisor ? 'A quick view of the most recent QA activity.' : 'Your recent label verification activity.'}</p>
          </div>
          <Link to="/inspections">View all <ArrowRight size={14} /></Link>
        </header>
        {loading ? <div className="dashboard-list-message" role="status">Loading recent activity…</div>
          : recent.length ? <ul className="dashboard-activity-list">
            {recent.map((inspection) => {
              const status = String(inspection.status ?? '').toLowerCase()
              const isFail = status === 'fail'
              const isReview = status === 'review_required'
              const ActivityIcon = isFail ? CircleX : isReview ? Clock3 : CheckCircle2
              const tone = isFail ? ' is-fail' : isReview ? ' is-review' : ''
              return <li key={inspection.id}>
                <span aria-hidden="true" className={`dashboard-activity-mark${tone}`}><ActivityIcon size={17} /></span>
                <div className="dashboard-activity-copy">
                  <Link to={`/inspections/${inspection.id}`}>{inspection.inspectionNumber}</Link>
                  <span>{inspection.brandName} · {inspection.modelName} · {formatInspectionDate(inspection.createdAtUtc)}</span>
                </div>
                <StatusPill status={inspection.status} />
              </li>
            })}
          </ul>
          : <EmptyState description={error || 'Saved inspections will appear here.'} title="No recent activity" />}
      </section>
    </section>
  )
}

export default Dashboard
