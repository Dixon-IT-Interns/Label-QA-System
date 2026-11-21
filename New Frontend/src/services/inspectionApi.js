import apiClient from './apiClient.js'

export async function createInspection(request) {
  return (await apiClient.post('/api/inspections', request)).data
}

export async function getInspections(params) {
  return (await apiClient.get('/api/inspections', { params })).data
}

export async function getInspection(id) {
  return (await apiClient.get(`/api/inspections/${id}`)).data
}

export async function uploadInspectionImage(id, imageType, file, onProgress) {
  const form = new FormData()
  form.append('imageType', imageType)
  form.append('file', file)
  return (await apiClient.post(`/api/inspections/${id}/images`, form, {
    onUploadProgress: (event) => {
      if (event.total) onProgress?.(Math.round((event.loaded / event.total) * 100))
    },
  })).data
}

export async function getInspectionImages(id) {
  return (await apiClient.get(`/api/inspections/${id}/images`)).data
}

export async function getInspectionImage(id, imageId) {
  return (await apiClient.get(`/api/inspections/${id}/images/${imageId}`, { responseType: 'blob' })).data
}

export async function correctInspectionField(inspectionId, imageId, fieldDefinitionId, correction) {
  return (await apiClient.post(
    `/api/inspections/${inspectionId}/images/${imageId}/fields/${fieldDefinitionId}/correct`,
    correction,
  )).data
}

export async function deleteInspectionImage(id, imageId) {
  await apiClient.delete(`/api/inspections/${id}/images/${imageId}`)
}

export async function validateInspection(id) {
  return (await apiClient.post(`/api/inspections/${id}/validate`)).data
}

export async function processInspection(id) {
  return (await apiClient.post(`/api/inspections/${id}/process`, null, { timeout: 180000 })).data
}

export async function cancelInspection(id) {
  await apiClient.post(`/api/inspections/${id}/cancel`)
}

export async function getInspectionLogs(id) {
  return (await apiClient.get(`/api/inspections/${id}/logs`)).data
}

export async function downloadInspectionReport(id) {
  return (await apiClient.get(`/api/inspections/${id}/report`, { responseType: 'blob' })).data
}

export function getInspectionErrorMessage(error) {
  const problem = error.response?.data
  if (typeof problem?.title === 'string') return problem.title
  if (typeof problem?.detail === 'string') return problem.detail
  return 'The inspection request failed. Check the API connection and try again.'
}
