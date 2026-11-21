import { useEffect, useMemo, useRef, useState } from 'react'
import {
  Check,
  FileImage,
  LoaderCircle,
  ScanLine,
  ShieldCheck,
  Upload,
} from 'lucide-react'
import PageHeader from '../components/PageHeader.jsx'
import useAuth from '../hooks/useAuth.js'
import { getApiErrorMessage, getBrands, getModels } from '../services/configurationApi.js'
import {
  getMasterLabelImage,
  getMasterLabelsForModel,
  saveMasterLabelPair,
  detectMasterLabelModel,
} from '../services/masterLabelApi.js'

const labelTypes = [
  {
    type: 'CO',
    title: 'Master COO',
    icon: ShieldCheck,
  },
  {
    type: 'CONFIG',
    title: 'Master Config',
    icon: FileImage,
  },
]

function MasterLabelManagement() {
  const { user } = useAuth()
  const isAdmin = user?.role === 'Admin'
  const [brands, setBrands] = useState([])
  const [models, setModels] = useState([])
  const [brandId, setBrandId] = useState('')
  const [modelId, setModelId] = useState('')
  const [labels, setLabels] = useState([])
  const [loadedModelId, setLoadedModelId] = useState('')
  const [files, setFiles] = useState({})
  const [fileInputVersion, setFileInputVersion] = useState(0)
  const [previews, setPreviews] = useState({})
  const [loading, setLoading] = useState(true)
  const [savingType, setSavingType] = useState('')
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const previewUrls = useRef(new Set())
  const detectModelFileRef = useRef(null)

  useEffect(() => {
    let active = true
    Promise.all([getBrands(), getModels()])
      .then(([brandRows, modelRows]) => {
        if (!active) return
        const activeBrands = brandRows.filter((brand) => brand.isActive)
        setBrands(activeBrands)
        setModels(modelRows.filter((model) => model.isActive))
        if (!isAdmin) setBrandId(user?.brandId ?? '')
      })
      .catch((requestError) => { if (active) setError(getApiErrorMessage(requestError)) })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [isAdmin, user?.brandId])

  const brandModels = useMemo(
    () => models.filter((model) => model.brandId === brandId),
    [models, brandId],
  )
  const selectedModel = models.find((model) => model.id === modelId)
  const assignedBrandName = brands.find((brand) => brand.id === user?.brandId)?.name
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

  async function saveMasterLabels() {
    const coFile = files.CO
    const configFile = files.CONFIG
    if (!brandId || !modelId || !coFile || !configFile) {
      setError('Select a brand and model, then choose both master-label images.')
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
      setNotice(`Both master labels were saved for ${selectedModel?.name || 'the selected model'}.`)
    } catch (requestError) {
      setError(getApiErrorMessage(requestError))
    } finally {
      setSavingType('')
    }
  }

  async function detectModelFromLabel(file) {
    if (!brandId || !file) {
      setError('Select a Brand and capture or upload a label image to detect the model.')
      return
    }

    setSavingType('DETECT')
    setError('')
    setNotice('')
    try {
      const detected = await detectMasterLabelModel(brandId, file)
      const detectedModel = {
        id: detected.modelId,
        brandId: detected.brandId,
        code: detected.modelCode,
        name: detected.modelName,
        isActive: true,
      }
      setModels((current) => current.some((model) => model.id === detectedModel.id)
        ? current
        : [...current, detectedModel])
      setModelId(detected.modelId)
      setNotice(detected.wasCreated
        ? `New model ${detected.modelName} was detected and created. Upload both labels to assign them to this model.`
        : `Model ${detected.modelName} was detected. Upload both labels to assign them to this model.`)
    } catch (requestError) {
      setError(getApiErrorMessage(requestError))
    } finally {
      setSavingType('')
    }
  }

  return (
    <section className="master-label-page">
      <PageHeader title="Master Label" />

      {error && <div className="configuration-alert" role="alert">{error}</div>}
      {notice && <div className="inspection-notice" role="status">{notice}</div>}

      <div className="master-label-selectors mes-outlined-fields">
        <label className="form-field">
          <span>Brand</span>
          {isAdmin
            ? <select disabled={loading || savingType !== ''} onChange={(event) => {
              setBrandId(event.target.value)
              setModelId('')
              setFiles({})
              setError('')
              setNotice('')
            }} value={brandId}>
              <option value="">Select a brand</option>
              {brands.map((brand) => <option key={brand.id} value={brand.id}>{brand.name}</option>)}
            </select>
            : <input readOnly value={assignedBrandName ?? (loading ? 'Loading assigned brand…' : 'Assigned brand unavailable')} />}
        </label>
        <div className="master-model-field">
          <label className="form-field" htmlFor="master-label-model">
            <span>Model</span>
            <select
              disabled={!brandId || loading || savingType !== ''}
              id="master-label-model"
              onChange={(event) => {
                setModelId(event.target.value)
                setFiles({})
                setError('')
                setNotice('')
              }}
              value={modelId}
            >
              <option value="">{brandId ? 'Select a model' : 'Select a brand first'}</option>
              {brandModels.map((model) => (
                <option key={model.id} value={model.id}>{model.name} ({model.code})</option>
              ))}
            </select>
          </label>
          <button
            className="button button-primary master-detect-model-button"
            disabled={!brandId || loading || savingType !== ''}
            onClick={() => detectModelFileRef.current?.click()}
            type="button"
          >
            {savingType === 'DETECT' ? <LoaderCircle className="spin" size={15} /> : <ScanLine size={15} />}
            {savingType === 'DETECT' ? 'Detecting model…' : 'Detect Model'}
          </button>
          <input
            accept="image/jpeg,image/png,.jpg,.jpeg,.png"
            capture="environment"
            disabled={!brandId || loading || savingType !== ''}
            hidden
            onChange={(event) => {
              const file = event.target.files?.[0]
              event.target.value = ''
              if (file) void detectModelFromLabel(file)
            }}
            ref={detectModelFileRef}
            type="file"
          />
        </div>
      </div>

      {masterLoading && <div className="inspection-notice" role="status">Loading saved master labels…</div>}

      {brandId && !masterLoading && (
        <section aria-label="Model master labels" className="master-label-reference-area">
          <div className="master-reference-heading">
            <div>
              <h2>{modelId ? 'Master Labels' : 'Upload labels to identify a model'}</h2>
              <p>{selectedModel
                ? `${selectedModel.name} (${selectedModel.code})`
                : 'Upload a COO or Config image, then detect the model before saving both labels.'}</p>
            </div>
            {modelId && <div className="reference-completion"><span>{savedCount}/2 configured</span><div><i style={{ width: `${savedCount * 50}%` }} /></div></div>}
          </div>

          <div className="master-label-grid">
            {labelTypes.map(({ type, title, icon: Icon }) => {
              const master = labelsByType[type]
              return (
                <article className={`panel master-label-card ${master ? 'is-saved' : ''}`} key={type}>
                  <header className="master-card-header">
                    <div className="master-card-title">
                      <div className="master-card-icon"><Icon size={18} /></div>
                      <div><h2>{title}</h2></div>
                    </div>
                    {master && <span className="master-label-saved"><Check size={13} /> Saved</span>}
                  </header>

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
                    <span><strong>{master ? 'Replace master label' : 'Upload master label'}</strong><small>JPG or PNG</small></span>
                    <input
                      key={`${type}-${fileInputVersion}`}
                      accept="image/jpeg,image/png,.jpg,.jpeg,.png"
                      disabled={savingType !== ''}
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
            <button
              className="button button-primary master-save-button"
              disabled={!files.CO || !files.CONFIG || !modelId || masterLoading || savingType !== ''}
              onClick={() => void saveMasterLabels()}
              type="button"
            >
              {savingType === 'PAIR' ? <LoaderCircle className="spin" size={15} /> : <Check size={15} />}
              {savingType === 'PAIR' ? 'Validating and saving…' : 'Validate and Save'}
            </button>
          </div>
        </section>
      )}
    </section>
  )
}

export default MasterLabelManagement
