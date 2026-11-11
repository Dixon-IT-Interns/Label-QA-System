import { useEffect, useState } from 'react'
import { ClipboardList, Search } from 'lucide-react'
import { Link } from 'react-router-dom'
import EmptyState from '../components/EmptyState.jsx'
import PageHeader from '../components/PageHeader.jsx'
import StatusPill from '../components/StatusPill.jsx'
import { getBrands, getModels } from '../services/configurationApi.js'
import { getInspectionErrorMessage, getInspections } from '../services/inspectionApi.js'

const pageSize = 20
const statuses = [
  'READY', 'PROCESSING', 'PASS', 'FAIL', 'REVIEW_REQUIRED', 'BLOCKED', 'CANCELLED', 'ERROR',
]

function InspectionHistory() {
  const [brands, setBrands] = useState([])
  const [models, setModels] = useState([])
  const [rows, setRows] = useState([])
  const [filters, setFilters] = useState({
    search: '', brandId: '', modelId: '', status: '', fromUtc: '', toUtc: '',
    page: 1, sortBy: 'createdAtUtc', descending: true,
  })
  const [total, setTotal] = useState(0)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    let active = true
    Promise.all([getBrands(), getModels()])
      .then(([brandRows, modelRows]) => {
        if (active) { setBrands(brandRows); setModels(modelRows) }
      })
      .catch((requestError) => { if (active) setError(getInspectionErrorMessage(requestError)) })
    return () => { active = false }
  }, [])

  useEffect(() => {
    let active = true
    const params = {
      page: filters.page,
      pageSize,
      sortBy: filters.sortBy,
      descending: filters.descending,
    }
    if (filters.search.trim()) params.search = filters.search.trim()
    if (filters.brandId) params.brandId = filters.brandId
    if (filters.modelId) params.modelId = filters.modelId
    if (filters.status) params.status = filters.status
    if (filters.fromUtc) params.fromUtc = `${filters.fromUtc}T00:00:00Z`
    if (filters.toUtc) params.toUtc = `${filters.toUtc}T23:59:59Z`

    getInspections(params)
      .then((data) => {
        if (active) { setRows(data.items); setTotal(data.totalCount); setError('') }
      })
      .catch((requestError) => { if (active) setError(getInspectionErrorMessage(requestError)) })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [filters])

  function updateFilter(key, value) {
    setLoading(true)
    setFilters((current) => ({ ...current, [key]: value, page: 1 }))
  }

  const filteredModels = models.filter((model) => !filters.brandId || model.brandId === filters.brandId)
  const pageCount = Math.max(1, Math.ceil(total / pageSize))

  return (
    <>
      <PageHeader
        description="Search and review saved inspections, workflow stages, and audit history."
        eyebrow="Quality records"
        title="Inspection history"
      />
      {error && <div className="configuration-alert" role="alert">{error}</div>}
      <section className="panel">
        <header className="panel-header configuration-toolbar">
          <div><h2 className="panel-title">All inspections</h2><p className="panel-subtitle">{total} record{total === 1 ? '' : 's'} · newest first</p></div>
          <label className="history-search">
            <Search aria-hidden="true" color="#98a1af" size={14} />
            <input aria-label="Search inspections" onChange={(event) => updateFilter('search', event.target.value)} placeholder="Inspection, order, work order, serial" value={filters.search} />
          </label>
        </header>
        <div className="history-filters">
          <select aria-label="Brand filter" onChange={(event) => { setLoading(true); setFilters((current) => ({ ...current, brandId: event.target.value, modelId: '', page: 1 })) }} value={filters.brandId}>
            <option value="">All brands</option>{brands.map((brand) => <option key={brand.id} value={brand.id}>{brand.name}</option>)}
          </select>
          <select aria-label="Model filter" onChange={(event) => updateFilter('modelId', event.target.value)} value={filters.modelId}>
            <option value="">All models</option>{filteredModels.map((model) => <option key={model.id} value={model.id}>{model.name}</option>)}
          </select>
          <select aria-label="Status filter" onChange={(event) => updateFilter('status', event.target.value)} value={filters.status}>
            <option value="">All statuses</option>{statuses.map((status) => <option key={status} value={status}>{status.replaceAll('_', ' ')}</option>)}
          </select>
          <label><span>From</span><input aria-label="From date" onChange={(event) => updateFilter('fromUtc', event.target.value)} type="date" value={filters.fromUtc} /></label>
          <label><span>To</span><input aria-label="To date" onChange={(event) => updateFilter('toUtc', event.target.value)} type="date" value={filters.toUtc} /></label>
          <select aria-label="Sort field" onChange={(event) => updateFilter('sortBy', event.target.value)} value={filters.sortBy}>
            <option value="createdAtUtc">Created date</option><option value="inspectionNumber">Inspection number</option><option value="status">Status</option>
          </select>
          <button className="button button-secondary" onClick={() => updateFilter('descending', !filters.descending)} type="button">{filters.descending ? 'Descending' : 'Ascending'}</button>
        </div>
        {loading ? <div className="configuration-loading" role="status">Loading inspections…</div>
          : rows.length === 0 ? <EmptyState description={error || 'No saved inspection records match these filters.'} icon={ClipboardList} title="No inspections found" />
            : <div className="table-wrap">
              <table className="data-table">
                <thead><tr><th>Inspection</th><th>Brand / model</th><th>Production order</th><th>Started</th><th>CO</th><th>Config</th><th>Status</th></tr></thead>
                <tbody>{rows.map((row) => (
                  <tr key={row.id}>
                    <td><Link className="table-link" to={`/inspections/${row.id}`}>{row.inspectionNumber}</Link></td>
                    <td>{row.brandName}<small className="table-secondary-cell">{row.modelName} · {row.modelNumber}</small></td>
                    <td>{row.productionOrderNumber || '—'}<small className="table-secondary-cell">{row.workOrderNumber || 'No work order'}</small></td>
                    <td>{new Date(row.createdAtUtc).toLocaleString()}</td>
                    <td><StatusPill status={row.coStatus} /></td>
                    <td><StatusPill status={row.configStatus} /></td>
                    <td><StatusPill status={row.status} /></td>
                  </tr>
                ))}</tbody>
              </table>
            </div>}
        <footer className="history-pagination">
          <span>Page {filters.page} of {pageCount}</span>
          <div><button className="button button-secondary" disabled={filters.page <= 1 || loading} onClick={() => updateFilter('page', filters.page - 1)} type="button">Previous</button>
            <button className="button button-secondary" disabled={filters.page >= pageCount || loading} onClick={() => updateFilter('page', filters.page + 1)} type="button">Next</button></div>
        </footer>
      </section>
    </>
  )
}

export default InspectionHistory
