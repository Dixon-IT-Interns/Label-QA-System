import { useState } from 'react'
import { Link, Navigate, useNavigate } from 'react-router-dom'
import { Eye, EyeOff, IdCard, LockKeyhole } from 'lucide-react'
import BrandLogo from '../components/BrandLogo.jsx'
import useAuth from '../hooks/useAuth.js'
import { getAuthErrorMessage } from '../services/authApi.js'

function Login() {
  const { isAuthenticated, isLoading, login } = useAuth()
  const navigate = useNavigate()
  const [form, setForm] = useState({ identifier: '', password: '' })
  const [rememberMe, setRememberMe] = useState(false)
  const [showPassword, setShowPassword] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')

  if (isAuthenticated) return <Navigate replace to="/dashboard" />
  if (isLoading) return <main className="login-shell login-page-shell"><div className="login-card login-page-card" role="status">Checking secure session…</div></main>

  async function submit(event) {
    event.preventDefault()
    setBusy(true)
    setError('')
    try {
      await login(form.identifier, form.password, rememberMe)
      navigate('/dashboard', { replace: true })
    } catch (requestError) {
      setError(getAuthErrorMessage(requestError, 'Sign-in failed.'))
    } finally {
      setBusy(false)
    }
  }

  return (
    <main className="login-shell login-page-shell">
      <form className="login-card login-page-card configuration-form" onSubmit={submit}>
        <div className="login-brand"><BrandLogo className="login-brand-logo" /></div>
        <div className="login-heading"><h1>Secure Access</h1><p>Sign in to continue</p></div>
        <div className="login-field-row">
          <div aria-hidden="true" className="login-field-icon"><IdCard size={22} strokeWidth={2} /></div>
          <div className="form-field login-field"><label htmlFor="login-identifier">EMP_ID/Email</label><div className="login-input-wrap"><input autoComplete="username" id="login-identifier" onChange={(event) => setForm({ ...form, identifier: event.target.value })} placeholder="Enter EMP_ID or email" required value={form.identifier} /></div></div>
        </div>
        <div className="login-field-row">
          <div aria-hidden="true" className="login-field-icon"><LockKeyhole size={21} strokeWidth={2} /></div>
          <div className="form-field login-field"><label htmlFor="login-password">Password</label><div className="login-input-wrap"><input autoComplete="current-password" id="login-password" onChange={(event) => setForm({ ...form, password: event.target.value })} placeholder="Enter your password" required type={showPassword ? 'text' : 'password'} value={form.password} /><button aria-label={showPassword ? 'Hide password' : 'Show password'} className="login-password-toggle" onClick={() => setShowPassword((visible) => !visible)} type="button">{showPassword ? <EyeOff size={16} /> : <Eye size={16} />}</button></div></div>
        </div>
        <div className="login-options"><label className="form-checkbox login-remember"><input checked={rememberMe} onChange={(event) => setRememberMe(event.target.checked)} type="checkbox" /><span>Remember me</span></label><Link to="/forgot-password">Forgot password?</Link></div>
        {error && <div className="configuration-alert" role="alert">{error}</div>}
        <button className="button button-primary login-submit" disabled={busy} type="submit">{busy ? 'Signing in…' : 'Login →'}</button>
      </form>
    </main>
  )
}

export default Login
