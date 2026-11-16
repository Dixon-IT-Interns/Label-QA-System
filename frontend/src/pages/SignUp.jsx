import { useState } from 'react'
import { Link } from 'react-router-dom'
import { ShieldCheck } from 'lucide-react'
import { getAuthErrorMessage, signUp } from '../services/authApi.js'

function SignUp() {
  const [form, setForm] = useState({ fullName: '', username: '', email: '', password: '', confirmPassword: '' })
  const [error, setError] = useState('')
  const [created, setCreated] = useState(false)
  const [busy, setBusy] = useState(false)

  async function submit(event) {
    event.preventDefault()
    setError('')
    if (form.password !== form.confirmPassword) {
      setError('Passwords do not match.')
      return
    }

    setBusy(true)
    try {
      await signUp({
        fullName: form.fullName,
        username: form.username,
        email: form.email,
        password: form.password,
      })
      setCreated(true)
    } catch (requestError) {
      setError(getAuthErrorMessage(requestError, 'Could not create your account. Please try again.'))
    } finally {
      setBusy(false)
    }
  }

  return (
    <main className="login-shell">
      <form className="login-card configuration-form" onSubmit={submit}>
        <div className="login-brand"><span className="brand-mark"><ShieldCheck size={20} /></span><span>LabelVerify</span></div>
        <div className="login-heading"><h1>Create account</h1><p>Register for a LabelVerify Operator account.</p></div>
        {created ? <>
          <div className="configuration-notice" role="status">Your account is ready. Sign in with your email or username.</div>
          <Link className="button button-primary login-submit auth-link-button" to="/login">Go to sign in</Link>
        </> : <>
          <div className="form-field login-field"><label htmlFor="signup-name">Full name</label><div className="login-input-wrap"><input autoComplete="name" id="signup-name" maxLength="150" minLength="2" onChange={(event) => setForm({ ...form, fullName: event.target.value })} required value={form.fullName} /></div></div>
          <div className="form-field login-field"><label htmlFor="signup-username">Username</label><div className="login-input-wrap"><input autoComplete="username" id="signup-username" maxLength="100" minLength="3" onChange={(event) => setForm({ ...form, username: event.target.value })} pattern="[A-Za-z0-9._-]+" required value={form.username} /></div></div>
          <div className="form-field login-field"><label htmlFor="signup-email">Email address</label><div className="login-input-wrap"><input autoComplete="email" id="signup-email" maxLength="256" onChange={(event) => setForm({ ...form, email: event.target.value })} required type="email" value={form.email} /></div></div>
          <div className="form-field login-field"><label htmlFor="signup-password">Password (at least 12 characters)</label><div className="login-input-wrap"><input autoComplete="new-password" id="signup-password" maxLength="256" minLength="12" onChange={(event) => setForm({ ...form, password: event.target.value })} required type="password" value={form.password} /></div></div>
          <div className="form-field login-field"><label htmlFor="signup-confirm">Confirm password</label><div className="login-input-wrap"><input autoComplete="new-password" id="signup-confirm" onChange={(event) => setForm({ ...form, confirmPassword: event.target.value })} required type="password" value={form.confirmPassword} /></div></div>
          {error && <div className="configuration-alert" role="alert">{error}</div>}
          <button className="button button-primary login-submit" disabled={busy} type="submit">{busy ? 'Creating account…' : 'Create account'}</button>
          <p className="login-signup-prompt">Already have an account? <Link to="/login">Sign in</Link></p>
        </>}
      </form>
    </main>
  )
}

export default SignUp
