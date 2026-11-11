import { useEffect, useState } from 'react'
import { AlertCircle, ClipboardCheck, FileImage, Pencil, Save, XCircle } from 'lucide-react'
import { useParams } from 'react-router-dom'
import EmptyState from '../components/EmptyState.jsx'
import PageHeader from '../components/PageHeader.jsx'
import Modal from '../components/Modal.jsx'
import StatusPill from '../components/StatusPill.jsx'
import {
  cancelInspection,
  correctInspectionField,
  downloadInspectionReport,
  getInspection,
  getInspectionErrorMessage,
  getInspectionImage,
} from '../services/inspectionApi.js'

const imageTitles = {
  MASTER_CO: 'Master CO',
  ACTUAL_CO: 'Actual CO',
  MASTER_CONFIG: 'Master Config',
  ACTUAL_CONFIG: 'Actual Config',
}
const stageTitles = {
  CO_COMPARISON: 'CO comparison',
  CONFIG_COMPARISON: 'Config comparison',
  SERIAL_VALIDATION: 'Serial validation',
}
const ocrImageTitles = {
  MASTER_CO: 'Master CO',
  ACTUAL_CO: 'Actual CO',
  MASTER_CONFIG: 'Master Config',
  ACTUAL_CONFIG: 'Actual Config',
}
const displayFieldName = (fieldKey) => ({
  MODEL_NO: 'Model No.',
  QUEUE_NO: 'Queue No.',
}[fieldKey] || fieldKey)

function parseOcrEvidence(json) {
  if (!json) return { lines: [], fieldCandidates: [], barcodes: [], warnings: [] }
  try {
    const parsed = JSON.parse(json)
    if (Array.isArray(parsed)) return { lines: parsed, fieldCandidates: [], barcodes: [], warnings: [] }
    return {
      lines: Array.isArray(parsed.lines) ? parsed.lines : [],
      fieldCandidates: Array.isArray(parsed.configuredFieldCandidates) ? parsed.configuredFieldCandidates : [],
      barcodes: Array.isArray(parsed.barcodes) ? parsed.barcodes : [],
      warnings: Array.isArray(parsed.warnings) ? parsed.warnings : [],
      imageQuality: parsed.imageQuality ?? null,
    }
  } catch {
    return null
  }
}

