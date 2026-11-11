import apiClient from './apiClient.js'

export async function getDashboard() {
  return (await apiClient.get('/api/dashboard')).data
}

export async function getUsers() {
  return (await apiClient.get('/api/users')).data
}

export async function saveUser(user, id) {
  const response = id
    ? await apiClient.put(`/api/users/${id}`, user)
    : await apiClient.post('/api/users', user)
  return response.data
}

export async function getSettings() {
  return (await apiClient.get('/api/settings')).data
}

export async function saveOcrConfidence(value) {
  return (await apiClient.put('/api/settings/ocr-confidence', { value })).data
}

export function getAdminErrorMessage(error) {
  const problem = error.response?.data
  if (typeof problem?.title === 'string') return problem.title
  if (typeof problem?.detail === 'string') return problem.detail
  return 'The request failed. Check your permissions and API connection.'
}
