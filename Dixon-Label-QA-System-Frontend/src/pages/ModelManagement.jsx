import { useCallback, useEffect, useMemo, useState } from 'react'
import { Laptop, Pencil, Plus, Trash2 } from 'lucide-react'
import EmptyState from '../components/EmptyState.jsx'
import Modal from '../components/Modal.jsx'
import PageHeader from '../components/PageHeader.jsx'
import { deleteModel, getApiErrorMessage, getBrands, getModels, saveModel } from '../services/configurationApi.js'

const emptyForm = { brandId: '', code: '', name: '', description: '', isActive: true }

function ModelManagement() {
  const [brands, setBrands] = useState([])
  const [models, setModels] = useState([])
  const [filterBrandId, setFilterBrandId] = useState('')
  const [form, setForm] = useState(emptyForm)
  const [dialog, setDialog] = useState(null)
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')

  const loadData = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const [brandRows, modelRows] = await Promise.all([getBrands(), getModels(filterBrandId || undefined)])
      setBrands(brandRows)
      setModels(modelRows)
    } catch (requestError) {
      setError(getApiErrorMessage(requestError))
    } finally {
      setLoading(false)
    }
  }, [filterBrandId])

  useEffect(() => {
    let active = true
    Promise.all([getBrands(), getModels(filterBrandId || undefined)])
      .then(([brandRows, modelRows]) => {
        if (active) {
          setBrands(brandRows)
          setModels(modelRows)
        }
      })
      .catch((requestError) => { if (active) setError(getApiErrorMessage(requestError)) })
      .finally(() => { if (active) setLoading(false) })

    return () => { active = false }
  }, [filterBrandId])

  const activeBrands = useMemo(() => brands.filter((brand) => brand.isActive), [brands])

  function openForm(model = null) {
    setDialog(model ? { id: model.id } : { id: null })
    setForm(model
      ? { brandId: model.brandId, code: model.code, name: model.name, description: model.description ?? '', isActive: model.isActive }
      : { ...emptyForm, brandId: filterBrandId || activeBrands[0]?.id || '' })
    setError('')
  }

  async function submit(event) {
    event.preventDefault()
    setBusy(true)
    setError('')
    try {
      await saveModel(form, dialog.id)
      setDialog(null)
      await loadData()
    } catch (requestError) {
      setError(getApiErrorMessage(requestError))
    } finally {
      setBusy(false)
    }
  }

  async function remove(model) {
    if (!window.confirm(`Delete model "${model.name}"?`)) return
    setBusy(true)
    setError('')
    try {
      await deleteModel(model.id)
      await loadData()
    } catch (requestError) {
      setError(getApiErrorMessage(requestError))
    } finally {
      setBusy(false)
    }
  }

  return (
    <>
      <PageHeader
        description="Associate laptop models with brands and their future label profiles."
        eyebrow="Configuration"
        title="Model management"
        actions={
          <button className="button button-primary" disabled={activeBrands.length === 0} onClick={() => openForm()} type="button">
            <Plus size={15} /> Add model
          </button>
        }
      />
      {error && !dialog && <div className="configuration-alert" role="alert">{error}</div>}
      <section className="panel">
        <header className="panel-header configuration-toolbar">
          <div>
            <h2 className="panel-title">Models</h2>
            <p className="panel-subtitle">A model code must be unique within its brand.</p>
          </div>
          <div className="toolbar-actions">
            <select aria-label="Filter models by brand" className="form-control compact-control" onChange={(event) => setFilterBrandId(event.target.value)} value={filterBrandId}>
              <option value="">All brands</option>
              {brands.map((brand) => <option key={brand.id} value={brand.id}>{brand.name}</option>)}
            </select>
            <button className="button button-secondary" disabled={loading || busy} onClick={loadData} type="button">Refresh</button>
          </div>
        </header>
        {loading ? (
          <div className="configuration-loading" role="status">Loading models…</div>
        ) : error && models.length === 0 ? (
          <EmptyState description={error} icon={Laptop} title="Could not load models" />
        ) : models.length === 0 ? (
          <EmptyState
            description={brands.length ? 'Add a model to a configured brand. Model names are not prepopulated.' : 'Create a brand before configuring laptop models.'}
            icon={Laptop}
            title={brands.length ? 'No models configured' : 'No brands available'}
          />
        ) : (
          <div className="table-wrap">
            <table className="data-table">
              <thead><tr><th>Model</th><th>Code</th><th>Brand</th><th>Description</th><th>Status</th><th /></tr></thead>
              <tbody>
                {models.map((model) => (
                  <tr key={model.id}>
                    <td className="table-primary-cell">{model.name}</td>
                    <td><span className="code-chip">{model.code}</span></td>
                    <td>{model.brandName}</td>
                    <td>{model.description || '—'}</td>
                    <td><span className={`status-pill ${model.isActive ? 'status-success' : 'status-muted'}`}>{model.isActive ? 'Active' : 'Inactive'}</span></td>
                    <td className="table-actions">
                      <button aria-label={`Edit ${model.name}`} className="icon-button" disabled={busy} onClick={() => openForm(model)} type="button"><Pencil size={14} /></button>
                      <button aria-label={`Delete ${model.name}`} className="icon-button danger-icon" disabled={busy} onClick={() => remove(model)} type="button"><Trash2 size={14} /></button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      {dialog && (
        <Modal onClose={() => setDialog(null)} title={dialog.id ? 'Edit model' : 'Add model'}>
          <form className="configuration-form" onSubmit={submit}>
            <label className="form-field">
              <span>Brand</span>
              <select onChange={(event) => setForm({ ...form, brandId: event.target.value })} required value={form.brandId}>
                <option value="" disabled>Select brand</option>
                {activeBrands.map((brand) => <option key={brand.id} value={brand.id}>{brand.name}</option>)}
                {dialog.id && brands.find((brand) => brand.id === form.brandId && !brand.isActive) && <option value={form.brandId}>{brands.find((brand) => brand.id === form.brandId).name} (inactive)</option>}
              </select>
            </label>
            <label className="form-field">
              <span>Model name</span>
              <input autoFocus maxLength="200" onChange={(event) => setForm({ ...form, name: event.target.value })} required value={form.name} />
            </label>
            <label className="form-field">
              <span>Model code</span>
              <input maxLength="100" onChange={(event) => setForm({ ...form, code: event.target.value })} pattern="[A-Za-z0-9][A-Za-z0-9._-]*" required value={form.code} />
            </label>
            <label className="form-field">
              <span>Description</span>
              <input maxLength="500" onChange={(event) => setForm({ ...form, description: event.target.value })} value={form.description} />
            </label>
            <label className="form-checkbox">
              <input checked={form.isActive} onChange={(event) => setForm({ ...form, isActive: event.target.checked })} type="checkbox" />
              <span>Model is active</span>
            </label>
            {error && <div className="configuration-alert" role="alert">{error}</div>}
            <div className="form-actions">
              <button className="button button-secondary" disabled={busy} onClick={() => setDialog(null)} type="button">Cancel</button>
              <button className="button button-primary" disabled={busy} type="submit">{busy ? 'Saving…' : 'Save model'}</button>
            </div>
          </form>
        </Modal>
      )}
    </>
  )
}

export default ModelManagement
