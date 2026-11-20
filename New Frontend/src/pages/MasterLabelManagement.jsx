import { useEffect, useMemo, useRef, useState } from 'react'
import {
  ArrowLeft,
  Check,
  ChevronRight,
  FileImage,
  LoaderCircle,
  ScanLine,
  ShieldCheck,
  Sparkles,
  Upload,
} from 'lucide-react'
import { Link } from 'react-router-dom'
import { getApiErrorMessage, getBrands, getModels } from '../services/configurationApi.js'
import {
  detectMasterLabelModel,
  getMasterLabelImage,
  getMasterLabelsForModel,
  saveMasterLabelPair,
} from '../services/masterLabelApi.js'

const labelTypes = [
  {
    type: 'CO',
    title: 'Master COO',
    kicker: 'COUNTRY OF ORIGIN',
    description: 'Trusted reference label used to validate origin and identity fields.',
    icon: ShieldCheck,
  },
  {
    type: 'CONFIG',
    title: 'Master Config',
    kicker: 'CONFIGURATION LABEL',
    description: 'Trusted reference label used to validate model, queue and configuration fields.',
    icon: FileImage,
  },
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

  function registerDetectedModel(result) {
    setModels((current) => current.some((model) => model.id === result.modelId)
      ? current
      : [...current, {
          id: result.modelId,
          brandId: result.brandId,
          code: result.modelCode,
          name: result.modelName,
          isActive: true,
        }])
  }

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
  const selectedBrand = brands.find((brand) => brand.id === brandId)
  const masterLoading = Boolean(modelId && loadedModelId !== modelId)
  const currentLabels = loadedModelId === modelId ? labels : []
  const currentPreviews = loadedModelId === modelId ? previews : {}
  const labelsByType = Object.fromEntries(currentLabels.map((label) => [label.labelType, label]))
  const savedCount = Object.keys(labelsByType).length

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
      registerDetectedModel(result)
      setModelId(result.modelId)
      setModelSearch(result.modelCode)
      setFiles({})
      setNotice(result.wasCreated
        ? `Detected and added model ${result.modelName} (${result.modelCode}) to Model Management.`
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
    if (!brandId || !coFile || !configFile) {
      setError('Select both the COO and Config images before saving the master-label pair.')
      return
    }
    setSavingType('PAIR')
    setError('')
    setNotice('')
    let autoDetectedModel = null
    try {
      let targetModelId = modelId
      let targetModel = selectedModel
      let detectedModelWasCreated = false
      if (!targetModelId) {
        setDetecting(true)
        try {
          const result = await detectMasterLabelModel(brandId, coFile)
          registerDetectedModel(result)
          targetModelId = result.modelId
          targetModel = { name: result.modelName, code: result.modelCode }
          detectedModelWasCreated = result.wasCreated
          autoDetectedModel = { id: result.modelId, code: result.modelCode }
        } finally {
          setDetecting(false)
        }
      }

      const saved = await saveMasterLabelPair(brandId, targetModelId, coFile, configFile)
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
      if (!modelId) {
        setModelId(targetModelId)
        setModelSearch(targetModel.code)
      }
      setNotice(detectedModelWasCreated
        ? `Both master labels match and were saved for ${targetModel.name}. Model ${targetModel.code} was also added to Model Management.`
        : `Both master labels match and were saved for ${targetModel?.name}.`)
    } catch (requestError) {
      if (autoDetectedModel) {
        setModelId(autoDetectedModel.id)
        setModelSearch(autoDetectedModel.code)
      }
      setError(getApiErrorMessage(requestError))
    } finally {
      setSavingType('')
    }
  }

  function resetSelection() {
    setBrandId('')
    setModelId('')
    setModelSearch('')
    setFiles({})
    setDetectFile(null)
    setError('')
    setNotice('')
  }

  return (
    <section className="master-label-page">
      <div className="master-label-hero">
        <div className="master-label-hero-copy">
          <Link aria-label="Back to dashboard" className="master-label-back" to="/dashboard">
            <ArrowLeft size={16} />
            <span>Back to dashboard</span>
          </Link>
          <div className="master-label-eyebrow"><Sparkles size={12} /> MASTER REFERENCE LIBRARY</div>
          <h1>Build the trusted<br /><span>label reference.</span></h1>
          <p>Configure COO and Config master labels once. Every future operator inspection can use the saved reference automatically.</p>
          <div className="master-label-hero-pills">
            <span><Check size={12} /> Model-linked</span>
            <span><Check size={12} /> OCR ready</span>
            <span><Check size={12} /> Reusable reference</span>
          </div>
        </div>

        <div className="master-label-hero-visual" aria-hidden="true">
          <div className="hero-orbit hero-orbit-one" />
          <div className="hero-orbit hero-orbit-two" />
          <div className="hero-label-card hero-label-config">
            <div className="hero-label-top"><span>CONFIG</span><span className="hero-mini-status"><Check size={9} /> MASTER</span></div>
            <div className="hero-label-title">HP Laptop 15-fd1654TU</div>
            <div className="hero-label-lines"><i /><i /><i /><i /></div>
            <div className="hero-barcode"><i /><i /><i /><i /><i /><i /><i /><i /><i /><i /></div>
            <small>QUEUE • MODEL • SERIAL</small>
          </div>
          <div className="hero-label-card hero-label-coo">
            <div className="hero-label-top"><span>COO</span><span className="hero-mini-status"><Check size={9} /> VERIFIED</span></div>
            <div className="hero-label-title">COUNTRY OF ORIGIN</div>
            <div className="hero-label-lines"><i /><i /><i /></div>
            <div className="hero-label-data"><b>MODEL</b><span>15-fd1654TU</span></div>
            <div className="hero-label-data"><b>ORIGIN</b><span>MADE IN ...</span></div>
          </div>
          <div className="hero-scan-chip"><ScanLine size={14} /><span>OCR reference</span><strong>READY</strong></div>
        </div>
      </div>

      {error && <div className="configuration-alert" role="alert">{error}</div>}
      {notice && <div className="inspection-notice" role="status">{notice}</div>}

      <section className="master-label-workspace">
        <div className="master-label-stepper">
          <div className="master-step active"><span>01</span><div><strong>Identify model</strong><small>Select or detect the model</small></div></div>
          <ChevronRight className="master-step-arrow" size={17} />
          <div className={`master-step ${modelId ? 'active' : ''}`}><span>02</span><div><strong>Save references</strong><small>COO + Config master labels</small></div></div>
          <ChevronRight className="master-step-arrow" size={17} />
          <div className={`master-step ${savedCount === 2 ? 'active' : ''}`}><span>03</span><div><strong>Ready for QA</strong><small>Available to operators</small></div></div>
        </div>

        <section className="panel master-label-selection master-label-selector-card">
          <div className="master-section-heading">
            <div>
              <span className="section-number">01</span>
              <div>
                <h2>Choose the reference model</h2>
                <p>Select a model manually, or upload a COO/Config label below to detect and add the model automatically.</p>
              </div>
            </div>
            {modelId && <button className="text-button" onClick={resetSelection} type="button">Change model</button>}
          </div>

          <div className="master-label-selectors">
            <label className="form-field">
              <span>Brand</span>
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
            <label className="form-field">
              <span>Model</span>
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
                placeholder="Type or select model"
                value={modelSearch}
              />
              <datalist id="master-label-model-options">
                {brandModels.map((model) => <option key={model.id} value={model.code} label={model.name} />)}
              </datalist>
            </label>
          </div>

          <div className="master-label-detect-box">
            <div className="detect-copy">
              <div className="detect-icon"><ScanLine size={18} /></div>
              <div><strong>Or detect model from a label</strong><span>This adds a new model under the selected brand. The COO image can also detect the model when you save the pair below.</span></div>
            </div>
            <div className="detect-actions">
              <input
                key={brandId}
                accept="image/jpeg,image/png,.jpg,.jpeg,.png"
                aria-label="Choose label image for model detection"
                onChange={(event) => setDetectFile(event.target.files?.[0] ?? null)}
                ref={detectInput}
                type="file"
              />
              <button className="button button-primary" disabled={!brandId || !detectFile || detecting || savingType !== ''} onClick={() => void detectModel()} type="button">
                {detecting ? <LoaderCircle className="spin" size={15} /> : <ScanLine size={15} />}
                {detecting ? 'Detecting model…' : 'Detect or add model with OCR'}
              </button>
            </div>
          </div>
        </section>

        {masterLoading && <div className="inspection-notice" role="status">Loading this model&apos;s saved master labels…</div>}

        {brandId && !masterLoading && (
          <section aria-label="Model master labels" className="master-label-reference-area">
            <div className="master-reference-heading">
              <div>
                <span className="section-number">02</span>
                <div><h2>Reference labels</h2><p>{selectedBrand?.name} · {selectedModel?.name || selectedModel?.code || 'Model will be detected from the COO image'}</p></div>
              </div>
              <div className="reference-completion"><span>{savedCount}/2 configured</span><div><i style={{ width: `${savedCount * 50}%` }} /></div></div>
            </div>

            <div className="master-label-grid">
              {labelTypes.map(({ type, title, kicker, description, icon: Icon }) => {
                const master = labelsByType[type]
                return (
                  <article className={`panel master-label-card ${master ? 'is-saved' : ''}`} key={type}>
                    <header className="master-card-header">
                      <div className="master-card-title">
                        <div className="master-card-icon"><Icon size={18} /></div>
                        <div><span>{kicker}</span><h2>{title}</h2></div>
                      </div>
                      {master && <span className="master-label-saved"><Check size={13} /> Saved</span>}
                    </header>

                    <p className="master-card-description">{description}</p>

                    {currentPreviews[type]
                      ? <div className="master-label-preview-frame"><img alt={`${title} saved label`} className="master-label-preview" src={currentPreviews[type]} /></div>
                      : <div className="master-label-placeholder"><div><FileImage size={24} /><span>{master ? 'Image preview unavailable' : 'No master image saved yet'}</span></div></div>}

                    {master && (
                      <div className="master-label-meta">
                        <div><strong>{master.originalFileName}</strong><span>{master.fields.length} extracted fields</span></div>
                        <span>Profile v{master.labelProfileVersion} · OCR {master.ocrStatus}</span>
                      </div>
                    )}

                    <label className="master-upload-zone">
                      <Upload size={17} />
                      <span><strong>{master ? 'Replace master label' : 'Choose master label'}</strong><small>JPG or PNG · saved only after both labels validate</small></span>
                      <input
                        key={`${type}-${fileInputVersion}`}
                        accept="image/jpeg,image/png,.jpg,.jpeg,.png"
                        onChange={(event) => setFiles((current) => ({ ...current, [type]: event.target.files?.[0] ?? null }))}
                        type="file"
                      />
                    </label>
                    {files[type] && <small className="master-label-file-name">Selected: {files[type].name}</small>}

                    {master && master.fields.length > 0 && <details className="master-label-fields">
                      <summary>View extracted fields</summary>
                      <dl>{master.fields.map((field) => <div key={field.fieldKey}><dt>{field.displayName}</dt><dd>{field.extractedValue || 'Not detected'}</dd></div>)}</dl>
                    </details>}
                  </article>
                )
              })}
            </div>
            <div className="master-label-pair-actions">
              <p>Both labels are processed together. COO Model No. must match Config Model No., and COO P/N must match Config Queue No. If either check fails, neither label is saved.</p>
              <button
                className="button button-primary master-save-button"
                disabled={!files.CO || !files.CONFIG || !brandId || masterLoading || savingType !== '' || detecting}
                onClick={() => void saveMasterLabels()}
                type="button"
              >
                {savingType === 'PAIR' ? <LoaderCircle className="spin" size={15} /> : <Upload size={15} />}
                {savingType === 'PAIR'
                  ? detecting ? 'Detecting, validating and saving…' : 'Extracting, validating and saving…'
                  : modelId ? 'Validate and save both master labels' : 'Detect model, validate and save both'}
              </button>
            </div>
          </section>
        )}

        {!loading && !brandId && (
          <div className="master-label-empty-state">
            <div className="empty-state-icon"><FileImage size={24} /></div>
            <div><strong>Select a brand to configure references</strong><p>Choose an existing model, detect one with OCR, or upload a master label to detect and add its model automatically.</p></div>
          </div>
        )}
      </section>
    </section>
  )
}

export default MasterLabelManagement