function InspectionDetails() {
  const { inspectionId } = useParams()
  const [details, setDetails] = useState(null)
  const [imageUrls, setImageUrls] = useState({})
  const [loadedInspectionId, setLoadedInspectionId] = useState(null)
  const [loadAttempt, setLoadAttempt] = useState(0)
  const [notFound, setNotFound] = useState(false)
  const [busy, setBusy] = useState(false)
  const [reportBusy, setReportBusy] = useState(false)
  const [correctionBusy, setCorrectionBusy] = useState(false)
  const [correction, setCorrection] = useState(null)
  const [notice, setNotice] = useState('')
  const [error, setError] = useState('')

  useEffect(() => {
    let active = true
    const urls = []
    getInspection(inspectionId).then(async (data) => {
      if (active) {
        setDetails(data)
        setError('')
        setNotFound(false)
      }
      const entries = await Promise.all(data.images.map(async (image) => {
        const blob = await getInspectionImage(inspectionId, image.id)
        const url = URL.createObjectURL(blob)
        urls.push(url)
        return [image.id, url]
      }))
      if (active) setImageUrls(Object.fromEntries(entries))
    }).catch((requestError) => {
      if (active) {
        setError(getInspectionErrorMessage(requestError))
        setNotFound(requestError.response?.status === 404)
      }
    })
      .finally(() => { if (active) setLoadedInspectionId(inspectionId) })
    return () => {
      active = false
      urls.forEach((url) => URL.revokeObjectURL(url))
    }
  }, [inspectionId, loadAttempt])

  async function cancel() {
    if (!window.confirm('Cancel this inspection? The saved history and uploaded images will be retained.')) return
    setBusy(true)
    setError('')
    try {
      await cancelInspection(inspectionId)
      setDetails(await getInspection(inspectionId))
    } catch (requestError) {
      setError(getInspectionErrorMessage(requestError))
    } finally {
      setBusy(false)
    }
  }

  async function downloadReport() {
    setReportBusy(true)
    setError('')
    try {
      const blob = await downloadInspectionReport(inspectionId)
      const url = URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url
      link.download = `${item.inspectionNumber}.pdf`
      link.click()
      window.setTimeout(() => URL.revokeObjectURL(url), 1000)
    } catch (requestError) {
      setError(getInspectionErrorMessage(requestError))
    } finally {
      setReportBusy(false)
    }
  }

  async function saveCorrection(event) {
    event.preventDefault()
    setCorrectionBusy(true)
    setError('')
    setNotice('')
    try {
      const updated = await correctInspectionField(
        inspectionId, correction.imageId, correction.fieldDefinitionId,
        { value: correction.value, reason: correction.reason },
      )
      setDetails(updated)
      setCorrection(null)
      setNotice('Manual correction was saved and all comparison stages were recalculated.')
    } catch (requestError) {
      setError(getInspectionErrorMessage(requestError))
    } finally {
      setCorrectionBusy(false)
    }
  }

  const loading = loadedInspectionId !== inspectionId
  if (loading) {
    return <><PageHeader eyebrow="Inspection record" title="Inspection details" /><div className="panel configuration-loading" role="status">Loading inspection…</div></>
  }
  if (!details || details.inspection.id !== inspectionId) {
    return <><PageHeader eyebrow="Inspection record" title="Inspection details" />{error && <div className="configuration-alert" role="alert">{error}</div>}
      {notFound
        ? <EmptyState description={`No inspection record is available for ${inspectionId}.`} icon={ClipboardCheck} title="Inspection not found" />
        : <div className="panel configuration-loading"><p>Inspection details could not be loaded.</p><button className="button button-secondary" onClick={() => { setError(''); setLoadAttempt((attempt) => attempt + 1) }} type="button">Retry</button></div>}
    </>
  }

  const item = details.inspection
  const canCancel = ['READY', 'REVIEW_REQUIRED', 'BLOCKED', 'ERROR'].includes(item.status)
  const orderedImages = Object.keys(imageTitles).map((type) => details.images.find((image) => image.imageType === type)).filter(Boolean)

  return (
    <>
      <PageHeader
        description={`${item.brandName} · ${item.modelName} · created ${new Date(item.createdAtUtc).toLocaleString()}`}
        eyebrow={item.inspectionNumber}
        title="Inspection details"
        actions={<>
          <button className="button button-secondary" disabled={reportBusy || ['READY', 'PROCESSING'].includes(item.status)} onClick={() => void downloadReport()} type="button">
            {reportBusy ? 'Generating…' : 'Download PDF report'}
          </button>
          {canCancel && <button className="button button-secondary" disabled={busy} onClick={() => void cancel()} type="button"><XCircle size={14} /> Cancel inspection</button>}
        </>}
      />
      {error && <div className="configuration-alert" role="alert">{error}</div>}
      {notice && <div className="configuration-notice" role="status">{notice}</div>}
      <section className="panel inspection-detail-summary">
        <div><span>Overall status</span><StatusPill status={item.status} /></div>
        <div><span>CO status</span><StatusPill status={item.coStatus} /></div>
        <div><span>Config status</span><StatusPill status={item.configStatus} /></div>
        <div><span>Serial status</span><StatusPill status={item.serialStatus} /></div>
        <div><span>Production order</span><strong>{item.productionOrderNumber || '—'}</strong></div>
        <div><span>Work order</span><strong>{item.workOrderNumber || '—'}</strong></div>
        <div><span>CO profile version</span><strong>{details.labelProfileCOVersion ? `v${details.labelProfileCOVersion}` : '—'}</strong></div>
        <div><span>Config profile version</span><strong>{details.labelProfileConfigVersion ? `v${details.labelProfileConfigVersion}` : '—'}</strong></div>
      </section>

      {details.finalRemarks && <div className="callout inspection-detail-callout"><AlertCircle size={16} /><span>{details.finalRemarks}</span></div>}

      <section className="panel inspection-details-panel">
        <header className="panel-header"><div><h2 className="panel-title">Uploaded label images</h2><p className="panel-subtitle">Original uploaded files are preserved as stored evidence.</p></div></header>
        {orderedImages.length === 0 ? <EmptyState description="No image files have been uploaded." icon={FileImage} title="Images unavailable" />
          : <div className="inspection-detail-images">{orderedImages.map((image) => (
            <article className="inspection-detail-image-card" key={image.id}>
              {imageUrls[image.id] ? <img alt={`${imageTitles[image.imageType]} label`} src={imageUrls[image.id]} /> : <div className="inspection-image-placeholder">Loading preview…</div>}
              <strong>{imageTitles[image.imageType]}</strong>
              <small>{image.originalFileName} · {image.width} × {image.height} · {(image.fileSizeBytes / 1024 / 1024).toFixed(2)} MB</small>
              <small>SHA-256: {image.sha256Hash}</small>
            </article>
          ))}</div>}
      </section>

      <section className="panel inspection-details-panel">
        <header className="panel-header"><div><h2 className="panel-title">OCR evidence</h2><p className="panel-subtitle">Raw extracted text, configured field candidates, confidence, and barcode evidence.</p></div></header>
        {details.ocrResults.length === 0 ? <EmptyState description="OCR evidence is saved after processing begins." icon={FileImage} title="No OCR results" />
          : <div className="ocr-evidence-list">{details.ocrResults.map((result) => {
            const evidence = parseOcrEvidence(result.extractedFieldsJson)
            return <article className="ocr-evidence-card" key={result.id}>
              <header><div><h3>{ocrImageTitles[result.imageType] || result.imageType}</h3><small>{result.engine} {result.engineVersion} · {result.processingDurationMs ?? '—'} ms · Confidence {result.overallConfidence == null ? '—' : `${(result.overallConfidence * 100).toFixed(0)}%`}</small></div><StatusPill status={result.status} /></header>
              {evidence ? <>
                {evidence.imageQuality && <div className="inspection-help">Image quality: {evidence.imageQuality.width} × {evidence.imageQuality.height}px · sharpness {evidence.imageQuality.blur_score} · brightness {evidence.imageQuality.brightness_mean} · contrast {evidence.imageQuality.contrast_stddev}</div>}
                {evidence.fieldCandidates.length > 0 && <div className="table-wrap"><table className="data-table"><thead><tr><th>Configured field</th><th>Detected value</th><th>Confidence</th><th>Candidate status</th><th>Reason</th></tr></thead><tbody>
                  {evidence.fieldCandidates.map((candidate, index) => <tr key={`${candidate.fieldName}-${index}`}><td>{displayFieldName(candidate.fieldName)}</td><td>{candidate.value ?? 'Not detected'}</td><td>{`${(candidate.confidence * 100).toFixed(0)}%`}</td><td>{candidate.status}</td><td>{candidate.reason || '—'}{candidate.debug && <details><summary>OCR debug</summary><pre>{JSON.stringify(candidate.debug, null, 2)}</pre></details>}</td></tr>)}
                </tbody></table></div>}
                {result.extractedFields.length > 0 && <div className="table-wrap"><table className="data-table"><thead><tr><th>Saved field</th><th>Value</th><th>Confidence</th><th>Correction</th><th /></tr></thead><tbody>
                  {result.extractedFields.map((field, index) => <tr key={`${field.fieldKey}-${index}`}><td>{displayFieldName(field.fieldKey)}</td><td>{field.extractedValue ?? 'Unreadable'}</td>
                    <td>{(field.confidence * 100).toFixed(0)}%</td><td>{field.isManuallyCorrected ? `Manual: ${field.correctionReason}` : 'OCR'}</td>
                    <td>{field.fieldDefinitionId && ['REVIEW_REQUIRED', 'FAIL'].includes(item.status) && <button className="button button-secondary" onClick={() => setCorrection({
                      imageId: result.imageId,
                      fieldDefinitionId: field.fieldDefinitionId,
                      fieldKey: field.fieldKey,
                      value: field.extractedValue ?? '',
                      reason: '',
                    })} type="button"><Pencil size={13} /> Correct</button>}</td></tr>)}
                </tbody></table></div>}
                {evidence.barcodes.length > 0 && <div className="ocr-barcode-list"><strong>Barcodes</strong>{evidence.barcodes.map((barcode, index) => <div key={`${barcode.format}-${index}`}>{barcode.format}: {barcode.value}</div>)}</div>}
                {evidence.warnings.map((warning, index) => <div className="inspection-help" key={`${warning}-${index}`}><AlertCircle size={14} />{warning}</div>)}
                <details><summary>OCR lines ({evidence.lines.length})</summary><ul className="ocr-line-list">{evidence.lines.map((line, index) => <li key={`${line.text}-${index}`}><span>{line.text}</span><small>{(line.confidence * 100).toFixed(0)}% confidence</small></li>)}</ul></details>
              </> : <div className="configuration-alert" role="alert">Saved OCR evidence has an unrecognized format: {result.rawExtractedText || 'No raw text saved.'}</div>}
            </article>
          })}</div>}
      </section>

      <section className="inspection-stage-grid">
        {['CO_COMPARISON', 'CONFIG_COMPARISON', 'SERIAL_VALIDATION'].map((type) => {
          const stages = details.stageResults.filter((stage) => stage.stageType === type)
          const stage = stages.at(-1)
          return <article className="panel inspection-stage-card" key={type}>
            <header><div><h2 className="panel-title">{stageTitles[type]}</h2><StatusPill status={type === 'CO_COMPARISON' ? item.coStatus : type === 'CONFIG_COMPARISON' ? item.configStatus : item.serialStatus} /></div></header>
            {stage ? <>
              <p>{stage.remarks || 'No remarks recorded.'}</p>
              <div className="stage-counts"><span>Fields: {stage.totalFields}</span><span>Matched: {stage.matchedFields}</span><span>Mismatch: {stage.mismatchedFields}</span><span>Unreadable: {stage.unreadableFields}</span></div>
              {stage.comparisons.length > 0 && <div className="table-wrap"><table className="data-table"><thead><tr><th>Field</th><th>{stage.stageType === 'SERIAL_VALIDATION' ? 'Actual CO serial' : 'Master'}</th><th>{stage.stageType === 'SERIAL_VALIDATION' ? 'Actual Config serial' : 'Actual'}</th><th>Strategy</th><th>Result</th><th>Reason</th></tr></thead><tbody>
                {stage.comparisons.map((comparison) => <tr key={comparison.fieldKey}>
                  <td>{displayFieldName(comparison.fieldKey)}</td>
                  <td>{comparison.masterValue ?? 'Unreadable'}{comparison.normalizedMasterValue && <small className="table-secondary">Normalized: {comparison.normalizedMasterValue}</small>}</td>
                  <td>{comparison.actualValue ?? 'Unreadable'}{comparison.normalizedActualValue && <small className="table-secondary">Normalized: {comparison.normalizedActualValue}</small>}</td>
                  <td>{comparison.comparisonStrategy}</td><td><StatusPill status={comparison.status} /></td><td>{comparison.mismatchReason || '—'}</td>
                </tr>)}
              </tbody></table></div>}
            </> : <p className="inspection-stage-empty">Stage has not been run.</p>}
          </article>
        })}
      </section>

      <section className="panel inspection-details-panel">
        <header className="panel-header"><div><h2 className="panel-title">Audit log</h2><p className="panel-subtitle">Inspection actions and state changes.</p></div></header>
        {details.logs.length === 0 ? <EmptyState description="No audit events have been recorded." icon={ClipboardCheck} title="No audit history" />
          : <div className="table-wrap"><table className="data-table"><thead><tr><th>Time (UTC)</th><th>Action</th><th>Description</th><th>Old value</th><th>New value</th><th>IP address</th></tr></thead><tbody>
            {details.logs.map((log) => <tr key={log.id}><td>{new Date(log.createdAtUtc).toLocaleString()}</td><td>{log.action}</td><td>{log.description}</td><td>{log.oldValue || '—'}</td><td>{log.newValue || '—'}</td><td>{log.ipAddress || '—'}</td></tr>)}
          </tbody></table></div>}
      </section>
      {correction && <Modal onClose={() => setCorrection(null)} title={`Correct ${displayFieldName(correction.fieldKey)}`}>
        <form className="configuration-form" onSubmit={saveCorrection}>
          <label className="form-field"><span>Verified value</span><input autoFocus maxLength="2000" onChange={(event) => setCorrection({ ...correction, value: event.target.value })} required value={correction.value} /></label>
          <label className="form-field"><span>Correction reason</span><textarea maxLength="1000" minLength="5" onChange={(event) => setCorrection({ ...correction, reason: event.target.value })} required rows="3" value={correction.reason} /></label>
          <p className="modal-description">This correction is audited and the inspection comparisons are recalculated using saved OCR evidence.</p>
          <div className="form-actions"><button className="button button-secondary" onClick={() => setCorrection(null)} type="button">Cancel</button><button className="button button-primary" disabled={correctionBusy} type="submit"><Save size={14} />{correctionBusy ? 'Saving…' : 'Save correction'}</button></div>
        </form>
      </Modal>}
    </>
  )
}

export default InspectionDetails
