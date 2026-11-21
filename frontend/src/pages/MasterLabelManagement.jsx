import { useEffect, useMemo, useRef, useState } from 'react'
import { ArrowLeft, Check, FileImage, LoaderCircle, ScanLine, Upload } from 'lucide-react'
import { Link } from 'react-router-dom'
import { getApiErrorMessage, getBrands, getModels } from '../services/configurationApi.js'
import {
  detectMasterLabelModel,
  getMasterLabelImage,
  getMasterLabelsForModel,
  saveMasterLabelPair,
} from '../services/masterLabelApi.js'

const labelTypes = [
  { type: 'CO', title: 'Master COO', description: 'Country-of-origin label and configured CO fields.' },
  { type: 'CONFIG', title: 'Master Config', description: 'Configuration label and configured model/queue fields.' },
]

function MasterLabelManagement() {
  const [brands, setBrands] = useState([])
  const [models, setModels] = useState([])
  const [brandId, setBrandId] = useState('')
  const [modelId, setModelId] = useState('')
  const [modelSearch, setModelSearch] = useState('')
  const [labels, setLabels] = useState([])
  const [loadedModelId, setLoadedModelId] = useState('')
  const [files, setFiles] = useState({})
  const [fileInputVersion, setFileInputVersion] = useState(0)
  const [previews, setPreviews] = useState({})
  const [detectFile, setDetectFile] = useState(null)
  const [loading, setLoading] = useState(true)
  const [savingType, setSavingType] = useState('')
  const [detecting, setDetecting] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const previewUrls = useRef(new Set())
  const detectInput = useRef(null)

  useEffect(() => {
    let active = true
    Promise.all([getBrands(), getModels()])
      .then(([brandRows, modelRows]) => {
        if (!active) return
        setBrands(brandRows.filter((brand) => brand.isActive))
        setModels(modelRows.filter((model) => model.isActive))
      })
      .catch((requestError) => { if (active) setError(getApiErrorMessage(requestError)) })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [])

  const brandModels = useMemo(
    () => models.filter((model) => model.brandId === brandId),
    [models, brandId],
  )
  const selectedModel = models.find((model) => model.id === modelId)
  const masterLoading = Boolean(modelId && loadedModelId !== modelId)
  const currentLabels = loadedModelId === modelId ? labels : []
  const currentPreviews = loadedModelId === modelId ? previews : {}
  const labelsByType = Object.fromEntries(currentLabels.map((label) => [label.labelType, label]))

  useEffect(() => {
    if (!modelId) return undefined
    const controller = new AbortController()
    const modelPreviewUrls = previewUrls.current
    getMasterLabelsForModel(modelId, controller.signal)
      .then(async (rows) => {
        const imageRows = await Promise.all(rows.map(async (label) => {
          try {
            const blob = await getMasterLabelImage(modelId, label.labelType, controller.signal)
            return [label.labelType, URL.createObjectURL(blob)]
          } catch (requestError) {
            if (!controller.signal.aborted) setError(getApiErrorMessage(requestError))
            return null
          }
        }))
        if (controller.signal.aborted) {
          imageRows.forEach((row) => { if (row) URL.revokeObjectURL(row[1]) })
          return
        }
        const nextPreviews = Object.fromEntries(imageRows.filter(Boolean))
        Object.values(nextPreviews).forEach((url) => previewUrls.current.add(url))
        setLabels(rows)
        setPreviews(nextPreviews)
        setLoadedModelId(modelId)
      })
      .catch((requestError) => {
        if (controller.signal.aborted) return
        setError(getApiErrorMessage(requestError))
        setLabels([])
        setPreviews({})
        setLoadedModelId(modelId)
      })
    return () => {
      controller.abort()
      modelPreviewUrls.forEach((url) => URL.revokeObjectURL(url))
      modelPreviewUrls.clear()
    }
  }, [modelId])

  async function detectModel() {
    if (!brandId || !detectFile) {
      setError('Select a brand and choose a label image to detect its model.')
      return
    }
    setDetecting(true)
    setError('')
    setNotice('')
    try {
      const result = await detectMasterLabelModel(brandId, detectFile)
      setModels((current) => current.some((model) => model.id === result.modelId)
        ? current
        : [...current, {
            id: result.modelId,
            brandId: result.brandId,
            code: result.modelCode,
            name: result.modelName,
            isActive: true,
          }])
      setModelId(result.modelId)
      setModelSearch(result.modelCode)
      setFiles({})
      setNotice(result.wasCreated
        ? `Detected and added model ${result.modelName} (${result.modelCode}).`
        : `Detected model ${result.modelName} (${result.modelCode}).`)
      setDetectFile(null)
      if (detectInput.current) detectInput.current.value = ''
    } catch (requestError) {
      setError(getApiErrorMessage(requestError))
    } finally {
      setDetecting(false)
    }
  }

  async function saveMasterLabels() {
    const coFile = files.CO
    const configFile = files.CONFIG
    if (!brandId || !modelId || !coFile || !configFile) {
      setError('Select a brand and model, then choose both master label images.')
      return
    }
    setSavingType('PAIR')
    setError('')
    setNotice('')
    try {
      const saved = await saveMasterLabelPair(brandId, modelId, coFile, configFile)
      const savedLabels = [saved.co, saved.config]
      setLabels((current) => [
        ...current.filter((label) =>
          !savedLabels.some(savedLabel => savedLabel.labelType === label.labelType)),
        ...savedLabels,
      ])
      setFiles({})
      setFileInputVersion((version) => version + 1)
      setPreviews((current) => {
        const next = { ...current }
        for (const [labelType, file] of [['CO', coFile], ['CONFIG', configFile]]) {
          if (next[labelType]) {
            URL.revokeObjectURL(next[labelType])
            previewUrls.current.delete(next[labelType])
          }
          next[labelType] = URL.createObjectURL(file)
          previewUrls.current.add(next[labelType])
        }
        return next
      })
      setNotice(`Both master labels match and were saved for ${selectedModel?.name}.`)
    } catch (requestError) {
      setError(getApiErrorMessage(requestError))
    } finally {
      setSavingType('')
    }
  }

  return <section className="master-label-page">
    <header className="master-label-heading">
      <Link aria-label="Back to dashboard" className="new-inspection-back" to="/dashboard"><ArrowLeft size={17} /></Link>
      <div><h1>Master Labels</h1><p>Save the reference COO and Config labels once for each brand and model.</p></div>
    </header>
    {error && <div className="configuration-alert" role="alert">{error}</div>}
    {notice && <div className="inspection-notice" role="status">{notice}</div>}

    <section className="panel master-label-selection">
      <div>
        <h2>Brand and model</h2>
        <p>Choose an existing model or upload a label image to detect one with OCR. Default fields apply automatically: COO uses Model No., QR Code, and P/N; Config uses Model No. and Queue No.</p>
      </div>
      <div className="master-label-selectors">
        <label className="form-field"><span>Brand</span>
          <select disabled={loading || savingType !== '' || detecting} onChange={(event) => {
            setBrandId(event.target.value)
            setModelId('')
            setModelSearch('')
            setFiles({})
            setDetectFile(null)
            setError('')
            setNotice('')
          }} value={brandId}>
            <option value="">Select a brand</option>
            {brands.map((brand) => <option key={brand.id} value={brand.id}>{brand.name}</option>)}
          </select>
        </label>
        <label className="form-field"><span>Model</span>
          <input
            disabled={!brandId || savingType !== '' || detecting}
            list="master-label-model-options"
            onChange={(event) => {
              const value = event.target.value
              setModelSearch(value)
              const match = brandModels.find((model) =>
                model.code.toLowerCase() === value.trim().toLowerCase()
                || model.name.toLowerCase() === value.trim().toLowerCase())
              setModelId(match?.id ?? '')
              setFiles({})
              setError('')
              setNotice('')
            }}
            placeholder="Type or select an existing model"
            value={modelSearch}
          />
          <datalist id="master-label-model-options">
            {brandModels.map((model) => <option key={model.id} value={model.code} label={model.name} />)}
          </datalist>
        </label>
      </div>
      <div className="master-label-detect">
        <input
          key={brandId}
          accept="image/jpeg,image/png,.jpg,.jpeg,.png"
          aria-label="Choose label image for model detection"
          onChange={(event) => setDetectFile(event.target.files?.[0] ?? null)}
          ref={detectInput}
          type="file"
        />
        <button className="button button-secondary" disabled={!brandId || !detectFile || detecting || savingType !== ''} onClick={() => void detectModel()} type="button">
          {detecting ? <LoaderCircle className="spin" size={15} /> : <ScanLine size={15} />}
          {detecting ? 'Detecting model…' : 'Detect or add model with OCR'}
        </button>
      </div>
    </section>

    {masterLoading && <div className="inspection-notice" role="status">Loading this model&apos;s saved master labels…</div>}
    {modelId && !masterLoading && <>
      <section aria-label="Model master labels" className="master-label-grid">
      {labelTypes.map(({ type, title, description }) => {
        const master = labelsByType[type]
        return <article className="panel master-label-card" key={type}>
          <header>
            <div><h2>{title}</h2><p>{description}</p></div>
            {master && <span className="master-label-saved"><Check size={14} /> Saved</span>}
          </header>
          {currentPreviews[type]
            ? <img alt={`${title} saved label`} className="master-label-preview" src={currentPreviews[type]} />
            : <div className="master-label-placeholder"><FileImage size={26} /><span>{master ? 'Image preview unavailable' : 'No master image saved'}</span></div>}
          {master && <div className="master-label-meta">
            <strong>{master.originalFileName}</strong>
            <span>Profile v{master.labelProfileVersion} · OCR {master.ocrStatus}</span>
            <span>{master.fields.length} extracted fields</span>
          </div>}
          <label className="form-field">
            <span>{master ? 'Replace master image' : 'Upload master image'}</span>
            <input
              key={`${type}-${fileInputVersion}`}
              accept="image/jpeg,image/png,.jpg,.jpeg,.png"
              onChange={(event) => setFiles((current) => ({
                ...current,
                [type]: event.target.files?.[0] ?? null,
              }))}
              type="file"
            />
          </label>
          {files[type] && <small className="master-label-file-name">{files[type].name}</small>}
          {master && master.fields.length > 0 && <details className="master-label-fields">
            <summary>View extracted fields</summary>
            <dl>{master.fields.map((field) => <div key={field.fieldKey}>
              <dt>{field.displayName}</dt>
              <dd>{field.extractedValue || 'Not detected'}</dd>
            </div>)}</dl>
          </details>}
        </article>
      })}
      </section>
      <div className="master-label-pair-actions">
        <p>Both labels are processed together. COO Model No. must match Config Model No., and COO P/N must match Config Queue No. If either check fails, neither label is saved.</p>
        <button
          className="button button-primary"
          disabled={!files.CO || !files.CONFIG || !selectedModel || masterLoading || savingType !== '' || detecting}
          onClick={() => void saveMasterLabels()}
          type="button"
        >
          {savingType === 'PAIR' ? <LoaderCircle className="spin" size={15} /> : <Upload size={15} />}
          {savingType === 'PAIR' ? 'Extracting, validating and saving…' : 'Validate and save both master labels'}
        </button>
      </div>
    </>}
    {!loading && !modelId && <p className="master-label-empty">Select a brand and model to configure its master labels.</p>}
  </section>
}

export default MasterLabelManagement
