import { useCallback, useEffect, useState } from 'react'
import { Boxes, Pencil, Plus, Trash2 } from 'lucide-react'
import EmptyState from '../components/EmptyState.jsx'
import Modal from '../components/Modal.jsx'
import PageHeader from '../components/PageHeader.jsx'
import { deleteBrand, getApiErrorMessage, getBrands, saveBrand } from '../services/configurationApi.js'

const emptyForm = { code: '', name: '', description: '', isActive: true }

function BrandManagement() {
  const [brands, setBrands] = useState([])
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [dialog, setDialog] = useState(null)
  const [form, setForm] = useState(emptyForm)

  const loadBrands = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      setBrands(await getBrands())
    } catch (requestError) {
      setError(getApiErrorMessage(requestError))
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    let active = true
    getBrands()
      .then((items) => { if (active) setBrands(items) })
      .catch((requestError) => { if (active) setError(getApiErrorMessage(requestError)) })
      .finally(() => { if (active) setLoading(false) })

    return () => { active = false }
  }, [])

  function openForm(brand = null) {
    setDialog(brand ? { id: brand.id } : { id: null })
    setForm(brand
      ? { code: brand.code, name: brand.name, description: brand.description ?? '', isActive: brand.isActive }
      : emptyForm)
    setError('')
  }

  async function submit(event) {
    event.preventDefault()
    setBusy(true)
    setError('')
    try {
      await saveBrand(form, dialog.id)
      setDialog(null)
      await loadBrands()
    } catch (requestError) {
      setError(getApiErrorMessage(requestError))
    } finally {
      setBusy(false)
    }
  }

  async function remove(brand) {
    if (!window.confirm(`Delete brand "${brand.name}"?`)) return
    setBusy(true)
    setError('')
    try {
      await deleteBrand(brand.id)
      await loadBrands()
    } catch (requestError) {
      setError(getApiErrorMessage(requestError))
    } finally {
      setBusy(false)
    }
  }

  return (
    <>
      <PageHeader
        description="Create and maintain brands used by models and label profiles."
        eyebrow="Configuration"
        title="Brand management"
        actions={
          <button className="button button-primary" onClick={() => openForm()} type="button">
            <Plus size={15} /> Add brand
          </button>
        }
      />
      {error && !dialog && <div className="configuration-alert" role="alert">{error}</div>}
      <section className="panel">
        <header className="panel-header">
          <div>
            <h2 className="panel-title">Brands</h2>
            <p className="panel-subtitle">Brand records configure the available model and label-profile catalog.</p>
          </div>
          <button className="button button-secondary" disabled={loading || busy} onClick={loadBrands} type="button">
            Refresh
          </button>
        </header>
        {loading ? (
          <div className="configuration-loading" role="status">Loading brands…</div>
        ) : error && brands.length === 0 ? (
          <EmptyState description={error} icon={Boxes} title="Could not load brands" />
        ) : brands.length === 0 ? (
          <EmptyState
            description="Create your first brand to start configuring models and label profiles."
            icon={Boxes}
            title="No brands configured"
          />
        ) : (
          <div className="table-wrap">
            <table className="data-table">
              <thead><tr><th>Brand</th><th>Code</th><th>Description</th><th>Status</th><th>Created</th><th /></tr></thead>
              <tbody>
                {brands.map((brand) => (
                  <tr key={brand.id}>
                    <td className="table-primary-cell">{brand.name}</td>
                    <td><span className="code-chip">{brand.code}</span></td>
                    <td>{brand.description || '—'}</td>
                    <td><span className={`status-pill ${brand.isActive ? 'status-success' : 'status-muted'}`}>{brand.isActive ? 'Active' : 'Inactive'}</span></td>
                    <td>{new Date(brand.createdAtUtc).toLocaleDateString()}</td>
                    <td className="table-actions">
                      <button aria-label={`Edit ${brand.name}`} className="icon-button" disabled={busy} onClick={() => openForm(brand)} type="button"><Pencil size={14} /></button>
                      <button aria-label={`Delete ${brand.name}`} className="icon-button danger-icon" disabled={busy} onClick={() => remove(brand)} type="button"><Trash2 size={14} /></button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      {dialog && (
        <Modal onClose={() => setDialog(null)} title={dialog.id ? 'Edit brand' : 'Add brand'}>
          <form className="configuration-form" onSubmit={submit}>
            <label className="form-field">
              <span>Brand name</span>
              <input autoFocus maxLength="150" onChange={(event) => setForm({ ...form, name: event.target.value })} required value={form.name} />
            </label>
            <label className="form-field">
              <span>Code</span>
              <input maxLength="50" onChange={(event) => setForm({ ...form, code: event.target.value })} pattern="[A-Za-z0-9][A-Za-z0-9._-]*" required value={form.code} />
              <small>Letters, numbers, periods, underscores and hyphens.</small>
            </label>
            <label className="form-field">
              <span>Description</span>
              <input maxLength="500" onChange={(event) => setForm({ ...form, description: event.target.value })} value={form.description} />
            </label>
            <label className="form-checkbox">
              <input checked={form.isActive} onChange={(event) => setForm({ ...form, isActive: event.target.checked })} type="checkbox" />
              <span>Brand is active</span>
            </label>
            {error && <div className="configuration-alert" role="alert">{error}</div>}
            <div className="form-actions">
              <button className="button button-secondary" disabled={busy} onClick={() => setDialog(null)} type="button">Cancel</button>
              <button className="button button-primary" disabled={busy} type="submit">{busy ? 'Saving…' : 'Save brand'}</button>
            </div>
          </form>
        </Modal>
      )}
    </>
  )
}

export default BrandManagement
