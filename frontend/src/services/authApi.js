import apiClient from './apiClient.js'

export async function signUp(user) {
  return (await apiClient.post('/api/auth/signup', user)).data
}

export async function requestPasswordReset(email) {
  return (await apiClient.post('/api/auth/forgot-password', { email })).data
}

export async function resetPassword({ email, token, password }) {
  return (await apiClient.post('/api/auth/reset-password', { email, token, password })).data
}

export function getAuthErrorMessage(error, fallback) {
  const problem = error.response?.data
  if (typeof problem?.title === 'string') return problem.title
  if (typeof problem?.detail === 'string') return problem.detail
  return fallback
}
