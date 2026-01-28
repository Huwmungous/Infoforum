import { useState, useEffect, useRef } from 'react'

// PKCE helpers
function generateCodeVerifier() {
  const array = new Uint8Array(32)
  crypto.getRandomValues(array)
  return base64UrlEncode(array)
}

async function generateCodeChallenge(verifier) {
  const encoder = new TextEncoder()
  const data = encoder.encode(verifier)
  const hash = await crypto.subtle.digest('SHA-256', data)
  return base64UrlEncode(new Uint8Array(hash))
}

function base64UrlEncode(buffer) {
  let binary = ''
  const bytes = new Uint8Array(buffer)
  for (let i = 0; i < bytes.length; i++) {
    binary += String.fromCharCode(bytes[i])
  }
  return btoa(binary)
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/, '')
}

function parseJwt(token) {
  try {
    const base64Url = token.split('.')[1]
    let base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/')
    const pad = base64.length % 4
    if (pad) base64 += '='.repeat(4 - pad)
    const jsonPayload = decodeURIComponent(
      atob(base64)
        .split('')
        .map(c => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2))
        .join('')
    )
    return JSON.parse(jsonPayload)
  } catch {
    return null
  }
}

function App() {
  // Connection settings
  const [loggerUrl, setLoggerUrl] = useState('https://longmanrd.net/logger')
  const [authority, setAuthority] = useState('https://longmanrd.net/auth/realms/LongmanRd')
  const [realm, setRealm] = useState('LongmanRd')
  const [clientId, setClientId] = useState('infoforum-user')
  
  // Auth state
  const [accessToken, setAccessToken] = useState(null)
  const [tokenExpiry, setTokenExpiry] = useState(null)
  const [username, setUsername] = useState(null)
  const [authStatus, setAuthStatus] = useState({ text: 'Not authenticated', color: 'gray' })
  
  // Log entry fields
  const [application, setApplication] = useState('LogSender')
  const [category, setCategory] = useState('TestController')
  const [level, setLevel] = useState('Information')
  const [environment, setEnvironment] = useState('DEV')
  const [message, setMessage] = useState('Test log message from LogSender')
  const [count, setCount] = useState(5)
  
  // Status log
  const [statusLog, setStatusLog] = useState([])
  const [isSending, setIsSending] = useState(false)
  const statusRef = useRef(null)
  
  // PKCE state stored in sessionStorage
  const pkceKey = 'logsender_pkce'

  const logLevels = ['Trace', 'Debug', 'Information', 'Warning', 'Error', 'Critical']

  const addStatus = (msg) => {
    const timestamp = new Date().toTimeString().slice(0, 12)
    setStatusLog(prev => [...prev, `[${timestamp}] ${msg}`].slice(-100))
  }

  // Scroll status to bottom when updated
  useEffect(() => {
    if (statusRef.current) {
      statusRef.current.scrollTop = statusRef.current.scrollHeight
    }
  }, [statusLog])

  // Check for OAuth callback on mount
  useEffect(() => {
    const params = new URLSearchParams(window.location.search)
    const code = params.get('code')
    const state = params.get('state')
    const error = params.get('error')

    if (error) {
      addStatus(`Login failed: ${error}`)
      setAuthStatus({ text: `Login failed: ${error}`, color: 'red' })
      window.history.replaceState({}, '', window.location.pathname)
      return
    }

    if (code && state) {
      handleCallback(code, state)
    }
  }, [])

  const handleCallback = async (code, state) => {
    try {
      setAuthStatus({ text: 'Exchanging code for tokens...', color: 'orange' })
      
      // Retrieve PKCE data from sessionStorage
      const pkceData = JSON.parse(sessionStorage.getItem(pkceKey) || '{}')
      
      if (pkceData.state !== state) {
        throw new Error('State mismatch - possible CSRF attack')
      }

      const tokenUrl = `${pkceData.authority}/protocol/openid-connect/token`
      const redirectUri = window.location.origin + window.location.pathname

      const response = await fetch(tokenUrl, {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
        body: new URLSearchParams({
          grant_type: 'authorization_code',
          client_id: pkceData.clientId,
          code: code,
          redirect_uri: redirectUri,
          code_verifier: pkceData.codeVerifier
        })
      })

      const tokenData = await response.json()

      if (!response.ok) {
        throw new Error(tokenData.error_description || tokenData.error || 'Token exchange failed')
      }

      const token = tokenData.access_token
      const expiresIn = tokenData.expires_in
      const expiry = new Date(Date.now() + (expiresIn - 30) * 1000)

      setAccessToken(token)
      setTokenExpiry(expiry)

      const claims = parseJwt(token)
      const user = claims?.preferred_username || claims?.sub || 'unknown'
      setUsername(user)

      addStatus(`Login successful! Token expires in ${expiresIn}s`)
      setAuthStatus({ 
        text: `Logged in as ${user}. Expires: ${expiry.toLocaleTimeString()}`, 
        color: 'green' 
      })

      // Clean up
      sessionStorage.removeItem(pkceKey)
      window.history.replaceState({}, '', window.location.pathname)
    } catch (err) {
      addStatus(`Auth error: ${err.message}`)
      setAuthStatus({ text: `Error: ${err.message}`, color: 'red' })
      sessionStorage.removeItem(pkceKey)
      window.history.replaceState({}, '', window.location.pathname)
    }
  }

  const handleLogin = async () => {
    try {
      setAuthStatus({ text: 'Starting login...', color: 'orange' })
      addStatus('Starting PKCE login flow...')

      const codeVerifier = generateCodeVerifier()
      const codeChallenge = await generateCodeChallenge(codeVerifier)
      const state = crypto.randomUUID().replace(/-/g, '')
      const redirectUri = window.location.origin + window.location.pathname

      // Store PKCE data for callback
      sessionStorage.setItem(pkceKey, JSON.stringify({
        codeVerifier,
        state,
        authority,
        clientId
      }))

      const authUrl = `${authority}/protocol/openid-connect/auth?` +
        `client_id=${encodeURIComponent(clientId)}&` +
        `redirect_uri=${encodeURIComponent(redirectUri)}&` +
        `response_type=code&` +
        `scope=openid&` +
        `state=${state}&` +
        `code_challenge=${codeChallenge}&` +
        `code_challenge_method=S256`

      addStatus('Redirecting to Keycloak...')
      window.location.href = authUrl
    } catch (err) {
      addStatus(`Login error: ${err.message}`)
      setAuthStatus({ text: `Error: ${err.message}`, color: 'red' })
    }
  }

  const handleLogout = () => {
    setAccessToken(null)
    setTokenExpiry(null)
    setUsername(null)
    setAuthStatus({ text: 'Not authenticated', color: 'gray' })
    addStatus('Logged out')
  }

  const isTokenValid = () => {
    return accessToken && tokenExpiry && new Date() < tokenExpiry
  }

  const sendLogEntry = async (messageOverride) => {
    if (!isTokenValid()) {
      addStatus('Not authenticated or token expired. Please login first.')
      return false
    }

    try {
      const endpoint = `${loggerUrl.replace(/\/$/, '')}/api/logs`

      const logData = {
        timestamp: new Date().toISOString(),
        level: level,
        category: category,
        message: messageOverride || message,
        application: application,
        environment: environment,
        machineName: 'Browser'
      }

      const request = {
        realm: realm,
        client: clientId,
        environment: environment,
        application: application,
        logLevel: level,
        logData: logData
      }

      addStatus(`Sending ${level}: ${logData.message}`)

      const response = await fetch(endpoint, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${accessToken}`
        },
        body: JSON.stringify(request)
      })

      const responseBody = await response.text()

      if (response.ok) {
        addStatus(`Log sent successfully. Response: ${responseBody}`)
        return true
      } else {
        addStatus(`Failed to send log: ${response.status}`)
        addStatus(responseBody)
        return false
      }
    } catch (err) {
      addStatus(`Error sending log: ${err.message}`)
      return false
    }
  }

  const handleSendLog = async () => {
    setIsSending(true)
    await sendLogEntry()
    setIsSending(false)
  }

  const handleSendMultiple = async () => {
    setIsSending(true)
    addStatus(`Sending ${count} log entries...`)

    for (let i = 1; i <= count; i++) {
      await sendLogEntry(`${message} [${i}/${count}]`)
      if (i < count) {
        await new Promise(r => setTimeout(r, 100))
      }
    }

    addStatus(`Completed sending ${count} log entries.`)
    setIsSending(false)
  }

  const getLevelStyle = (lvl) => {
    const styles = {
      'Trace': { bg: '#666', color: '#fff' },
      'Debug': { bg: '#888', color: '#fff' },
      'Information': { bg: '#33ff33', color: '#000' },
      'Warning': { bg: '#ffcc00', color: '#000' },
      'Error': { bg: '#ff6633', color: '#fff' },
      'Critical': { bg: '#ff3333', color: '#fff' }
    }
    return styles[lvl] || { bg: '#888', color: '#fff' }
  }

  return (
    <div style={styles.container}>
      <h1 style={styles.title}>Log Sender</h1>

      {/* Connection Group */}
      <fieldset style={styles.fieldset}>
        <legend style={styles.legend}>Connection</legend>
        
        <div style={styles.row}>
          <label style={styles.label}>Logger URL:</label>
          <input
            style={styles.input}
            value={loggerUrl}
            onChange={e => setLoggerUrl(e.target.value)}
          />
        </div>
        
        <div style={styles.row}>
          <label style={styles.label}>Authority:</label>
          <input
            style={styles.input}
            value={authority}
            onChange={e => setAuthority(e.target.value)}
          />
        </div>
        
        <div style={styles.row}>
          <label style={styles.label}>Realm:</label>
          <input
            style={{ ...styles.input, width: '150px' }}
            value={realm}
            onChange={e => setRealm(e.target.value)}
          />
          <label style={{ ...styles.label, marginLeft: '20px' }}>Client ID:</label>
          <input
            style={{ ...styles.input, width: '200px' }}
            value={clientId}
            onChange={e => setClientId(e.target.value)}
          />
        </div>
        
        <div style={styles.row}>
          {!accessToken ? (
            <button style={styles.button} onClick={handleLogin}>
              Login (Browser)
            </button>
          ) : (
            <button style={styles.buttonSecondary} onClick={handleLogout}>
              Logout
            </button>
          )}
          <span style={{ ...styles.authStatus, color: authStatus.color }}>
            {authStatus.text}
          </span>
        </div>
      </fieldset>

      {/* Log Entry Group */}
      <fieldset style={styles.fieldset}>
        <legend style={styles.legend}>Log Entry</legend>
        
        <div style={styles.row}>
          <label style={styles.label}>Application:</label>
          <input
            style={{ ...styles.input, width: '180px' }}
            value={application}
            onChange={e => setApplication(e.target.value)}
          />
          <label style={{ ...styles.label, marginLeft: '20px' }}>Category:</label>
          <input
            style={{ ...styles.input, width: '200px' }}
            value={category}
            onChange={e => setCategory(e.target.value)}
          />
        </div>
        
        <div style={styles.row}>
          <label style={styles.label}>Level:</label>
          <div style={styles.levelButtons}>
            {logLevels.map(lvl => {
              const style = getLevelStyle(lvl)
              const isSelected = level === lvl
              return (
                <button
                  key={lvl}
                  style={{
                    ...styles.levelButton,
                    backgroundColor: isSelected ? style.bg : '#444',
                    color: isSelected ? style.color : '#aaa',
                    border: isSelected ? '2px solid #fff' : '2px solid transparent'
                  }}
                  onClick={() => setLevel(lvl)}
                >
                  {lvl.charAt(0)}
                </button>
              )
            })}
            <span style={{ marginLeft: '10px', color: '#aaa' }}>{level}</span>
          </div>
          <label style={{ ...styles.label, marginLeft: '20px' }}>Environment:</label>
          <input
            style={{ ...styles.input, width: '80px' }}
            value={environment}
            onChange={e => setEnvironment(e.target.value)}
          />
        </div>
        
        <div style={styles.row}>
          <label style={styles.label}>Message:</label>
          <textarea
            style={styles.textarea}
            value={message}
            onChange={e => setMessage(e.target.value)}
            rows={3}
          />
        </div>
        
        <div style={styles.row}>
          <button 
            style={styles.button} 
            onClick={handleSendLog}
            disabled={isSending || !isTokenValid()}
          >
            Send Log
          </button>
          <label style={{ ...styles.label, marginLeft: '20px' }}>Count:</label>
          <input
            type="number"
            min="1"
            max="100"
            style={{ ...styles.input, width: '60px', textAlign: 'center' }}
            value={count}
            onChange={e => setCount(parseInt(e.target.value) || 1)}
          />
          <button 
            style={{ ...styles.button, marginLeft: '10px' }}
            onClick={handleSendMultiple}
            disabled={isSending || !isTokenValid()}
          >
            Send Multiple
          </button>
        </div>
      </fieldset>

      {/* Status Console */}
      <div ref={statusRef} style={styles.statusConsole}>
        {statusLog.length === 0 ? (
          <span style={{ color: '#666' }}>Ready...</span>
        ) : (
          statusLog.map((line, i) => (
            <div key={i}>{line}</div>
          ))
        )}
      </div>
    </div>
  )
}

const styles = {
  container: {
    fontFamily: 'Segoe UI, Tahoma, Geneva, Verdana, sans-serif',
    maxWidth: '600px',
    margin: '0 auto',
    padding: '20px',
    backgroundColor: '#1a1a1a',
    minHeight: '100vh',
    color: '#e0e0e0'
  },
  title: {
    color: '#33ff33',
    marginBottom: '20px',
    fontSize: '24px'
  },
  fieldset: {
    border: '1px solid #444',
    borderRadius: '4px',
    padding: '15px',
    marginBottom: '20px'
  },
  legend: {
    color: '#33ff33',
    padding: '0 10px',
    fontSize: '14px'
  },
  row: {
    display: 'flex',
    alignItems: 'center',
    marginBottom: '10px',
    flexWrap: 'wrap',
    gap: '5px'
  },
  label: {
    width: '80px',
    flexShrink: 0,
    fontSize: '13px',
    color: '#aaa'
  },
  input: {
    flex: 1,
    padding: '6px 10px',
    backgroundColor: '#333',
    border: '1px solid #555',
    borderRadius: '3px',
    color: '#e0e0e0',
    fontSize: '13px',
    outline: 'none'
  },
  textarea: {
    flex: 1,
    padding: '6px 10px',
    backgroundColor: '#333',
    border: '1px solid #555',
    borderRadius: '3px',
    color: '#e0e0e0',
    fontSize: '13px',
    outline: 'none',
    resize: 'vertical',
    fontFamily: 'inherit'
  },
  button: {
    padding: '8px 16px',
    backgroundColor: '#2a7a2a',
    border: 'none',
    borderRadius: '3px',
    color: '#fff',
    fontSize: '13px',
    cursor: 'pointer'
  },
  buttonSecondary: {
    padding: '8px 16px',
    backgroundColor: '#555',
    border: 'none',
    borderRadius: '3px',
    color: '#fff',
    fontSize: '13px',
    cursor: 'pointer'
  },
  authStatus: {
    marginLeft: '15px',
    fontSize: '12px'
  },
  levelButtons: {
    display: 'flex',
    alignItems: 'center',
    gap: '4px'
  },
  levelButton: {
    width: '28px',
    height: '28px',
    border: 'none',
    borderRadius: '3px',
    cursor: 'pointer',
    fontSize: '12px',
    fontWeight: 'bold'
  },
  statusConsole: {
    backgroundColor: '#000',
    border: '1px solid #333',
    borderRadius: '3px',
    padding: '10px',
    height: '150px',
    overflowY: 'auto',
    fontFamily: 'Consolas, Monaco, monospace',
    fontSize: '12px',
    color: '#33ff33',
    lineHeight: '1.4'
  }
}

export default App
