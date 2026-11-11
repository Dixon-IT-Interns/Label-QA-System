import { useEffect, useMemo, useState } from 'react'
import AuthContext from './AuthContext.js'
import apiClient from '../services/apiClient.js'

export function AuthProvider({ children }) {
  const [user, setUser] = useState(null)
  const [isLoading, setIsLoading] = useState(true)

  useEffect(() => {
    let active = true
    if (!sessionStorage.getItem('accessToken')) {
      setIsLoading(false)
      return () => { active = false }
    }

    apiClient.get('/api/auth/me')
      .then(({ data }) => { if (active) setUser(data) })
      .catch(() => {
        sessionStorage.removeItem('accessToken')
        if (active) setUser(null)
      })
      .finally(() => { if (active) setIsLoading(false) })
    return () => { active = false }
  }, [])

  async function login(identifier, password) {
    const { data } = await apiClient.post('/api/auth/login', { identifier, password })
    sessionStorage.setItem('accessToken', data.accessToken)
    setUser(data.user)
    return data.user
  }

  function logout() {
    sessionStorage.removeItem('accessToken')
    setUser(null)
  }

  const value = useMemo(() => ({
    isAuthenticated: Boolean(user),
    isLoading,
    user,
    login,
    logout,
  }), [isLoading, user])

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
