import apiClient from './apiClient.js'

const masterLabelOcrTimeoutMs = 15 * 60 * 1000

export async function getMasterLabels(filters = {}) {
  return (await apiClient.get('/api/master-labels', { params: filters })).data
}

export async function getMasterLabelsForModel(modelId, signal) {
  return (await apiClient.get(`/api/master-labels/models/${modelId}`, { signal })).data
}

export async function getMasterLabelAvailability(modelId, signal) {
  return (await apiClient.get(`/api/master-labels/availability/${modelId}`, { signal })).data
}

export async function saveMasterLabelPair(brandId, modelId, coFile, configFile) {
  const form = new FormData()
  form.append('brandId', brandId)
  form.append('coFile', coFile)
  form.append('configFile', configFile)
  return (await apiClient.put(`/api/master-labels/${modelId}/pair`, form, {
    timeout: masterLabelOcrTimeoutMs,
  })).data
}

export async function detectMasterLabelModel(brandId, file) {
  const form = new FormData()
  form.append('brandId', brandId)
  form.append('file', file)
  return (await apiClient.post('/api/master-labels/detect-model', form, {
    timeout: masterLabelOcrTimeoutMs,
  })).data
}

export async function getMasterLabelImage(modelId, labelType, signal) {
  return (await apiClient.get(
    `/api/master-labels/${modelId}/${labelType}/image`,
    { responseType: 'blob', signal },
  )).data
}
