import { useEffect, useState } from 'react'
import { Activity, ArrowRight, ClipboardList, Layers3, ScanLine, ShieldCheck } from 'lucide-react'
import { Link } from 'react-router-dom'
import EmptyState from '../components/EmptyState.jsx'
import StatusPill from '../components/StatusPill.jsx'
import { getAdminErrorMessage, getDashboard } from '../services/adminApi.js'

function Dashboard() {
  const [data, setData] = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    let active = true
    getDashboard().then((result) => { if (active) setData(result) })
      .catch((requestError) => { if (active) setError(getAdminErrorMessage(requestError)) })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [])

  const metrics = [
    { label: "Today's inspections", value: data?.inspectionsToday, note: 'Created since midnight UTC', icon: ClipboardList },
    { label: 'Pass rate', value: data ? `${data.passRatePercent}%` : null, note: data ? `${data.passCount} inspections passed` : 'Available when dashboard data loads', icon: ShieldCheck },
    { label: 'Review required', value: data?.reviewRequiredCount, note: 'Require operator attention', icon: Activity },
    { label: 'Active profiles', value: data?.activeProfiles, note: 'Configured label references', icon: Layers3 },
  ]

  return <>
    <section className="dashboard-hero">
      <div className="hero-copy">
        <div className="eyebrow">Manufacturing QA platform</div>
        <h1>Verify every CO label <span>with confidence.</span></h1>
        <p>Extract critical label information, compare Master and Actual labels, and identify mismatches before production.</p>
        <div className="hero-actions">
          <Link className="button button-primary" to="/inspections/new">Start new inspection <ArrowRight size={15} /></Link>
          <Link className="button button-hero-secondary" to="/inspections">View inspection history</Link>
        </div>
        <div className="hero-footnote"><span className="hero-live-dot" /> Precision capture · OCR extraction · Verified comparison</div>
      </div>
      <div aria-hidden="true" className="hero-label-art">
        <div className="hero-label-paper">
          <div className="label-art-top"><span>QUALITY CONTROL</span><ScanLine size={17} /></div>
          <div className="label-art-line label-art-line-long" />
          <div className="label-art-line label-art-line-mid" />
          <div className="label-art-code"><div className="decorative-qr">{Array.from({ length: 49 }, (_, index) => <i key={index} />)}</div><div className="label-art-barcode"><i /><i /><i /><i /><i /><i /><i /><i /><i /><i /><i /><i /><i /><i /><i /><i /></div></div>
          <div className="label-art-confirm"><ShieldCheck size={16} /> Verification ready</div>
        </div>
        <span className="hero-art-index">LV / 01</span>
      </div>
    </section>
    {error && <div className="configuration-alert" role="alert">{error}</div>}
    <section aria-label="Inspection metrics" className="metric-grid">
      {metrics.map(({ label, value, note, icon: Icon }) => <article className="panel metric-card" key={label}>
        <div className="metric-card-top"><span className="metric-label">{label}</span><Icon size={17} strokeWidth={1.7} /></div>
        <div className="metric-value">{loading ? '…' : value ?? '—'}</div><div className="metric-note">{note}</div>
      </article>)}
    </section>
    <section className="panel dashboard-recent">
      <header className="panel-header"><div><div className="eyebrow">Latest activity</div><h2 className="panel-title">Recent inspections</h2><p className="panel-subtitle">Most recent records from the inspection database</p></div>
        <Link className="button button-secondary" to="/inspections">View all inspections <ArrowRight size={14} /></Link></header>
      {loading ? <div className="configuration-loading" role="status">Loading inspection metrics…</div>
        : data?.recentInspections?.length ? <div className="table-wrap"><table className="data-table">
          <thead><tr><th>Inspection ID</th><th>Brand / model</th><th>Created</th><th>Overall result</th></tr></thead>
          <tbody>{data.recentInspections.map((row) => <tr key={row.id}><td><Link className="table-link" to={`/inspections/${row.id}`}>{row.inspectionNumber}</Link></td>
            <td>{row.brandName}<small className="table-secondary-cell">{row.modelName} · {row.modelNumber}</small></td>
            <td>{new Date(row.createdAtUtc).toLocaleString()}</td><td><StatusPill status={row.status} /></td></tr>)}</tbody>
        </table></div>
        : <EmptyState description={error || 'No saved inspection records are available yet.'} title="No inspection records" />}
      <footer className="dashboard-data-foot"><span>Inspection records are loaded live from your workspace.</span><span>{loading ? '—' : data?.totalInspections ?? '—'} total records</span></footer>
    </section>
  </>
}

export default Dashboard
