import { useEffect, useState } from 'react'
import { Activity, ArrowRight, ClipboardList, Layers3, ScanLine, ShieldCheck } from 'lucide-react'
import { Link } from 'react-router-dom'
import EmptyState from '../components/EmptyState.jsx'
import PageHeader from '../components/PageHeader.jsx'
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
    { label: 'Inspections today', value: data?.inspectionsToday, note: 'Created since midnight UTC', icon: ClipboardList },
    { label: 'Pass rate', value: data ? `${data.passRatePercent}%` : null, note: `${data?.passCount ?? 0} inspections passed`, icon: ShieldCheck },
    { label: 'Awaiting review', value: data?.reviewRequiredCount, note: 'Require operator attention', icon: Activity },
    { label: 'Configured profiles', value: data?.activeProfiles, note: 'Active model label profiles', icon: Layers3 },
  ]

  return <>
    <PageHeader description="Monitor label verification activity and access your inspection workspace."
      eyebrow="Operations overview" title="Dashboard"
      actions={<Link className="button button-primary" to="/inspections/new"><ScanLine size={15} /> New inspection</Link>} />
    {error && <div className="configuration-alert" role="alert">{error}</div>}
    <section aria-label="Inspection metrics" className="metric-grid">
      {metrics.map(({ label, value, note, icon: Icon }) => <article className="panel metric-card" key={label}>
        <div style={{ display: 'flex', justifyContent: 'space-between' }}><span className="metric-label">{label}</span><Icon color="#91a2c6" size={16} strokeWidth={1.8} /></div>
        <div className="metric-value">{loading ? '…' : value ?? '—'}</div><div className="metric-note">{note}</div>
      </article>)}
    </section>
    <div className="dashboard-grid">
      <section className="panel">
        <header className="panel-header"><div><h2 className="panel-title">Recent inspections</h2><p className="panel-subtitle">Latest records from the inspection database</p></div>
          <Link className="button button-secondary" to="/inspections">View history <ArrowRight size={14} /></Link></header>
        {loading ? <div className="configuration-loading" role="status">Loading inspection metrics…</div>
          : data?.recentInspections?.length ? <div className="table-wrap"><table className="data-table">
            <thead><tr><th>Inspection</th><th>Brand / model</th><th>Created</th><th>Status</th></tr></thead>
            <tbody>{data.recentInspections.map((row) => <tr key={row.id}><td><Link className="table-link" to={`/inspections/${row.id}`}>{row.inspectionNumber}</Link></td>
              <td>{row.brandName}<small className="table-secondary-cell">{row.modelName} · {row.modelNumber}</small></td>
              <td>{new Date(row.createdAtUtc).toLocaleString()}</td><td><StatusPill status={row.status} /></td></tr>)}</tbody>
          </table></div>
          : <EmptyState description={error || 'No saved inspection records are available yet.'} title="No inspection records" />}
      </section>
      <section className="panel">
        <header className="panel-header"><div><h2 className="panel-title">Inspection outcomes</h2><p className="panel-subtitle">Live totals, not sample data</p></div></header>
        <div className="panel-body">
          {[['TOTAL INSPECTIONS', data?.totalInspections], ['PASS', data?.passCount], ['FAIL', data?.failCount], ['REVIEW REQUIRED', data?.reviewRequiredCount]].map(([label, count]) =>
            <div className="health-row" key={label}><div className="health-service">{label}</div><strong>{loading ? '…' : count ?? '—'}</strong></div>)}
          <div className="health-row"><div><div className="health-service"><Activity size={14} /> Inspection service</div><div className="health-description">Dashboard response is database-backed.</div></div>
            <StatusPill label={error ? 'Unavailable' : loading ? 'Checking' : 'Connected'} status={error ? 'unavailable' : loading ? 'unavailable' : 'PASS'} /></div>
        </div>
      </section>
    </div>
  </>
}

export default Dashboard
