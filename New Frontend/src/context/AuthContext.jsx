import { useEffect, useMemo, useState } from 'react'
import AuthContext from './AuthContext.js'
import apiClient from '../services/apiClient.js'

export function AuthProvider({ children }) {
  const [user, setUser] = useState(null)
  const [isLoading, setIsLoading] = useState(true)

  useEffect(() => {
    let active = true
    if (!localStorage.getItem('accessToken') && !sessionStorage.getItem('accessToken')) {
      setIsLoading(false)
      return () => { active = false }
    }

    apiClient.get('/api/auth/me')
      .then(({ data }) => { if (active) setUser(data) })
      .catch(() => {
        localStorage.removeItem('accessToken')
        sessionStorage.removeItem('accessToken')
        if (active) setUser(null)
      })
      .finally(() => { if (active) setIsLoading(false) })
    return () => { active = false }
  }, [])

  function setAccessToken(accessToken, rememberMe) {
    localStorage.removeItem('accessToken')
    sessionStorage.removeItem('accessToken')
    const storage = rememberMe ? localStorage : sessionStorage
    storage.setItem('accessToken', accessToken)
  }

  async function login(identifier, password, rememberMe = false) {
    const { data } = await apiClient.post('/api/auth/login', { identifier, password })
    setAccessToken(data.accessToken, rememberMe)
    setUser(data.user)
    return data.user
  }

  async function loginWithGoogle(credential, rememberMe = false) {
    const { data } = await apiClient.post('/api/auth/google', { credential })
    setAccessToken(data.accessToken, rememberMe)
    setUser(data.user)
    return data.user
  }

  function logout() {
    localStorage.removeItem('accessToken')
    sessionStorage.removeItem('accessToken')
    setUser(null)
  }

  const value = useMemo(() => ({
    isAuthenticated: Boolean(user),
    isLoading,
    user,
    login,
    loginWithGoogle,
    logout,
  }), [isLoading, user])

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
