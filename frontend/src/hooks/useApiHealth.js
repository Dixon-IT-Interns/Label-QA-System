import { useEffect, useState } from 'react'
import { checkApiHealth } from '../services/apiClient.js'

function useApiHealth() {
  const [state, setState] = useState('checking')

  useEffect(() => {
    const controller = new AbortController()
    checkApiHealth(controller.signal)
      .then(() => setState('connected'))
      .catch((error) => {
        if (!controller.signal.aborted) {
          setState(error.response ? 'error' : 'unavailable')
        }
      })

    return () => controller.abort()
  }, [])

  return state
}

export default useApiHealth
