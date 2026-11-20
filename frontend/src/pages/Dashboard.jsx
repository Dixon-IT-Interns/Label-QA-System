import { useEffect, useState } from 'react'
import { ArrowRight, CheckCircle2, CircleX, Clock3, ClipboardList, Database } from 'lucide-react'
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
  const canCreateInspection = user?.role === 'Supervisor' || user?.role === 'Operator'

  useEffect(() => {
    let active = true
    getDashboard().then((result) => { if (active) setData(result) })
      .catch((requestError) => { if (active) setError(getAdminErrorMessage(requestError)) })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [])

  const firstName = user?.fullName?.trim().split(/\s+/)[0] || 'there'
  const failedRate = data?.totalInspections
    ? `${((data.failCount / data.totalInspections) * 100).toFixed(1)}% of inspections`
    : 'Failed inspections'
  const metrics = [
    { label: 'Total inspections', value: data?.totalInspections, note: user?.role === 'Supervisor' ? 'All recorded inspections' : 'Your recorded inspections', icon: ClipboardList, tone: 'primary' },
    { label: 'Pass rate', value: data ? `${data.passRatePercent}%` : null, note: data ? `${data.passCount} passed` : 'Overall results', icon: CheckCircle2, tone: 'success' },
    { label: 'Failed', value: data?.failCount, note: data ? failedRate : 'Failed inspections', icon: CircleX, tone: 'danger' },
    { label: 'Pending review', value: data?.reviewRequiredCount, note: 'Awaiting operator review', icon: Clock3, tone: 'neutral' },
  ]

  return <section className="dashboard-page">
    <header className="dashboard-welcome">
      <div>
        <h1>Hello, {firstName}</h1>
        <p>Here&apos;s what&apos;s happening today</p>
      </div>
      {canCreateInspection && <Link className="button button-primary" to="/inspections/new">New inspection <ArrowRight size={15} /></Link>}
    </header>
    <figure className="dashboard-banner-image">
      <img alt="Manufacturing QA Platform banner with a QR code" src={dashboardBanner} />
      {canCreateInspection && <Link
        aria-label="Open New Inspection from the QR code"
        className="dashboard-banner-qr-link"
        to="/inspections/new"
      />}
    </figure>
    {user?.role === 'Supervisor' && <section className="panel dashboard-master-labels">
      <div className="dashboard-master-labels-icon"><Database size={19} /></div>
      <div><h2>Master Labels</h2><p>Configure the reference COO and Config labels that are automatically used for each model&apos;s inspections.</p></div>
      <Link className="button button-secondary" to="/master-labels">Manage master labels <ArrowRight size={14} /></Link>
    </section>}
    {error && <div className="configuration-alert" role="alert">{error}</div>}
    <section aria-label="Inspection summary" className="dashboard-stat-grid">
      {metrics.map(({ label, value, note, icon: Icon, tone }) => <article className={`panel dashboard-stat-card is-${tone}`} key={label}>
        <div className="dashboard-stat-top"><span>{label}</span><Icon size={17} strokeWidth={1.8} /></div>
        <strong>{loading ? '…' : value ?? '—'}</strong>
        <small>{note}</small>
      </article>)}
    </section>
    <section className="panel dashboard-recent">
      <header className="dashboard-section-heading">
        <div><h2>Recent activity</h2><p>Your latest label inspections</p></div>
        <Link to="/inspections">View all <ArrowRight size={14} /></Link>
      </header>
      {loading ? <div className="dashboard-list-message" role="status">Loading recent activity…</div>
        : data?.recentInspections?.length ? <ul className="dashboard-activity-list">
          {data.recentInspections.slice(0, 5).map((inspection) => {
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
}

export default Dashboard
