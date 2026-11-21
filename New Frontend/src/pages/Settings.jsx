import { useEffect, useState } from 'react'
import { Save, Settings as SettingsIcon } from 'lucide-react'
import PageHeader from '../components/PageHeader.jsx'
import { getAdminErrorMessage, getSettings, saveOcrConfidence } from '../services/adminApi.js'

function Settings() {
  const [confidence, setConfidence] = useState('0.85')
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')

  useEffect(() => {
    let active = true
    getSettings().then((settings) => {
      const current = settings.find((setting) => setting.key === 'OcrMinimumConfidence')
      if (active && current?.value) setConfidence(current.value)
    }).catch((requestError) => { if (active) setError(getAdminErrorMessage(requestError)) })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [])

  async function submit(event) {
    event.preventDefault()
    setSaving(true)
    setError('')
    setNotice('')
    try {
      const saved = await saveOcrConfidence(Number(confidence))
      setConfidence(saved.value)
      setNotice('OCR confidence threshold saved.')
    } catch (requestError) { setError(getAdminErrorMessage(requestError)) }
    finally { setSaving(false) }
  }

  return <>
    <PageHeader eyebrow="Administration" title="System settings" description="Configure safeguards used by inspection processing." />
    {error && <div className="configuration-alert" role="alert">{error}</div>}
    {notice && <div className="configuration-notice" role="status">{notice}</div>}
    <section className="panel">
      <header className="panel-header"><div><h2 className="panel-title"><SettingsIcon size={16} /> Inspection processing</h2><p className="panel-subtitle">The global threshold applies only to fields without a profile-specific threshold.</p></div></header>
      <form className="configuration-form settings-form" onSubmit={submit}>
        <label className="form-field"><span>Minimum global OCR confidence</span><input disabled={loading} max="1" min="0" onChange={(event) => setConfidence(event.target.value)} required step="0.01" type="number" value={confidence} /><small>Range 0.00–1.00. This applies when a profile field does not set its own threshold; otherwise the profile field threshold is used.</small></label>
        <div className="form-actions"><button className="button button-primary" disabled={loading || saving} type="submit"><Save size={14} />{saving ? 'Saving…' : 'Save settings'}</button></div>
      </form>
    </section>
  </>
}

export default Settings
