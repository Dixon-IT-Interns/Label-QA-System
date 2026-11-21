import { useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import BrandLogo from '../components/BrandLogo.jsx'
import { getAuthErrorMessage, resetPassword } from '../services/authApi.js'

function ResetPassword() {
  const [searchParams] = useSearchParams()
  const email = searchParams.get('email') ?? ''
  const token = searchParams.get('token') ?? ''
  const [password, setPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [error, setError] = useState('')
  const [complete, setComplete] = useState(false)
  const [busy, setBusy] = useState(false)

  async function submit(event) {
    event.preventDefault()
    setError('')
    if (password !== confirmPassword) {
      setError('Passwords do not match.')
      return
    }

    setBusy(true)
    try {
      await resetPassword({ email, token, password })
      setComplete(true)
    } catch (requestError) {
      setError(getAuthErrorMessage(requestError, 'Could not reset your password. Request a new link and try again.'))
    } finally {
      setBusy(false)
    }
  }

  return (
    <main className="login-shell">
      <form className="login-card configuration-form" onSubmit={submit}>
        <div className="login-brand"><BrandLogo className="login-brand-logo" /></div>
        <div className="login-heading"><h1>Reset password</h1><p>Choose a new password for your account.</p></div>
        {complete ? <>
          <div className="configuration-notice" role="status">Your password has been reset. You can now sign in.</div>
          <Link className="button button-primary login-submit auth-link-button" to="/login">Go to sign in</Link>
        </> : <>
          {!email || !token ? <div className="configuration-alert" role="alert">This reset link is incomplete. Request a new password reset link.</div> : <>
            <div className="form-field login-field"><label htmlFor="new-password">New password (at least 12 characters)</label><div className="login-input-wrap"><input autoComplete="new-password" id="new-password" maxLength="256" minLength="12" onChange={(event) => setPassword(event.target.value)} required type="password" value={password} /></div></div>
            <div className="form-field login-field"><label htmlFor="confirm-new-password">Confirm new password</label><div className="login-input-wrap"><input autoComplete="new-password" id="confirm-new-password" onChange={(event) => setConfirmPassword(event.target.value)} required type="password" value={confirmPassword} /></div></div>
            {error && <div className="configuration-alert" role="alert">{error}</div>}
            <button className="button button-primary login-submit" disabled={busy} type="submit">{busy ? 'Resetting…' : 'Reset password'}</button>
          </>}
        </>}
        {!complete && <Link className="auth-back-link" to="/login">Back to sign in</Link>}
      </form>
    </main>
  )
}

export default ResetPassword
