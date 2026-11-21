import { useCallback, useEffect, useMemo, useState } from 'react'
import { FileStack, Pencil, Plus, SlidersHorizontal, Trash2 } from 'lucide-react'
import { Link } from 'react-router-dom'
import EmptyState from '../components/EmptyState.jsx'
import Modal from '../components/Modal.jsx'
import PageHeader from '../components/PageHeader.jsx'
import useAuth from '../hooks/useAuth.js'
import {
  deleteFieldDefinition,
  deleteLabelProfile,
  getApiErrorMessage,
  getBrands,
  getFieldDefinitions,
  getLabelProfiles,
  getModels,
  saveFieldDefinition,
  saveLabelProfile,
} from '../services/configurationApi.js'

const emptyProfile = {
  brandId: '',
  modelId: '',
  labelType: 'CO',
  code: '',
  name: '',
  version: 1,
  effectiveFromUtc: new Date().toISOString().slice(0, 10),
  isActive: true,
}

const emptyField = {
  fieldName: '',
  displayName: '',
  dataType: 'Text',
  required: true,
  critical: false,
  ocrConfidenceThreshold: 0.9,
  displayOrder: 0,
  isActive: true,
  comparisonStrategy: 'NormalizedText',
  numericAbsoluteTolerance: 0,
  numericRelativeTolerance: 0,
  aliases: '',
  aliasLanguage: 'en',
}

