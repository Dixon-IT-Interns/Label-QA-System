import apiClient from './apiClient.js'

export async function getBrands() {
  return (await apiClient.get('/api/brands')).data
}

export async function saveBrand(brand, id) {
  const response = id
    ? await apiClient.put(`/api/brands/${id}`, brand)
    : await apiClient.post('/api/brands', brand)
  return response.data
}

export async function deleteBrand(id) {
  await apiClient.delete(`/api/brands/${id}`)
}

export async function getModels(brandId) {
  return (await apiClient.get('/api/models', { params: brandId ? { brandId } : {} })).data
}

export async function saveModel(model, id) {
  const response = id
    ? await apiClient.put(`/api/models/${id}`, model)
    : await apiClient.post('/api/models', model)
  return response.data
}

export async function deleteModel(id) {
  await apiClient.delete(`/api/models/${id}`)
}

export async function getLabelProfiles(filters = {}) {
  return (await apiClient.get('/api/label-profiles', { params: filters })).data
}

export async function saveLabelProfile(profile, id) {
  const response = id
    ? await apiClient.put(`/api/label-profiles/${id}`, profile)
    : await apiClient.post('/api/label-profiles', profile)
  return response.data
}

export async function deleteLabelProfile(id) {
  await apiClient.delete(`/api/label-profiles/${id}`)
}

export async function getFieldDefinitions(labelProfileId) {
  return (await apiClient.get(`/api/label-profiles/${labelProfileId}/fields`)).data
}

export async function saveFieldDefinition(labelProfileId, field, id) {
  const response = id
    ? await apiClient.put(`/api/field-definitions/${id}`, field)
    : await apiClient.post(`/api/label-profiles/${labelProfileId}/fields`, field)
  return response.data
}

export async function deleteFieldDefinition(id) {
  await apiClient.delete(`/api/field-definitions/${id}`)
}

export function getApiErrorMessage(error) {
  const problem = error.response?.data
  if (typeof problem?.title === 'string') {
    return problem.title
  }
  if (typeof problem?.detail === 'string') {
    return problem.detail
  }
  if (problem?.errors && typeof problem.errors === 'object') {
    return Object.values(problem.errors).flat().join(' ')
  }
  if (error.code === 'ECONNABORTED' || error.code === 'ETIMEDOUT') {
    return 'The request timed out. Check whether it completed before retrying.'
  }
  return 'The configuration request failed. Check the API connection and try again.'
}
