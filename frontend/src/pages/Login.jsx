import { useState } from 'react'
import { Navigate, useNavigate } from 'react-router-dom'
import { ShieldCheck } from 'lucide-react'
import useAuth from '../hooks/useAuth.js'

function Login() {
  const { isAuthenticated, isLoading, login } = useAuth()
  const navigate = useNavigate()
  const [form, setForm] = useState({ identifier: '', password: '' })
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')

  if (isAuthenticated) return <Navigate replace to="/" />
  if (isLoading) return <main className="login-shell"><div className="login-card" role="status">Checking secure session…</div></main>

  async function submit(event) {
    event.preventDefault()
    setBusy(true)
    setError('')
    try {
      await login(form.identifier, form.password)
      navigate('/', { replace: true })
    } catch (requestError) {
      setError(requestError.response?.data?.title ?? requestError.response?.data?.detail ?? 'Sign-in failed.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <main className="login-shell">
      <form className="login-card configuration-form" onSubmit={submit}>
        <div className="login-brand"><span className="brand-mark"><ShieldCheck size={20} /></span><span>LabelVerify</span></div>
        <h1>Sign in</h1>
        <p>Use your application username or email and password.</p>
        <label className="form-field"><span>Username or email</span><input autoComplete="username" onChange={(event) => setForm({ ...form, identifier: event.target.value })} required value={form.identifier} /></label>
        <label className="form-field"><span>Password</span><input autoComplete="current-password" onChange={(event) => setForm({ ...form, password: event.target.value })} required type="password" value={form.password} /></label>
        {error && <div className="configuration-alert" role="alert">{error}</div>}
        <button className="button button-primary" disabled={busy} type="submit">{busy ? 'Signing in…' : 'Sign in'}</button>
      </form>
    </main>
  )
}

export default Login
