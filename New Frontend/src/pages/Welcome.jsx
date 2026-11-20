import { ArrowRight, ScanLine, ShieldCheck } from 'lucide-react'
import { Navigate, Link } from 'react-router-dom'
import useAuth from '../hooks/useAuth.js'

function Welcome() {
  const { isAuthenticated, isLoading } = useAuth()

  if (isLoading) return <main className="welcome-shell"><div className="configuration-loading" role="status">Preparing LabelVerify…</div></main>
  if (isAuthenticated) return <Navigate replace to="/dashboard" />

  return (
    <main className="welcome-shell">
      <div className="welcome-page">
        <header className="welcome-header">
          <Link aria-label="LabelVerify home" className="welcome-brand" to="/">
            <span className="brand-mark"><ShieldCheck size={21} strokeWidth={2.2} /></span>
            <span>LabelVerify</span>
          </Link>
          <Link className="welcome-signin" to="/login">Sign in</Link>
        </header>
        <section className="welcome-content">
          <div className="welcome-copy">
            <div className="welcome-product-mark"><ShieldCheck size={36} strokeWidth={1.8} /></div>
            <div className="eyebrow">OCR label QA system</div>
            <h1>Scan. Verify.<br /><span>Ensure quality.</span></h1>
            <p>Make every label check clear, consistent, and easy to trust.</p>
            <Link className="button button-primary welcome-cta" to="/login">Get started <ArrowRight size={16} /></Link>
            <div className="welcome-assurance"><span /> Precision capture · Verified comparison</div>
          </div>
          <div aria-hidden="true" className="welcome-visual">
            <div className="welcome-orbit welcome-orbit-one" />
            <div className="welcome-orbit welcome-orbit-two" />
            <div className="welcome-label">
              <div className="welcome-label-brand"><span className="welcome-hp-mark">hp</span><span>QUALITY CONTROL</span></div>
              <div className="welcome-label-copy"><i /><i /><i /></div>
              <div className="welcome-label-codes">
                <div className="welcome-qr">
                  {Array.from({ length: 49 }, (_, index) => <i key={index} />)}
                </div>
                <div className="welcome-barcode">
                  {Array.from({ length: 23 }, (_, index) => <i key={index} />)}
                </div>
              </div>
              <div className="welcome-label-footer"><ScanLine size={14} /> Inspection ready</div>
            </div>
            <div className="welcome-scan-badge"><ShieldCheck size={17} /><span>Verified<br /><strong>with confidence</strong></span></div>
          </div>
        </section>
        <footer className="welcome-footer"><span>LabelVerify</span><span>Accurate label inspections, made simple.</span></footer>
      </div>
    </main>
  )
}

export default Welcome
