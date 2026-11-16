import { useCallback, useState } from 'react'
import { Link, Navigate, useNavigate } from 'react-router-dom'
import { Eye, EyeOff, LockKeyhole, Mail, ShieldCheck } from 'lucide-react'
import GoogleSignInButton from '../components/GoogleSignInButton.jsx'
import useAuth from '../hooks/useAuth.js'
import { getAuthErrorMessage } from '../services/authApi.js'

function Login() {
  const { isAuthenticated, isLoading, login, loginWithGoogle } = useAuth()
  const navigate = useNavigate()
  const [form, setForm] = useState({ identifier: '', password: '' })
  const [rememberMe, setRememberMe] = useState(false)
  const [showPassword, setShowPassword] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')

  const handleGoogleCredential = useCallback(async (credential) => {
    setBusy(true)
    setError('')
    try {
      await loginWithGoogle(credential, rememberMe)
      navigate('/dashboard', { replace: true })
    } catch (requestError) {
      setError(getAuthErrorMessage(requestError, 'Google sign-in failed.'))
    } finally {
      setBusy(false)
    }
  }, [loginWithGoogle, navigate, rememberMe])

  if (isAuthenticated) return <Navigate replace to="/dashboard" />
  if (isLoading) return <main className="login-shell"><div className="login-card" role="status">Checking secure session…</div></main>

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
    <main className="login-shell">
      <form className="login-card configuration-form" onSubmit={submit}>
        <div className="login-brand"><span className="brand-mark"><ShieldCheck size={20} /></span><span>LabelVerify</span></div>
        <div className="login-heading"><h1>Welcome back <span aria-hidden="true">👋</span></h1><p>Sign in to continue</p></div>
        <div className="form-field login-field"><label htmlFor="login-identifier">Email address or username</label><div className="login-input-wrap"><Mail aria-hidden="true" size={15} /><input autoComplete="username" id="login-identifier" onChange={(event) => setForm({ ...form, identifier: event.target.value })} placeholder="Email address or username" required value={form.identifier} /></div></div>
        <div className="form-field login-field"><label htmlFor="login-password">Password</label><div className="login-input-wrap"><LockKeyhole aria-hidden="true" size={15} /><input autoComplete="current-password" id="login-password" onChange={(event) => setForm({ ...form, password: event.target.value })} placeholder="Password" required type={showPassword ? 'text' : 'password'} value={form.password} /><button aria-label={showPassword ? 'Hide password' : 'Show password'} className="login-password-toggle" onClick={() => setShowPassword((visible) => !visible)} type="button">{showPassword ? <EyeOff size={16} /> : <Eye size={16} />}</button></div></div>
        <div className="login-options"><label className="form-checkbox login-remember"><input checked={rememberMe} onChange={(event) => setRememberMe(event.target.checked)} type="checkbox" /><span>Remember me</span></label><Link to="/forgot-password">Forgot password?</Link></div>
        {error && <div className="configuration-alert" role="alert">{error}</div>}
        <button className="button button-primary login-submit" disabled={busy} type="submit">{busy ? 'Signing in…' : 'Sign in'}</button>
        <div aria-hidden="true" className="login-divider"><span>or</span></div>
        <GoogleSignInButton onCredential={handleGoogleCredential} onError={setError} />
        <p className="login-signup-prompt">Don&apos;t have an account? <Link to="/signup">Sign up</Link></p>
      </form>
    </main>
  )
}

export default Login
