import { useEffect, useMemo, useRef, useState } from 'react'
import { AlertCircle, ArrowLeft, Camera, Check, ChevronDown, CircleHelp, FileImage, LoaderCircle, Trash2, Upload } from 'lucide-react'
import { Link, useNavigate } from 'react-router-dom'
import {
  createInspection,
  deleteInspectionImage,
  getInspectionImages,
  getInspectionImage,
  getInspectionErrorMessage,
  processInspection,
  uploadInspectionImage,
  validateInspection,
} from '../services/inspectionApi.js'
import { getBrands, getLabelProfiles, getModels } from '../services/configurationApi.js'

const imageSlots = [
  { type: 'MASTER_CO', title: 'Master CO', description: 'Reference CO label' },
  { type: 'ACTUAL_CO', title: 'Actual CO', description: 'Unit CO label' },
  { type: 'MASTER_CONFIG', title: 'Master Config', description: 'Reference configuration label' },
  { type: 'ACTUAL_CONFIG', title: 'Actual Config', description: 'Unit configuration label' },
]
const maximumBytes = 20 * 1024 * 1024

function NewInspection() {
  const navigate = useNavigate()
  const [brands, setBrands] = useState([])
  const [models, setModels] = useState([])
  const [profiles, setProfiles] = useState([])
  const [form, setForm] = useState({ brandId: '', modelId: '' })
  const [files, setFiles] = useState({})
  const [stage, setStage] = useState(1)
  const [detailsOpen, setDetailsOpen] = useState(false)
  const [uploadedImages, setUploadedImages] = useState([])
  const [inspection, setInspection] = useState(null)
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [uploadProgress, setUploadProgress] = useState({})
  const [uploadedPreviews, setUploadedPreviews] = useState({})
  const saveInProgress = useRef(false)
  const processInProgress = useRef(false)
  const fileInputRefs = useRef({})
  const cameraInputRefs = useRef({})
  const fileUrls = useRef(new Set())
  const uploadedPreviewUrls = useRef(new Set())
  const validationRequest = useRef(null)

  function validateInspectionOnce(inspectionId) {
    if (validationRequest.current) return validationRequest.current
    const request = validateInspection(inspectionId).finally(() => {
      if (validationRequest.current === request) validationRequest.current = null
    })
    validationRequest.current = request
    return request
  }

  useEffect(() => {
    let active = true
    Promise.all([getBrands(), getModels(), getLabelProfiles()])
      .then(([brandRows, modelRows, profileRows]) => {
        if (!active) return
        setBrands(brandRows)
        setModels(modelRows)
        setProfiles(profileRows)
      })
      .catch((requestError) => { if (active) setError(getInspectionErrorMessage(requestError)) })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [])

  useEffect(() => () => {
    fileUrls.current.forEach((url) => URL.revokeObjectURL(url))
    fileUrls.current.clear()
    uploadedPreviewUrls.current.forEach((url) => URL.revokeObjectURL(url))
    uploadedPreviewUrls.current.clear()
  }, [])

  const filteredModels = useMemo(
    () => models.filter((model) => model.brandId === form.brandId && model.isActive),
    [models, form.brandId],
  )
  const activeProfiles = useMemo(
    () => profiles.filter((profile) => profile.modelId === form.modelId && profile.isActive),
    [profiles, form.modelId],
  )
  const hasCoProfile = activeProfiles.some((profile) => profile.labelType === 'CO')
  const hasConfigProfile = activeProfiles.some((profile) => profile.labelType === 'CONFIG')
  const missingImageTypes = imageSlots.filter(({ type }) =>
    !files[type] && !uploadedImages.some((image) => image.imageType === type))
  const readyImageCount = imageSlots.length - missingImageTypes.length
  const allImagesReady = readyImageCount === imageSlots.length
  const canStart = Boolean(inspection)
    && ['READY', 'ERROR'].includes(inspection.inspection.status)
    && imageSlots.every(({ type }) => uploadedImages.some((image) => image.imageType === type))
    && Object.keys(files).length === 0
    && !busy
  const allImagesUploaded = imageSlots.every(({ type }) => uploadedImages.some((image) => image.imageType === type))

  async function chooseFile(type, file) {
    setError('')
    setNotice('')
    if (!file) return
    if (!['image/jpeg', 'image/png'].includes(file.type) || file.size > maximumBytes || file.size === 0) {
      setError('Choose a non-empty JPEG or PNG image no larger than 20 MB.')
      return
    }
    try {
      const bitmap = await createImageBitmap(file)
      const validSize = bitmap.width >= 320 && bitmap.height >= 240
        && bitmap.width <= 20000 && bitmap.height <= 20000
        && bitmap.width * bitmap.height <= 100_000_000
      bitmap.close()
      if (!validSize) {
        setError('Images must be at least 320 × 240 pixels and within supported limits.')
        return
      }
    } catch {
      setError('The selected file could not be decoded as an image.')
      return
    }

    const url = URL.createObjectURL(file)
    fileUrls.current.add(url)
    setStage(1)
    setFiles((current) => {
      const previous = current[type]
      if (previous?.url) {
        URL.revokeObjectURL(previous.url)
        fileUrls.current.delete(previous.url)
      }
      return { ...current, [type]: { file, url } }
    })
  }

  async function removeImage(type) {
    setError('')
    setStage(1)
    const existing = uploadedImages.find((image) => image.imageType === type)
    if (existing && inspection) {
      setBusy(true)
      try {
        await deleteInspectionImage(inspection.inspection.id, existing.id)
        setUploadedImages((images) => images.filter((image) => image.id !== existing.id))
        setUploadedPreviews((current) => {
          if (current[existing.id]) {
            URL.revokeObjectURL(current[existing.id])
            uploadedPreviewUrls.current.delete(current[existing.id])
          }
          const next = { ...current }
          delete next[existing.id]
          return next
        })
        setInspection((current) => current
          ? { ...current, inspection: { ...current.inspection, status: 'READY' } }
          : current)
        setNotice('Uploaded image removed.')
      } catch (requestError) {
        setError(getInspectionErrorMessage(requestError))
      } finally {
        setBusy(false)
      }
    }
    setFiles((current) => {
      const next = { ...current }
      if (next[type]?.url) {
        URL.revokeObjectURL(next[type].url)
        fileUrls.current.delete(next[type].url)
      }
      delete next[type]
      return next
    })
  }

  async function saveInspection() {
    if (saveInProgress.current) return
    if (!form.brandId || !form.modelId) {
      setDetailsOpen(true)
      setError('Choose a brand and laptop model in Inspection details before continuing.')
      return
    }
    if (!hasCoProfile || !hasConfigProfile) {
      setDetailsOpen(true)
      setError('The selected model needs active CO and CONFIG profiles before an inspection can be created.')
      return
    }
    if (!allImagesReady) {
      setError('Upload or capture all four required images before continuing.')
      return
    }
    saveInProgress.current = true
    setBusy(true)
    setError('')
    setNotice('')
    try {
      let saved = inspection
      if (!saved) {
        saved = await createInspection({
          brandId: form.brandId,
          modelId: form.modelId,
        })
        setInspection(saved)
      }
      let images = await getInspectionImages(saved.inspection.id)
      setUploadedImages(images)
      for (const { type } of imageSlots) {
        if (files[type]) {
          const replaced = images.find((image) => image.imageType === type)
          if (replaced) {
            await deleteInspectionImage(saved.inspection.id, replaced.id)
            setUploadedPreviews((current) => {
              if (current[replaced.id]) {
                URL.revokeObjectURL(current[replaced.id])
                uploadedPreviewUrls.current.delete(current[replaced.id])
              }
              const next = { ...current }
              delete next[replaced.id]
              return next
            })
            images = images.filter((image) => image.id !== replaced.id)
            setUploadedImages(images)
          }
          const uploaded = await uploadInspectionImage(
            saved.inspection.id, type, files[type].file,
            (percent) => setUploadProgress((current) => ({ ...current, [type]: percent })))
          images = [...images.filter((image) => image.imageType !== type), uploaded]
          setUploadedImages(images)
          const previewBlob = await getInspectionImage(saved.inspection.id, uploaded.id)
          const previewUrl = URL.createObjectURL(previewBlob)
          uploadedPreviewUrls.current.add(previewUrl)
          setUploadedPreviews((current) => ({
            ...current,
            [uploaded.id]: previewUrl,
          }))
          const selected = files[type]
          if (selected?.url) {
            URL.revokeObjectURL(selected.url)
            fileUrls.current.delete(selected.url)
          }
          setFiles((current) => {
            const next = { ...current }
            delete next[type]
            return next
          })
          setUploadProgress((current) => ({ ...current, [type]: 100 }))
        }
      }
      if (imageSlots.every(({ type }) => images.some((image) => image.imageType === type))) {
        setInspection((current) => current
          ? { ...current, inspection: { ...current.inspection, status: 'READY' } }
          : current)
      }
      setNotice(`Inspection ${saved.inspection.inspectionNumber} saved with ${images.length} of 4 images.`)
      if (images.length === imageSlots.length) setStage(2)
    } catch (requestError) {
      setError(getInspectionErrorMessage(requestError))
    } finally {
      saveInProgress.current = false
      setBusy(false)
    }
  }

  async function startVerification() {
    if (processInProgress.current || !canStart) return
    processInProgress.current = true
    setBusy(true)
    setStage(3)
    setError('')
    setNotice('Checking the four uploaded images, then running OpenCV, Tesseract, and field comparison…')
    try {
      const inspectionId = inspection.inspection.id
      const serverImages = await getInspectionImages(inspectionId)
      setUploadedImages(serverImages)
      const missing = imageSlots
        .filter(({ type }) => !serverImages.some((image) => image.imageType === type))
        .map(({ title }) => title)
      if (missing.length > 0) {
        setStage(2)
        setError(`Upload all four images before processing. Missing: ${missing.join(', ')}.`)
        setNotice('')
        return
      }
      await validateInspectionOnce(inspectionId)
      const result = await processInspection(inspectionId)
      setInspection((current) => current
        ? { ...current, inspection: { ...current.inspection, status: result.status } }
        : current)
      navigate(`/inspections/${inspectionId}`)
    } catch (requestError) {
      setStage(2)
      if (requestError.response?.status === 502) {
        setInspection((current) => current
          ? { ...current, inspection: { ...current.inspection, status: 'ERROR' } }
          : current)
      }
      setError(getInspectionErrorMessage(requestError))
      setNotice('')
    } finally {
      processInProgress.current = false
      setBusy(false)
    }
  }

  return (
    <section className="new-inspection-page">
      <header className="new-inspection-heading">
        <Link aria-label="Back to dashboard" className="new-inspection-back" to="/dashboard"><ArrowLeft size={17} /></Link>
        <div><h1>New Inspection</h1><p>Upload or capture the four required label images</p></div>
        <button aria-label="Inspection help" className="icon-button" title="Upload all four images: CO and configuration labels for both Master and Actual." type="button"><CircleHelp size={17} /></button>
      </header>
      {error && <div className="configuration-alert" role="alert">{error}</div>}
      {notice && <div className={`inspection-notice${busy ? ' is-working' : ''}`} role="status">{notice}{inspection && <> · <Link to={`/inspections/${inspection.inspection.id}`}>Open details</Link></>}</div>}

      <ol aria-label="Inspection workflow steps" className="inspection-mobile-steps">
        <li className={stage === 1 ? 'is-current' : 'is-complete'}><span>1</span><strong>Upload Images</strong></li>
        <li className={stage === 2 ? 'is-current' : ''}><span>2</span><strong>Review</strong></li>
        <li className={stage === 3 ? 'is-current' : ''}><span>3</span><strong>Result</strong></li>
      </ol>

      {stage === 1 ? <form className="inspection-upload-flow" onSubmit={(event) => { event.preventDefault(); void saveInspection() }}>
        <div className="inspection-upload-prompt"><AlertCircle size={16} /><span>Upload or capture all 4 images <small>(CO &amp; Config)</small></span><strong>{readyImageCount}/4</strong></div>
        <section className="inspection-details-accordion">
          <button aria-expanded={detailsOpen} className="inspection-details-toggle" onClick={() => setDetailsOpen((open) => !open)} type="button">
            <span><strong>Inspection details</strong><small>{form.modelId ? filteredModels.find((model) => model.id === form.modelId)?.name : 'Select brand and model to continue'}</small></span>
            <ChevronDown className={detailsOpen ? 'is-open' : ''} size={17} />
          </button>
          {detailsOpen && <div className="inspection-details-fields">
            <label className="form-field"><span>Brand</span>
              <select disabled={loading || Boolean(inspection)} onChange={(event) => setForm({ ...form, brandId: event.target.value, modelId: '' })} value={form.brandId}>
                <option value="">Select a brand</option>
                {brands.filter((brand) => brand.isActive).map((brand) => <option key={brand.id} value={brand.id}>{brand.name}</option>)}
              </select>
            </label>
            <label className="form-field"><span>Laptop model</span>
              <select disabled={!form.brandId || Boolean(inspection)} onChange={(event) => setForm({ ...form, modelId: event.target.value })} value={form.modelId}>
                <option value="">Select a model</option>
                {filteredModels.map((model) => <option key={model.id} value={model.id}>{model.name} ({model.code})</option>)}
              </select>
            </label>
          </div>}
        </section>
        {form.modelId && (!hasCoProfile || !hasConfigProfile) && <div className="configuration-alert" role="alert">This model needs one active CO and one active CONFIG profile before an inspection can be created.</div>}
        <div className="inspection-upload-grid">
          {imageSlots.map(({ type, title, description }) => {
            const selected = files[type]
            const uploaded = uploadedImages.find((image) => image.imageType === type)
            const previewUrl = selected?.url ?? uploadedPreviews[uploaded?.id]
            return (
              <article className="inspection-upload-card" key={type}>
                <input
                  accept="image/jpeg,image/png,.jpg,.jpeg,.png"
                  aria-label={`Select ${title} image`}
                  className="visually-hidden"
                  onChange={(event) => { void chooseFile(type, event.target.files?.[0]); event.target.value = '' }}
                  ref={(element) => { fileInputRefs.current[type] = element }}
                  type="file"
                />
                <input
                  accept="image/jpeg,image/png,.jpg,.jpeg,.png"
                  aria-label={`Capture ${title} image`}
                  capture="environment"
                  className="visually-hidden"
                  onChange={(event) => { void chooseFile(type, event.target.files?.[0]); event.target.value = '' }}
                  ref={(element) => { cameraInputRefs.current[type] = element }}
                  type="file"
                />
                <div className="inspection-upload-heading"><strong>{title}</strong><small>{description}</small></div>
                {previewUrl ? <img alt={`${title} preview`} className="inspection-image-preview" src={previewUrl} /> : <div className="inspection-image-placeholder"><FileImage size={26} /><span>Image required</span></div>}
                <div className="inspection-file-meta">
                  {selected ? <span>{selected.file.name} · {(selected.file.size / 1024 / 1024).toFixed(2)} MB</span>
                    : uploaded ? <span><Check size={14} /> {uploaded.originalFileName} · {(uploaded.fileSizeBytes / 1024 / 1024).toFixed(2)} MB</span>
                      : <span>Required</span>}
                </div>
                {uploadProgress[type] > 0 && uploadProgress[type] < 100 && <div className="inspection-upload-progress" role="status"><progress max="100" value={uploadProgress[type]} /><span>{uploadProgress[type]}%</span></div>}
                <div className="inspection-card-actions">
                  <button className="button button-secondary" disabled={busy || Boolean(inspection && inspection.inspection.status === 'PROCESSING')} onClick={() => fileInputRefs.current[type]?.click()} type="button"><Upload size={13} /> Upload</button>
                  <button className="button button-secondary" disabled={busy || Boolean(inspection && inspection.inspection.status === 'PROCESSING')} onClick={() => cameraInputRefs.current[type]?.click()} type="button"><Camera size={13} /> Capture</button>
                  {(selected || uploaded) && <button aria-label={`Remove ${title} image`} className="icon-button danger-icon" disabled={busy} onClick={() => void removeImage(type)} type="button"><Trash2 size={15} /></button>}
                </div>
              </article>
            )
          })}
        </div>
        {!loading && brands.length === 0 && <div className="configuration-alert" role="alert">No brands are configured. Add a brand, model, and active CO/CONFIG profiles first.</div>}
        <footer className="inspection-continue-footer">
          {inspection && <span>Inspection <strong>{inspection.inspection.inspectionNumber}</strong></span>}
          <button className="button button-primary" disabled={busy || loading || !allImagesReady} type="submit">
            {busy ? <LoaderCircle className="spin" size={15} /> : null} {busy ? 'Saving images…' : 'Continue'}
          </button>
        </footer>
      </form> : stage === 3 ? <section aria-live="polite" className="inspection-processing panel">
        <div className="inspection-processing-ring"><FileImage size={34} /></div>
        <h2>Processing images…</h2>
        <p>Running OCR and comparing the configured label fields. This may take a few seconds.</p>
        <ol>
          <li><span />Extracting text and barcodes</li>
          <li><span />Comparing configured fields</li>
          <li><span />Preparing inspection result</li>
        </ol>
      </section> : <section className="inspection-review panel">
        <header><div><h2>Review images</h2><p>Make sure the correct label is shown in each image before processing.</p></div><span className="inspection-review-ready"><Check size={14} /> 4 images ready</span></header>
        <div className="inspection-review-grid">
          {imageSlots.map(({ type, title }) => {
            const uploaded = uploadedImages.find((image) => image.imageType === type)
            return <article key={type}>
              <strong>{title}</strong>
              {uploadedPreviews[uploaded?.id]
                ? <img alt={`${title} preview`} src={uploadedPreviews[uploaded.id]} />
                : <div className="inspection-review-placeholder"><FileImage size={24} /></div>}
            </article>
          })}
        </div>
        <footer>
          <button className="button button-secondary" disabled={busy} onClick={() => setStage(1)} type="button"><ArrowLeft size={14} /> Back to images</button>
          <button className="button button-primary" disabled={!canStart || busy || !allImagesUploaded} onClick={() => void startVerification()} type="button">
            {busy ? <LoaderCircle className="spin" size={15} /> : null} {busy ? 'Processing images…' : 'Start verification'}
          </button>
        </footer>
      </section>}
    </section>
  )
}

export default NewInspection
