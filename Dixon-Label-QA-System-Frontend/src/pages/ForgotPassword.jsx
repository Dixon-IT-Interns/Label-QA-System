import { useState } from 'react'
import { Link } from 'react-router-dom'
import { ArrowLeft } from 'lucide-react'
import BrandLogo from '../components/BrandLogo.jsx'
import { getAuthErrorMessage, requestPasswordReset } from '../services/authApi.js'

function ForgotPassword() {
  const [email, setEmail] = useState('')
  const [message, setMessage] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  async function submit(event) {
    event.preventDefault()
    setBusy(true)
    setError('')
    setMessage('')
    try {
      const result = await requestPasswordReset(email)
      setMessage(result.message)
    } catch (requestError) {
      setError(getAuthErrorMessage(requestError, 'Could not request a reset link. Please try again later.'))
    } finally {
      setBusy(false)
    }
  }

  return (
    <main className="login-shell">
      <form className="login-card configuration-form" onSubmit={submit}>
        <div className="login-brand"><BrandLogo className="login-brand-logo" /></div>
        <div className="login-heading"><h1>Forgot password?</h1><p>Enter your account email and we&apos;ll send a reset link.</p></div>
        <div className="form-field login-field"><label htmlFor="reset-email">Email address</label><div className="login-input-wrap"><input autoComplete="email" id="reset-email" onChange={(event) => setEmail(event.target.value)} required type="email" value={email} /></div></div>
        {error && <div className="configuration-alert" role="alert">{error}</div>}
        {message && <div className="configuration-notice" role="status">{message}</div>}
        <button className="button button-primary login-submit" disabled={busy} type="submit">{busy ? 'Sending…' : 'Send reset link'}</button>
        <Link className="auth-back-link" to="/login"><ArrowLeft size={14} /> Back to sign in</Link>
      </form>
    </main>
  )
}

export default ForgotPassword
