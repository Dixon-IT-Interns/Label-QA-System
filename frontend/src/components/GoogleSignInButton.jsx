import { useEffect, useRef } from 'react'

function GoogleSignInButton({ onCredential, onError }) {
  const containerRef = useRef(null)
  const activeRef = useRef(false)
  const clientId = import.meta.env.VITE_GOOGLE_CLIENT_ID?.trim()

  useEffect(() => {
    if (!clientId || !containerRef.current) return undefined

    activeRef.current = true
    const handleScriptError = () => {
      if (activeRef.current) onError('Google sign-in could not be loaded.')
    }
    const initialize = () => {
      if (!activeRef.current || !containerRef.current || !window.google?.accounts?.id) return
      containerRef.current.replaceChildren()
      window.google.accounts.id.initialize({
        client_id: clientId,
        callback: (response) => {
          if (response.credential) onCredential(response.credential)
          else onError('Google sign-in did not return a credential.')
        },
      })
      window.google.accounts.id.renderButton(containerRef.current, {
        theme: 'outline',
        size: 'large',
        shape: 'pill',
        text: 'continue_with',
        width: Math.max(220, Math.floor(containerRef.current.getBoundingClientRect().width)),
      })
    }

    let script = document.getElementById('google-identity-services')
    if (!script) {
      script = document.createElement('script')
      script.id = 'google-identity-services'
      script.src = 'https://accounts.google.com/gsi/client'
      script.async = true
      script.defer = true
      document.head.appendChild(script)
    }

    if (window.google?.accounts?.id) initialize()
    else {
      script.addEventListener('load', initialize)
      script.addEventListener('error', handleScriptError)
    }

    return () => {
      activeRef.current = false
      script?.removeEventListener('load', initialize)
      script?.removeEventListener('error', handleScriptError)
    }
  }, [clientId, onCredential, onError])

  return clientId
    ? <div aria-label="Continue with Google" className="google-signin-container" ref={containerRef} />
    : <div className="google-signin-unavailable"><button className="button button-google" disabled type="button"><span aria-hidden="true">G</span> Continue with Google</button><small>Google sign-in isn&apos;t configured yet.</small></div>
}

export default GoogleSignInButton
