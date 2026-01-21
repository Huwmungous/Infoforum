/**
 * PatientAuthService - Handles direct authentication for patients
 * Uses Resource Owner Password Credentials (ROPC) flow
 */

const CLIENT_ID = 'dev-login-pps';

class PatientAuthService {
  constructor() {
    this.user = null;
  }

  /**
   * Get the token endpoint
   * @param {string} openIdConfig - The OpenID config URL (e.g., "https://host/realms/RealmName")
   */
  getTokenEndpoint(openIdConfig) {
    return `${openIdConfig}/protocol/openid-connect/token`;
  }

  /**
   * Get the logout endpoint
   * @param {string} openIdConfig - The OpenID config URL
   */
  getLogoutEndpoint(openIdConfig) {
    return `${openIdConfig}/protocol/openid-connect/logout`;
  }

  /**
   * Format date of birth as YYYYMMDD
   */
  formatDob(dateOfBirth) {
    const date = new Date(dateOfBirth);
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}${month}${day}`;
  }

  /**
   * Construct username from patient details
   * Format: firstname.lastname.YYYYMMDD (lowercase)
   */
  constructUsername(firstName, lastName, dateOfBirth) {
    const dob = this.formatDob(dateOfBirth);
    return `${firstName.toLowerCase().trim()}.${lastName.toLowerCase().trim()}.${dob}`;
  }

  /**
   * Format date of birth as password (YYYYMMDD)
   */
  formatDobAsPassword(dateOfBirth) {
    return this.formatDob(dateOfBirth);
  }

  /**
   * Authenticate patient using direct grant (ROPC flow)
   * @param {string} firstName
   * @param {string} lastName
   * @param {string} dateOfBirth
   * @param {string} openIdConfig - The OpenID config URL from useAppContext().config.openIdConfig
   */
  async authenticate(firstName, lastName, dateOfBirth, openIdConfig) {
    const username = this.constructUsername(firstName, lastName, dateOfBirth);
    const password = this.formatDobAsPassword(dateOfBirth);

    const params = new URLSearchParams({
      grant_type: 'password',
      client_id: CLIENT_ID,
      username: username,
      password: password,
      scope: 'openid profile email',
    });

    try {
      const response = await fetch(this.getTokenEndpoint(openIdConfig), {
        method: 'POST',
        headers: {
          'Content-Type': 'application/x-www-form-urlencoded',
        },
        body: params.toString(),
      });

      if (!response.ok) {
        const errorData = await response.json().catch(() => ({}));
        const errorMessage = errorData.error_description || errorData.error || `HTTP ${response.status}`;
        throw new Error(`Authentication failed: ${errorMessage}`);
      }

      const tokenData = await response.json();
      
      // Decode the ID token to get user profile
      const profile = this.decodeJWT(tokenData.id_token);
      
      this.user = {
        access_token: tokenData.access_token,
        id_token: tokenData.id_token,
        refresh_token: tokenData.refresh_token,
        token_type: tokenData.token_type,
        expires_in: tokenData.expires_in,
        expires_at: Math.floor(Date.now() / 1000) + tokenData.expires_in,
        profile: profile,
      };

      // Store in session storage for persistence within the tab
      sessionStorage.setItem('patient_user', JSON.stringify(this.user));
      
      return this.user;
    } catch (error) {
      console.error('Patient authentication error:', error);
      throw error;
    }
  }

  /**
   * Refresh the access token using the refresh token
   * @param {string} openIdConfig - The OpenID config URL from useAppContext().config.openIdConfig
   */
  async renewToken(openIdConfig) {
    if (!this.user?.refresh_token) {
      throw new Error('No refresh token available');
    }

    const params = new URLSearchParams({
      grant_type: 'refresh_token',
      client_id: CLIENT_ID,
      refresh_token: this.user.refresh_token,
    });

    try {
      const response = await fetch(this.getTokenEndpoint(openIdConfig), {
        method: 'POST',
        headers: {
          'Content-Type': 'application/x-www-form-urlencoded',
        },
        body: params.toString(),
      });

      if (!response.ok) {
        const errorData = await response.json().catch(() => ({}));
        throw new Error(errorData.error_description || 'Token renewal failed');
      }

      const tokenData = await response.json();
      const profile = this.decodeJWT(tokenData.id_token);
      
      this.user = {
        access_token: tokenData.access_token,
        id_token: tokenData.id_token,
        refresh_token: tokenData.refresh_token,
        token_type: tokenData.token_type,
        expires_in: tokenData.expires_in,
        expires_at: Math.floor(Date.now() / 1000) + tokenData.expires_in,
        profile: profile,
      };

      sessionStorage.setItem('patient_user', JSON.stringify(this.user));
      
      return this.user;
    } catch (error) {
      console.error('Token renewal error:', error);
      throw error;
    }
  }

  /**
   * Get current user from memory or session storage
   */
  getUser() {
    if (this.user) {
      return this.user;
    }
    
    const stored = sessionStorage.getItem('patient_user');
    if (stored) {
      this.user = JSON.parse(stored);
      return this.user;
    }
    
    return null;
  }

  /**
   * Logout - clear stored tokens
   */
  logout() {
    this.user = null;
    sessionStorage.removeItem('patient_user');
  }

  /**
   * Decode JWT token payload
   */
  decodeJWT(token) {
    try {
      const base64Url = token.split('.')[1];
      const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
      const jsonPayload = decodeURIComponent(
        atob(base64)
          .split('')
          .map((c) => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2))
          .join('')
      );
      return JSON.parse(jsonPayload);
    } catch (err) {
      return null;
    }
  }
}

// Export singleton instance
export const patientAuthService = new PatientAuthService();
export default patientAuthService;
