import { useCallback, useEffect, useState } from 'react'
import { Pencil, Plus, Users } from 'lucide-react'
import EmptyState from '../components/EmptyState.jsx'
import Modal from '../components/Modal.jsx'
import PageHeader from '../components/PageHeader.jsx'
import { getAdminErrorMessage, getUsers, saveUser } from '../services/adminApi.js'

const blankUser = { fullName: '', username: '', email: '', role: 'Operator', password: '', isActive: true }

function UserManagement() {
  const [users, setUsers] = useState([])
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  const [editing, setEditing] = useState(null)
  const [dialogOpen, setDialogOpen] = useState(false)
  const [form, setForm] = useState(blankUser)

  const refresh = useCallback(async () => {
    setLoading(true)
    setError('')
    try { setUsers(await getUsers()) } catch (requestError) { setError(getAdminErrorMessage(requestError)) }
    finally { setLoading(false) }
  }, [])

  useEffect(() => { void refresh() }, [refresh])

  function openForm(user = null) {
    setEditing(user?.id ?? null)
    setDialogOpen(true)
    setForm(user
      ? { fullName: user.fullName, username: user.username, email: user.email, role: user.role, password: '', isActive: user.isActive }
      : blankUser)
    setError('')
  }

  async function submit(event) {
    event.preventDefault()
    setSaving(true)
    setError('')
    try {
      await saveUser({ ...form, password: form.password || null }, editing)
      setDialogOpen(false)
      await refresh()
    } catch (requestError) { setError(getAdminErrorMessage(requestError)) }
    finally { setSaving(false) }
  }

  return <>
    <PageHeader eyebrow="Administration" title="User management" description="Manage operator accounts, access roles, and account availability."
      actions={<button className="button button-primary" onClick={() => openForm()} type="button"><Plus size={15} /> Add user</button>} />
    {error && !editing && <div className="configuration-alert" role="alert">{error}</div>}
    <section className="panel">
      <header className="panel-header"><div><h2 className="panel-title">Application users</h2><p className="panel-subtitle">Passwords are stored as secure hashes and are never displayed.</p></div>
        <button className="button button-secondary" disabled={loading} onClick={() => void refresh()} type="button">Refresh</button></header>
      {loading ? <div className="configuration-loading" role="status">Loading users…</div>
        : users.length === 0 ? <EmptyState description={error || 'No user accounts are available.'} icon={Users} title="No users found" />
          : <div className="table-wrap"><table className="data-table"><thead><tr><th>Name</th><th>Username</th><th>Email</th><th>Role</th><th>Status</th><th>Last sign-in</th><th /></tr></thead>
            <tbody>{users.map((user) => <tr key={user.id}><td className="table-primary-cell">{user.fullName}</td><td>{user.username}</td><td>{user.email}</td><td>{user.role}</td>
              <td><span className={`status-pill ${user.isActive ? 'status-success' : 'status-muted'}`}>{user.isActive ? 'Active' : 'Inactive'}</span></td>
              <td>{user.lastLoginAtUtc ? new Date(user.lastLoginAtUtc).toLocaleString() : 'Never'}</td>
              <td><button aria-label={`Edit ${user.fullName}`} className="icon-button" onClick={() => openForm(user)} type="button"><Pencil size={14} /></button></td></tr>)}</tbody>
          </table></div>}
    </section>
    {dialogOpen && <Modal onClose={() => setDialogOpen(false)} title={editing ? 'Edit user' : 'Add user'}>
      <UserForm form={form} setForm={setForm} submit={submit} saving={saving} error={error}
        editing={Boolean(editing)} onCancel={() => setDialogOpen(false)} />
    </Modal>}
  </>
}

function UserForm({ form, setForm, submit, saving, error, editing = false, onCancel }) {
  const update = (key, value) => setForm((current) => ({ ...current, [key]: value }))
  return <form className="configuration-form" onSubmit={submit}>
    <label className="form-field"><span>Full name</span><input autoFocus maxLength="150" minLength="2" onChange={(event) => update('fullName', event.target.value)} required value={form.fullName} /></label>
    <label className="form-field"><span>Username</span><input maxLength="100" onChange={(event) => update('username', event.target.value)} pattern="[A-Za-z0-9._-]+" required value={form.username} /></label>
    <label className="form-field"><span>Email</span><input maxLength="256" onChange={(event) => update('email', event.target.value)} required type="email" value={form.email} /></label>
    <label className="form-field"><span>{editing ? 'New password (optional)' : 'Password'}</span><input autoComplete="new-password" minLength="12" onChange={(event) => update('password', event.target.value)} required={!editing} type="password" value={form.password} /><small>Use at least 12 characters.</small></label>
    <label className="form-field"><span>Role</span><select onChange={(event) => update('role', event.target.value)} value={form.role}><option>Admin</option><option>Operator</option><option>Viewer</option></select></label>
    <label className="form-checkbox"><input checked={form.isActive} onChange={(event) => update('isActive', event.target.checked)} type="checkbox" /><span>Account active</span></label>
    {error && <div className="configuration-alert" role="alert">{error}</div>}
    <div className="form-actions"><button className="button button-secondary" onClick={onCancel} type="button">Cancel</button><button className="button button-primary" disabled={saving} type="submit">{saving ? 'Saving…' : 'Save user'}</button></div>
  </form>
}

export default UserManagement
