import { useCallback, useEffect, useState } from 'react'
import { Pencil, Plus, Trash2, Users } from 'lucide-react'
import EmptyState from '../components/EmptyState.jsx'
import Modal from '../components/Modal.jsx'
import PageHeader from '../components/PageHeader.jsx'
import useAuth from '../hooks/useAuth.js'
import { getAdminErrorMessage, getUsers, saveUser, deleteUser } from '../services/adminApi.js'
import { getBrands } from '../services/configurationApi.js'

const blankUser = {
  fullName: '',
  employeeId: '',
  email: '',
  password: '',
  confirmPassword: '',
  role: 'Operator',
  brandId: '',
}

function loadUsersAndBrands() {
  return Promise.all([getUsers(), getBrands()])
}

function UserManagement() {
  const { user: currentUser } = useAuth()
  const isAdmin = currentUser?.role === 'Admin'
  const [users, setUsers] = useState([])
  const [brands, setBrands] = useState([])
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  const [editing, setEditing] = useState(null)
  const [dialogOpen, setDialogOpen] = useState(false)
  const [form, setForm] = useState(blankUser)

  const refresh = useCallback(async () => {
    try {
      const [userRows, brandRows] = await loadUsersAndBrands()
      setUsers(userRows)
      setBrands(brandRows)
      setError('')
    } catch (requestError) {
      setError(getAdminErrorMessage(requestError))
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    let active = true
    loadUsersAndBrands()
      .then(([userRows, brandRows]) => {
        if (active) {
          setUsers(userRows)
          setBrands(brandRows)
          setError('')
        }
      })
      .catch((requestError) => {
        if (active) setError(getAdminErrorMessage(requestError))
      })
      .finally(() => {
        if (active) setLoading(false)
      })

    return () => { active = false }
  }, [])

  function openForm(account = null) {
    setEditing(account?.id ?? null)
    setDialogOpen(true)
    setForm(account
      ? {
          fullName: account.fullName,
          employeeId: account.employeeId,
          email: account.email,
          password: '',
          confirmPassword: '',
          role: account.role,
          brandId: account.brandId ?? '',
        }
      : { ...blankUser })
    setError('')
  }

  async function submit(event) {
    event.preventDefault()
    setSaving(true)
    setError('')
    try {
      if ((!editing || form.password) && form.password !== form.confirmPassword) {
        setError('Passwords do not match.')
        return
      }

      if (isAdmin && !form.brandId) {
        setError('Select a Brand for Supervisor and Operator accounts.')
        return
      }

      const payload = {
        fullName: form.fullName,
        employeeId: form.employeeId,
        email: form.email,
        password: form.password || null,
        role: isAdmin ? form.role : 'Operator',
      }
      if (isAdmin) payload.brandId = form.brandId

      await saveUser(payload, editing)
      setDialogOpen(false)
      setLoading(true)
      await refresh()
    } catch (requestError) {
      setError(getAdminErrorMessage(requestError))
    } finally {
      setSaving(false)
    }
  }

  async function remove(account) {
    if (!window.confirm(`Permanently delete "${account.employeeId}"? Inspection history will be preserved.`)) return
    setSaving(true)
    setError('')
    try {
      await deleteUser(account.id)
      setLoading(true)
      await refresh()
    } catch (requestError) {
      setError(getAdminErrorMessage(requestError))
    } finally {
      setSaving(false)
    }
  }

  const brandNameById = new Map(brands.map((brand) => [brand.id, brand.name]))

  return <>
    <PageHeader
      eyebrow="Administration"
      title="User management"
      description={isAdmin
        ? 'Create and manage Supervisor and Operator accounts across all Brands.'
        : 'Create and manage Operators assigned to your Brand.'}
      actions={<button className="button button-primary" onClick={() => openForm()} type="button"><Plus size={15} /> {isAdmin ? 'Add user' : 'Add Operator'}</button>}
    />
    {error && !dialogOpen && <div className="configuration-alert" role="alert">{error}</div>}
    <section className="panel">
      <header className="panel-header">
        <div>
          <h2 className="panel-title">Application users</h2>
          <p className="panel-subtitle">Passwords are stored as secure hashes and are never displayed.</p>
        </div>
        <button className="button button-secondary" disabled={loading || saving} onClick={() => { setLoading(true); void refresh() }} type="button">Refresh</button>
      </header>
      {loading ? <div className="configuration-loading" role="status">Loading users…</div>
        : users.length === 0 ? <EmptyState description={error || 'No user accounts are available.'} icon={Users} title="No users found" />
          : <div className="table-wrap">
            <table className="data-table">
              <thead><tr><th>Name</th><th>Employee ID</th><th>Email</th><th>Role</th><th>Brand</th><th>Created</th><th>Last sign-in</th><th /></tr></thead>
              <tbody>{users.map((account) => {
                const canEdit = isAdmin || account.role === 'Operator'
                return <tr key={account.id}>
                  <td className="table-primary-cell">{account.fullName}</td>
                  <td>{account.employeeId}</td>
                  <td>{account.email}</td>
                  <td>{account.role}</td>
                  <td>{account.brandId ? brandNameById.get(account.brandId) || 'Assigned Brand' : '—'}</td>
                  <td>{new Date(account.createdAtUtc).toLocaleString()}</td>
                  <td>{account.lastLoginAtUtc ? new Date(account.lastLoginAtUtc).toLocaleString() : 'Never'}</td>
                  <td className="table-actions">
                    {canEdit && <button aria-label={`Edit ${account.fullName}`} className="icon-button" disabled={saving} onClick={() => openForm(account)} type="button"><Pencil size={14} /></button>}
                    {isAdmin && <button aria-label={`Permanently delete ${account.fullName}`} className="icon-button danger-icon" disabled={saving} onClick={() => void remove(account)} type="button"><Trash2 size={14} /></button>}
                  </td>
                </tr>
              })}</tbody>
            </table>
          </div>}
    </section>
    {dialogOpen && <Modal onClose={() => setDialogOpen(false)} title={editing ? 'Edit user' : isAdmin ? 'Add user' : 'Add Operator'}>
      <UserForm
        brands={brands}
        error={error}
        form={form}
        isAdmin={isAdmin}
        editing={Boolean(editing)}
        onCancel={() => setDialogOpen(false)}
        saving={saving}
        setForm={setForm}
        submit={submit}
      />
    </Modal>}
  </>
}

function UserForm({ brands, error, form, isAdmin, editing, onCancel, saving, setForm, submit }) {
  const update = (key, value) => setForm((current) => ({ ...current, [key]: value }))
  const roleOptions = ['Supervisor', 'Operator']

  function updateRole(role) {
    setForm((current) => ({ ...current, role }))
  }

  return <form className="configuration-form" onSubmit={submit}>
    <label className="form-field"><span>Full name</span><input autoFocus maxLength="150" minLength="2" onChange={(event) => update('fullName', event.target.value)} required value={form.fullName} /></label>
    <label className="form-field"><span>Employee ID</span><input maxLength="100" onChange={(event) => update('employeeId', event.target.value)} pattern="[A-Za-z0-9._-]+" required value={form.employeeId} /></label>
    <label className="form-field"><span>Email</span><input maxLength="256" onChange={(event) => update('email', event.target.value)} required type="email" value={form.email} /></label>
    {isAdmin
      ? <label className="form-field"><span>Role</span><select onChange={(event) => updateRole(event.target.value)} required value={form.role}>{roleOptions.map((role) => <option key={role} value={role}>{role}</option>)}</select></label>
      : <label className="form-field"><span>Role</span><input disabled readOnly value="Operator" /></label>}
    {isAdmin && <label className="form-field">
      <span>Brand</span>
      <select onChange={(event) => update('brandId', event.target.value)} required value={form.brandId}>
        <option value="">Select a Brand</option>
        {brands.map((brand) => <option key={brand.id} value={brand.id}>{brand.name} ({brand.code}){brand.isActive ? '' : ' — Inactive'}</option>)}
      </select>
    </label>}
    {!isAdmin && <p className="panel-subtitle">The Operator will be assigned to your Brand automatically.</p>}
    <label className="form-field"><span>{editing ? 'New password (optional)' : 'Password'}</span><input autoComplete="new-password" minLength="12" onChange={(event) => update('password', event.target.value)} required={!editing} type="password" value={form.password} /><small>Use at least 12 characters.</small></label>
    <label className="form-field"><span>Confirm password</span><input autoComplete="new-password" minLength="12" onChange={(event) => update('confirmPassword', event.target.value)} required={!editing || Boolean(form.password)} type="password" value={form.confirmPassword} /></label>
    {error && <div className="configuration-alert" role="alert">{error}</div>}
    <div className="form-actions"><button className="button button-secondary" disabled={saving} onClick={onCancel} type="button">Cancel</button><button className="button button-primary" disabled={saving} type="submit">{saving ? 'Saving…' : 'Save user'}</button></div>
  </form>
}

export default UserManagement