function LabelProfileManagement() {
  const { user } = useAuth()
  const canManageConfiguration = user?.role === 'Admin' || user?.role === 'Supervisor'
  const [brands, setBrands] = useState([])
  const [models, setModels] = useState([])
  const [profiles, setProfiles] = useState([])
  const [fields, setFields] = useState([])
  const [selectedProfile, setSelectedProfile] = useState(null)
  const canEditSelectedProfile = canManageConfiguration && Boolean(selectedProfile?.isActive)
  const [profileDialog, setProfileDialog] = useState(null)
  const [fieldDialog, setFieldDialog] = useState(null)
  const [profileForm, setProfileForm] = useState(emptyProfile)
  const [fieldForm, setFieldForm] = useState(emptyField)
  const [configTab, setConfigTab] = useState('profiles')
  const [loading, setLoading] = useState(true)
  const [fieldsLoading, setFieldsLoading] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')

  const loadConfiguration = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const [brandRows, modelRows, profileRows] = await Promise.all([
        getBrands(),
        getModels(),
        getLabelProfiles(),
      ])
      setBrands(brandRows)
      setModels(modelRows)
      setProfiles(profileRows)
      setSelectedProfile((current) => profileRows.find((profile) => profile.id === current?.id) ?? null)
    } catch (requestError) {
      setError(getApiErrorMessage(requestError))
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    let active = true
    Promise.all([getBrands(), getModels(), getLabelProfiles()])
      .then(([brandRows, modelRows, profileRows]) => {
        if (active) {
          setBrands(brandRows)
          setModels(modelRows)
          setProfiles(profileRows)
        }
      })
      .catch((requestError) => { if (active) setError(getApiErrorMessage(requestError)) })
      .finally(() => { if (active) setLoading(false) })

    return () => { active = false }
  }, [])

  const activeBrands = useMemo(() => brands.filter((brand) => brand.isActive), [brands])
  const profileModels = useMemo(
    () => models.filter((model) => model.brandId === profileForm.brandId && (model.isActive || model.id === profileForm.modelId)),
    [models, profileForm.brandId, profileForm.modelId],
  )
  const visibleFields = fields.filter((field) => field.labelProfileId === selectedProfile?.id)

  const loadFields = useCallback(async (profile) => {
    if (!profile) {
      setFields([])
      return
    }
    setFieldsLoading(true)
    setError('')
    try {
      setFields(await getFieldDefinitions(profile.id))
    } catch (requestError) {
      setFields([])
      setError(getApiErrorMessage(requestError))
    } finally {
      setFieldsLoading(false)
    }
  }, [])

  useEffect(() => {
    if (!selectedProfile) {
      return undefined
    }

    let active = true
    getFieldDefinitions(selectedProfile.id)
      .then((items) => { if (active) setFields(items) })
      .catch((requestError) => {
        if (active) {
          setFields([])
          setError(getApiErrorMessage(requestError))
        }
      })
      .finally(() => { if (active) setFieldsLoading(false) })

    return () => { active = false }
  }, [selectedProfile])

  function openProfileForm(profile = null) {
    setProfileDialog(profile ? { id: profile.id } : { id: null })
    setProfileForm(profile
      ? {
          brandId: profile.brandId,
          modelId: profile.modelId ?? '',
          labelType: profile.labelType,
          code: profile.code,
          name: profile.name,
          version: profile.version,
          effectiveFromUtc: profile.effectiveFromUtc?.slice(0, 10) ?? '',
          isActive: profile.isActive,
        }
      : { ...emptyProfile, brandId: activeBrands[0]?.id ?? '' })
    setError('')
    setNotice('')
  }

  function openFieldForm(field = null) {
    if (!selectedProfile) return
    setFieldDialog(field ? { id: field.id } : { id: null })
    setFieldForm(field
      ? {
          fieldName: field.fieldName,
          displayName: field.displayName,
          dataType: field.dataType,
          required: field.required,
          critical: field.critical,
          ocrConfidenceThreshold: field.ocrConfidenceThreshold,
          displayOrder: field.displayOrder,
          isActive: field.isActive,
          comparisonStrategy: field.comparisonStrategy,
          numericAbsoluteTolerance: field.numericAbsoluteTolerance,
          numericRelativeTolerance: field.numericRelativeTolerance,
          aliases: field.aliases.filter((alias) => alias.isActive).map((alias) => alias.value).join(', '),
          aliasLanguage: field.aliases.find((alias) => alias.isActive)?.language ?? 'en',
        }
      : emptyField)
    setError('')
    setNotice('')
  }

  async function submitProfile(event) {
    event.preventDefault()
    setBusy(true)
    setError('')
    const request = {
      ...profileForm,
      effectiveFromUtc: profileForm.effectiveFromUtc
        ? new Date(`${profileForm.effectiveFromUtc}T00:00:00Z`).toISOString()
        : null,
      version: Number(profileForm.version),
    }
    try {
      const saved = await saveLabelProfile(request, profileDialog.id)
      setProfileDialog(null)
      await loadConfiguration()
      setSelectedProfile(saved)
      setConfigTab('fields')
    } catch (requestError) {
      setError(getApiErrorMessage(requestError))
    } finally {
      setBusy(false)
    }
  }

  async function submitField(event) {
    event.preventDefault()
    setBusy(true)
    setError('')
    setNotice('')
    const request = {
      ...fieldForm,
      ocrConfidenceThreshold: Number(fieldForm.ocrConfidenceThreshold),
      displayOrder: Number(fieldForm.displayOrder),
      numericAbsoluteTolerance: Number(fieldForm.numericAbsoluteTolerance),
      numericRelativeTolerance: Number(fieldForm.numericRelativeTolerance),
      aliases: fieldForm.aliases.split(',').map((alias) => alias.trim()).filter(Boolean),
      aliasDefinitions: fieldForm.aliases.split(',').map((alias) => alias.trim()).filter(Boolean)
        .map((aliasText) => ({ aliasText, language: fieldForm.aliasLanguage || 'en', isActive: true })),
    }
    try {
      const savedField = await saveFieldDefinition(selectedProfile.id, request, fieldDialog.id)
      setFieldDialog(null)
      if (savedField.labelProfileId !== selectedProfile.id) {
        setFields([])
        setFieldsLoading(true)
        let profileRows
        try {
          profileRows = await getLabelProfiles()
        } catch (refreshError) {
          setError(`Field changes were saved, but the new profile version could not be loaded. ${getApiErrorMessage(refreshError)}`)
          return
        }
        const nextProfile = profileRows.find((profile) => profile.id === savedField.labelProfileId)
        if (!nextProfile) {
          setProfiles(profileRows)
          setError('Field changes were saved, but the new profile version was not returned. Refresh the profiles list.')
          return
        }
        setProfiles(profileRows)
        setSelectedProfile(nextProfile)
        setNotice(`Saved as profile version v${nextProfile.version}. Existing inspections remain on v${selectedProfile.version}.`)
      } else {
        await loadFields(selectedProfile)
        setNotice('Field settings saved.')
      }
    } catch (requestError) {
      setError(getApiErrorMessage(requestError))
    } finally {
      setBusy(false)
    }
  }

  async function removeProfile(profile) {
    if (!window.confirm(`Delete ${profile.labelType} profile "${profile.name}"?`)) return
    setBusy(true)
    setError('')
    try {
      await deleteLabelProfile(profile.id)
      if (selectedProfile?.id === profile.id) setSelectedProfile(null)
      await loadConfiguration()
    } catch (requestError) {
      setError(getApiErrorMessage(requestError))
    } finally {
      setBusy(false)
    }
  }

  async function removeField(field) {
    if (!window.confirm(`Delete field "${field.displayName}"?`)) return
    setBusy(true)
    setError('')
    setNotice('')
    try {
      await deleteFieldDefinition(field.id)
      const profileRows = await getLabelProfiles()
      const activeProfile = profileRows.find((profile) =>
        profile.isActive
        && profile.brandId === selectedProfile.brandId
        && (profile.modelId ?? null) === (selectedProfile.modelId ?? null)
        && profile.labelType === selectedProfile.labelType
        && profile.code === selectedProfile.code)
      if (activeProfile && activeProfile.id !== selectedProfile.id) {
        setProfiles(profileRows)
        setFields([])
        setFieldsLoading(true)
        setSelectedProfile(activeProfile)
        setNotice(`Saved as profile version v${activeProfile.version}. Existing inspections remain on v${selectedProfile.version}.`)
      } else {
        setProfiles(profileRows)
        await loadFields(selectedProfile)
      }
    } catch (requestError) {
      setError(getApiErrorMessage(requestError))
    } finally {
      setBusy(false)
    }
  }

  return (
    <>
      <PageHeader
        description="Manage the label profiles and field rules used by inspections."
        eyebrow="Configuration"
        title="Configuration"
        actions={
          canManageConfiguration && <button className="button button-primary" disabled={activeBrands.length === 0} onClick={() => openProfileForm()} type="button">
            <Plus size={15} /> Add profile
          </button>
        }
      />
      <nav aria-label="Configuration sections" className="configuration-tabs">
        <button aria-pressed={configTab === 'profiles'} className={configTab === 'profiles' ? 'active' : ''} onClick={() => setConfigTab('profiles')} type="button">Profiles</button>
        <button aria-pressed={configTab === 'fields'} className={configTab === 'fields' ? 'active' : ''} disabled={!selectedProfile} onClick={() => setConfigTab('fields')} type="button">Field mapping</button>
        {user?.role === 'Admin' && <Link to="/settings">OCR settings</Link>}
      </nav>
      {error && !profileDialog && !fieldDialog && <div className="configuration-alert" role="alert">{error}</div>}
      {notice && <div className="inspection-notice" role="status">{notice}</div>}
      {configTab === 'profiles' && <section className="panel">
        <header className="panel-header configuration-toolbar">
          <div>
            <h2 className="panel-title">Profiles</h2>
            <p className="panel-subtitle">A profile is scoped to a brand, optional model, label type, and version.</p>
          </div>
          <button className="button button-secondary" disabled={loading || busy} onClick={loadConfiguration} type="button">Refresh</button>
        </header>
        {loading ? (
          <div className="configuration-loading" role="status">Loading label profiles…</div>
        ) : error && profiles.length === 0 ? (
          <EmptyState description={error} icon={FileStack} title="Could not load label profiles" />
        ) : profiles.length === 0 ? (
          <EmptyState
            description={brands.length ? 'Add a CO or Config profile to define fields for a brand or specific model.' : 'Create a brand before configuring label profiles.'}
            icon={FileStack}
            title={brands.length ? 'No label profiles configured' : 'No brands available'}
          />
        ) : (
          <div className="table-wrap">
            <table className="data-table">
              <thead><tr><th>Profile</th><th>Brand / model</th><th>Label type</th><th>Version</th><th>Status</th>{canManageConfiguration && <th />}</tr></thead>
              <tbody>
                {profiles.map((profile) => (
                  <tr className={selectedProfile?.id === profile.id ? 'selected-table-row' : ''} key={profile.id}>
                    <td>
                      <button className="table-link" onClick={() => { setNotice(''); setFieldsLoading(true); setSelectedProfile(profile); setConfigTab('fields') }} type="button">{profile.name}</button>
                      <small className="table-secondary-cell">{profile.code}</small>
                    </td>
                    <td>{profile.brandName}<small className="table-secondary-cell">{profile.modelName ?? 'All models'}</small></td>
                    <td><span className="code-chip">{profile.labelType}</span></td>
                    <td>v{profile.version}</td>
                    <td><span className={`status-pill ${profile.isActive ? 'status-success' : 'status-muted'}`}>{profile.isActive ? 'Active' : 'Inactive'}</span></td>
                    {canManageConfiguration && <td className="table-actions">
                      <button aria-label={`Edit ${profile.name}`} className="icon-button" disabled={busy} onClick={() => openProfileForm(profile)} type="button"><Pencil size={14} /></button>
                      <button aria-label={`Delete ${profile.name}`} className="icon-button danger-icon" disabled={busy} onClick={() => removeProfile(profile)} type="button"><Trash2 size={14} /></button>
                    </td>}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>}

      {configTab === 'fields' && (selectedProfile ? (
        <section className="panel configuration-fields-panel">
          <header className="panel-header">
            <div>
              <h2 className="panel-title">{selectedProfile.name} fields</h2>
              <p className="panel-subtitle">{selectedProfile.labelType} · {selectedProfile.brandName}{selectedProfile.modelName ? ` / ${selectedProfile.modelName}` : ' / all models'}</p>
            </div>
            {canEditSelectedProfile && <button className="button button-primary" onClick={() => openFieldForm()} type="button"><Plus size={15} /> Add field</button>}
          </header>
          {!selectedProfile.isActive && <div className="inspection-notice" role="status">This profile version is read-only. Select the active version to edit its settings.</div>}
          {fieldsLoading ? (
            <div className="configuration-loading" role="status">Loading fields…</div>
          ) : error && visibleFields.length === 0 ? (
            <EmptyState description={error} icon={SlidersHorizontal} title="Could not load field definitions" />
          ) : visibleFields.length === 0 ? (
            <EmptyState description="Add field definitions and OCR aliases. No brand-specific field assumptions are built in." icon={SlidersHorizontal} title="No fields configured" />
          ) : (
            <div className="table-wrap">
              <table className="data-table">
                <thead><tr><th>Field</th><th>Rules</th><th>Comparison</th><th>Confidence</th><th>Aliases</th><th>Status</th>{canEditSelectedProfile && <th />}</tr></thead>
                <tbody>
                  {visibleFields.map((field) => (
                    <tr key={field.id}>
                      <td className="table-primary-cell">{field.displayName}<small className="table-secondary-cell">{field.fieldName} · order {field.displayOrder}</small></td>
                      <td><div className="rule-tags"><span className={`rule-tag ${field.required ? 'rule-tag-required' : ''}`}>{field.required ? 'Required' : 'Optional'}</span><span className={`rule-tag ${field.critical ? 'rule-tag-critical' : ''}`}>{field.critical ? 'Critical' : 'Non-critical'}</span></div></td>
                      <td>{field.comparisonStrategy === 'Numeric' ? `Numeric (±${field.numericAbsoluteTolerance}; ${field.numericRelativeTolerance * 100}% rel.)` : field.comparisonStrategy}</td>
                      <td>{Math.round(field.ocrConfidenceThreshold * 100)}%</td>
                      <td className="alias-list">{field.aliases.length ? field.aliases.map((alias) => alias.value).join(', ') : '—'}</td>
                      <td><span className={`status-pill ${field.isActive ? 'status-success' : 'status-muted'}`}>{field.isActive ? 'Active' : 'Inactive'}</span></td>
                      {canEditSelectedProfile && <td className="table-actions">
                        <button aria-label={`Edit field ${field.displayName}`} className="icon-button" disabled={busy} onClick={() => openFieldForm(field)} type="button"><Pencil size={14} /></button>
                        <button aria-label={`Delete field ${field.displayName}`} className="icon-button danger-icon" disabled={busy} onClick={() => removeField(field)} type="button"><Trash2 size={14} /></button>
                      </td>}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </section>
      ) : <EmptyState description="Select a label profile to view its configured fields and comparison rules." icon={SlidersHorizontal} title="Choose a profile" />)}

      {profileDialog && canManageConfiguration && (
        <Modal onClose={() => setProfileDialog(null)} title={profileDialog.id ? 'Edit label profile' : 'Add label profile'}>
          <form className="configuration-form" onSubmit={submitProfile}>
            <label className="form-field"><span>Brand</span>
              <select onChange={(event) => setProfileForm({ ...profileForm, brandId: event.target.value, modelId: '' })} required value={profileForm.brandId}>
                <option value="" disabled>Select brand</option>
                {brands.filter((brand) => brand.isActive || brand.id === profileForm.brandId).map((brand) => <option key={brand.id} value={brand.id}>{brand.name}{brand.isActive ? '' : ' (inactive)'}</option>)}
              </select>
            </label>
            <label className="form-field"><span>Model scope</span>
              <select onChange={(event) => setProfileForm({ ...profileForm, modelId: event.target.value })} value={profileForm.modelId}>
                <option value="">Select a model</option>
                {profileModels.map((model) => <option key={model.id} value={model.id}>{model.name}</option>)}
              </select>
            </label>
            <div className="form-grid">
              <label className="form-field"><span>Label type</span>
                <select onChange={(event) => setProfileForm({ ...profileForm, labelType: event.target.value })} value={profileForm.labelType}>
                  <option value="CO">CO</option><option value="CONFIG">Config</option>
                </select>
              </label>
              <label className="form-field"><span>Version</span>
                <input min="1" onChange={(event) => setProfileForm({ ...profileForm, version: event.target.value })} required type="number" value={profileForm.version} />
              </label>
            </div>
            <label className="form-field"><span>Effective from (UTC)</span><input onChange={(event) => setProfileForm({ ...profileForm, effectiveFromUtc: event.target.value })} required type="date" value={profileForm.effectiveFromUtc} /></label>
            <label className="form-field"><span>Profile name</span><input autoFocus maxLength="200" onChange={(event) => setProfileForm({ ...profileForm, name: event.target.value })} required value={profileForm.name} /></label>
            <label className="form-field"><span>Profile code</span><input maxLength="100" onChange={(event) => setProfileForm({ ...profileForm, code: event.target.value })} pattern="[A-Za-z0-9][A-Za-z0-9._-]*" required value={profileForm.code} /></label>
            <label className="form-checkbox"><input checked={profileForm.isActive} onChange={(event) => setProfileForm({ ...profileForm, isActive: event.target.checked })} type="checkbox" /><span>Profile is active</span></label>
            {error && <div className="configuration-alert" role="alert">{error}</div>}
            <div className="form-actions"><button className="button button-secondary" disabled={busy} onClick={() => setProfileDialog(null)} type="button">Cancel</button><button className="button button-primary" disabled={busy} type="submit">{busy ? 'Saving…' : 'Save profile'}</button></div>
          </form>
        </Modal>
      )}

      {fieldDialog && selectedProfile && canManageConfiguration && (
        <Modal onClose={() => setFieldDialog(null)} title={fieldDialog.id ? 'Edit field definition' : 'Add field definition'} wide>
          <form className="configuration-form" onSubmit={submitField}>
            <div className="form-grid">
              <label className="form-field"><span>Display name</span><input autoFocus maxLength="200" onChange={(event) => setFieldForm({ ...fieldForm, displayName: event.target.value })} required value={fieldForm.displayName} /></label>
              <label className="form-field"><span>Field name</span><input maxLength="100" onChange={(event) => setFieldForm({ ...fieldForm, fieldName: event.target.value })} pattern="[A-Za-z0-9][A-Za-z0-9._-]*" required value={fieldForm.fieldName} /></label>
            </div>
            <div className="form-grid">
              <label className="form-field"><span>Comparison strategy</span>
                <select onChange={(event) => setFieldForm({ ...fieldForm, comparisonStrategy: event.target.value, numericAbsoluteTolerance: event.target.value === 'Numeric' ? fieldForm.numericAbsoluteTolerance : 0, numericRelativeTolerance: event.target.value === 'Numeric' ? fieldForm.numericRelativeTolerance : 0 })} value={fieldForm.comparisonStrategy}>
                  <option value="Exact">Exact</option><option value="NormalizedText">Normalized text</option><option value="CaseInsensitive">Case-insensitive</option><option value="Numeric">Numeric</option><option value="Identifier">Identifier (ignore separators)</option><option value="MacAddress">MAC address</option>
                </select>
              </label>
              <label className="form-field"><span>Data type</span>
                <select onChange={(event) => setFieldForm({ ...fieldForm, dataType: event.target.value })} value={fieldForm.dataType}>
                  <option value="Text">Text</option><option value="Numeric">Numeric</option><option value="SerialNumber">Serial number</option><option value="Date">Date</option>
                </select>
              </label>
              <label className="form-field"><span>Minimum OCR confidence (%)</span><input max="100" min="0" onChange={(event) => setFieldForm({ ...fieldForm, ocrConfidenceThreshold: Number(event.target.value) / 100 })} required type="number" value={Math.round(fieldForm.ocrConfidenceThreshold * 100)} /></label>
            </div>
            {fieldForm.comparisonStrategy === 'Numeric' && (
              <div className="form-grid">
                <label className="form-field"><span>Absolute tolerance</span><input min="0" onChange={(event) => setFieldForm({ ...fieldForm, numericAbsoluteTolerance: event.target.value })} step="any" type="number" value={fieldForm.numericAbsoluteTolerance} /></label>
                <label className="form-field"><span>Relative tolerance (0–1)</span><input max="1" min="0" onChange={(event) => setFieldForm({ ...fieldForm, numericRelativeTolerance: event.target.value })} step="any" type="number" value={fieldForm.numericRelativeTolerance} /></label>
              </div>
            )}
            <div className="form-grid">
              <label className="form-field"><span>OCR aliases</span><input onChange={(event) => setFieldForm({ ...fieldForm, aliases: event.target.value })} placeholder="Serial No, S/N, Serial Number" value={fieldForm.aliases} /><small>Separate alternate printed labels with commas. Matching returns candidates without guessing ambiguous fields.</small></label>
              <label className="form-field"><span>Alias language</span><input maxLength="20" onChange={(event) => setFieldForm({ ...fieldForm, aliasLanguage: event.target.value })} placeholder="en" value={fieldForm.aliasLanguage} /></label>
            </div>
            <label className="form-field"><span>Display order</span><input min="0" onChange={(event) => setFieldForm({ ...fieldForm, displayOrder: event.target.value })} required type="number" value={fieldForm.displayOrder} /></label>
            <div className="form-grid form-check-grid">
              <label className="form-checkbox"><input checked={fieldForm.required} onChange={(event) => setFieldForm({ ...fieldForm, required: event.target.checked })} type="checkbox" /><span>Required field</span></label>
              <label className="form-checkbox"><input checked={fieldForm.critical} onChange={(event) => setFieldForm({ ...fieldForm, critical: event.target.checked })} type="checkbox" /><span>Critical field (mismatch fails the field)</span></label>
              <label className="form-checkbox"><input checked={fieldForm.isActive} onChange={(event) => setFieldForm({ ...fieldForm, isActive: event.target.checked })} type="checkbox" /><span>Field is active</span></label>
            </div>
            {error && <div className="configuration-alert" role="alert">{error}</div>}
            <div className="form-actions"><button className="button button-secondary" disabled={busy} onClick={() => setFieldDialog(null)} type="button">Cancel</button><button className="button button-primary" disabled={busy} type="submit">{busy ? 'Saving…' : 'Save field'}</button></div>
          </form>
        </Modal>
      )}
    </>
  )
}

export default LabelProfileManagement
