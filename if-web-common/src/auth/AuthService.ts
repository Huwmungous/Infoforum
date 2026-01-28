import { User, UserManager, UserManagerSettings, WebStorageStateStore } from 'oidc-client-ts';
import { ConfigService } from '../config';
import { LoggerService } from '../logger';

export interface AuthConfig {
  oidcConfigUrl?: string;
  realm?: string; // Optional - will use ConfigService.Realm if not provided
  clientId?: string; // Optional - will use ConfigService.ClientId if not provided
  redirectUri: string;
  scope?: string;
  postLogoutRedirectUri?: string;
  automaticSilentRenew?: boolean;
  silentRedirectUri?: string;
  // Direct OIDC configuration (alternative to oidcConfigUrl)
  authority?: string; // Optional - will use ConfigService.OpenIdConfig if not provided
  metadata?: any;
}

export type UserChangeCallback = (user: User | null) => void;

class AuthService {
  private static _instance: AuthService | null = null;

  private userManager: UserManager | null = null;
  private config: AuthConfig | null = null;
  private _logger: ReturnType<typeof LoggerService.create> | null = null;
  private userChangeCallbacks: Set<UserChangeCallback> = new Set();

  // --- “Anti machine-gun” storm suppression state ---
  private expiringNotificationSeconds: number = 60; // oidc-client-ts default if unset
  private endOfSessionStormMode: boolean = false;
  private endOfSessionStormLogged: boolean = false;
  private silentRenewStoppedByStorm: boolean = false;

  // --- Renewal failure cooldown ---
  private renewalFailureCount: number = 0;
  private renewalCooldownUntil: number = 0;
  private readonly maxRenewalFailures: number = 3;
  private readonly renewalCooldownMs: number = 30000; // 30 seconds cooldown after failures

  // Private constructor prevents direct instantiation
  private constructor() {}
  
  /**
   * Get the singleton instance of AuthService
   */
  public static getInstance(): AuthService {
    if (!AuthService._instance) {
      AuthService._instance = new AuthService();
    }
    return AuthService._instance;
  }
  
  // Lazy-initialize the logger to avoid creating it before ConfigService is ready
  private get logger(): ReturnType<typeof LoggerService.create> {
    if (!this._logger) {
      this._logger = LoggerService.create('AuthService');
    }
    return this._logger;
  }

  private notifyUserChange(user: User | null): void {
    // Calling setState in the callback triggers React re-render
    // (the state object changes even if the user reference doesn't)
    this.userChangeCallbacks.forEach(callback => callback(user));
  }

  onUserChange(callback: UserChangeCallback): () => void {
    this.userChangeCallbacks.add(callback);
    return () => this.userChangeCallbacks.delete(callback);
  }

  async initialize(config: AuthConfig): Promise<void> {
    // Prevent re-initialization - UserManager already exists
    if (this.userManager) {
      return;
    }

    this.config = config;

    // FIXED: Ensure ConfigService is initialized with better error message
    if (!ConfigService.isInitialized) {
      throw new Error(
        'ConfigService must be initialized before AuthService. ' +
        'Wrap your app with <AppInitializer> to ensure proper initialization order.'
      );
    }

    // Use ConfigService values as defaults if not provided in config
    const clientId = config.clientId || ConfigService.ClientId;
    const authority = config.authority || ConfigService.OpenIdConfig;

    let settings: UserManagerSettings;

    if (config.oidcConfigUrl) {
      // Fetch OIDC configuration
      const response = await fetch(config.oidcConfigUrl);
      if (!response.ok) {
        throw new Error(`Failed to fetch OIDC configuration: ${response.statusText}`);
      }
      const metadata = await response.json();

      settings = {
        authority: metadata.issuer,
        client_id: clientId,
        redirect_uri: config.redirectUri,
        scope: config.scope || 'openid profile email',
        response_type: 'code',
        post_logout_redirect_uri: config.postLogoutRedirectUri || config.redirectUri,
        automaticSilentRenew: config.automaticSilentRenew !== false,
        silent_redirect_uri: config.silentRedirectUri || config.redirectUri,
        userStore: new WebStorageStateStore({ store: window.localStorage }),
        metadata,
        accessTokenExpiringNotificationTimeInSeconds: this.expiringNotificationSeconds,        
      };
    } else {
      settings = {
        authority: authority,
        client_id: clientId,
        redirect_uri: config.redirectUri,
        scope: config.scope || 'openid profile email',
        response_type: 'code',
        post_logout_redirect_uri: config.postLogoutRedirectUri || config.redirectUri,
        automaticSilentRenew: config.automaticSilentRenew !== false,
        silent_redirect_uri: config.silentRedirectUri || config.redirectUri,
        userStore: new WebStorageStateStore({ store: window.localStorage }),
        metadata: config.metadata,
        accessTokenExpiringNotificationTimeInSeconds: this.expiringNotificationSeconds,
      };
    }

    this.userManager = new UserManager(settings);

    // Set up event handlers
    this.userManager.events.addAccessTokenExpiring(() => {
      // Avoid log storms if we’ve deliberately disabled auto-renew at end-of-session.
      if (!this.endOfSessionStormMode) {
        this.logger.debug('Access token is about to expire, attempting silent renewal');
      }
    });

    this.userManager.events.addAccessTokenExpired(async () => {
      this.logger.warn('Access token has expired');
      // If we’re in storm mode, we intentionally stopped auto-renew; treat expiry as “session ended”.
      if (this.endOfSessionStormMode) {
        await this.userManager?.removeUser();
        this.notifyUserChange(null);
        return;
      }

      const user = await this.userManager?.getUser();
      this.notifyUserChange(user ?? null);
    });

    this.userManager.events.addSilentRenewError((error) => {
      this.renewalFailureCount++;
      
      // In storm mode we expect "invalid_grant" etc. near session end; log once to avoid hammering logs.
      if (!this.endOfSessionStormMode) {
        this.logger.error('Silent token renewal failed');
      } else if (!this.endOfSessionStormLogged) {
        this.endOfSessionStormLogged = true;
        this.logger.warn('Silent renew failed near end-of-session; auto-renew is disabled to prevent refresh storms');
      }

      // After maxRenewalFailures, stop trying for a cooldown period
      if (this.renewalFailureCount >= this.maxRenewalFailures) {
        this.renewalCooldownUntil = Date.now() + this.renewalCooldownMs;
        this.userManager?.stopSilentRenew();
        this.logger.warn(
          `Silent renewal failed ${this.renewalFailureCount} times. ` +
          `Pausing renewal attempts for ${this.renewalCooldownMs / 1000}s. User may need to re-authenticate.`
        );
        
        // Schedule re-enabling after cooldown
        setTimeout(() => {
          if (Date.now() >= this.renewalCooldownUntil) {
            this.renewalFailureCount = 0;
            this.userManager?.startSilentRenew();
            this.logger.info('Silent renewal re-enabled after cooldown period');
          }
        }, this.renewalCooldownMs);
      }
    });

    this.userManager.events.addUserLoaded((user) => {
      this.logger.debug('User loaded/renewed');
      this.renewalFailureCount = 0; // Reset failure count on successful load/renewal
      this.applyEndOfSessionStormSuppression(user);
      this.notifyUserChange(user);
    });

    this.userManager.events.addUserUnloaded(() => {
      this.logger.info('User unloaded');
      this.notifyUserChange(null);
    });

    this.userManager.events.addUserSignedOut(() => {
      this.logger.info('User was signed out');
      this.notifyUserChange(null);
    });
  }

  async signin(): Promise<void> {
    if (!this.userManager)
      throw new Error('AuthService not initialized');

    this.logger.debug('Initiating signin redirect');
    await this.userManager.signinRedirect();
  }

  /**
   * Storm suppression for oidc-client-ts refresh-token renew loops near end-of-session.
   *
   * Background:
   * - When using refresh tokens, oidc-client-ts will renew the access token automatically.
   * - Near the end of the IdP session (commonly reproducible with short Keycloak session settings),
   *   the newly-issued access token can have remaining lifetime <= accessTokenExpiringNotificationTimeInSeconds.
   * - That causes AccessTokenExpiring to fire immediately again, triggering rapid refresh_token requests (“machine-gun”).
   *
   * Upstream tracking:
   * - Repo: authts/oidc-client-ts
   * - Issue: #644 "Refresh token renewal" (constant refresh cycle when refresh token/session expiry is within expiring window)
   *   https://github.com/authts/oidc-client-ts/issues/644
   *
   * Mitigation:
   * - Detect this condition on successful renewal (UserLoaded) and stop automatic silent renew.
   * - This prevents hammering the token endpoint and flooding our logs/services.
   */
  private applyEndOfSessionStormSuppression(user: User): void {
    if (!this.userManager) return;
    if (this.endOfSessionStormMode) return;
    if (!user.expires_at) return;

    const nowSec = Math.floor(Date.now() / 1000);
    const secondsLeft = user.expires_at - nowSec;

    // Small buffer for clock skew / scheduling jitter.
    const bufferSeconds = 2;
    const threshold = this.expiringNotificationSeconds;

    // If the *new* token is already inside the “expiring soon” window, we’re in the danger zone.
    if (secondsLeft <= (threshold + bufferSeconds)) {
      this.endOfSessionStormMode = true;
      this.silentRenewStoppedByStorm = true;
      this.userManager.stopSilentRenew();

      if (!this.endOfSessionStormLogged) {
        this.endOfSessionStormLogged = true;
        this.logger.warn(
          `End-of-session detected: token lifetime (${secondsLeft}s) is within expiring window (${threshold}s). ` +
          `Disabling automatic silent renew to prevent refresh-token storm (see oidc-client-ts #644).`
        );
      }
   }
  }

  async completeSignin(): Promise<User> {
    if (!this.userManager) {
      throw new Error('AuthService not initialized');
    }
    this.logger.debug('Processing signin callback');
    const user = await this.userManager.signinRedirectCallback();
    const username = user?.profile.preferred_username || user?.profile.email || 'unknown';
    this.logger.info(`User authenticated successfully: ${username}`);

    // If we disabled auto-renew due to end-of-session storm suppression, re-enable it for the new session.
    if (this.silentRenewStoppedByStorm) {
      this.endOfSessionStormMode = false;
      this.endOfSessionStormLogged = false;
      this.silentRenewStoppedByStorm = false;
      this.renewalFailureCount = 0;
      this.userManager.startSilentRenew();
      this.logger.info('Automatic silent renew re-enabled after interactive sign-in');
    }

    return user!;
  }

  async signout(): Promise<void> {
    if (!this.userManager) {
      throw new Error('AuthService not initialized');
    }
    this.logger.debug('Initiating signout redirect');
    await this.userManager.signoutRedirect();
  }

  async completeSignout(): Promise<void> {
    if (!this.userManager) {
      throw new Error('AuthService not initialized');
    }
    this.logger.debug('Processing signout callback');
    await this.userManager.signoutRedirectCallback();
    this.logger.info('User signed out successfully');

    // Reset storm suppression for next session.
    this.endOfSessionStormMode = false;
    this.endOfSessionStormLogged = false;
    this.silentRenewStoppedByStorm = false;
    this.renewalFailureCount = 0;
  }

  async renewToken(): Promise<User> {
    if (!this.userManager)
      throw new Error('AuthService not initialized');

    this.logger.debug('Attempting silent token renewal');
    const user = await this.userManager.signinSilent();

    if (!user) {
      throw new Error('Silent renewal failed: no user returned');
    }

    const username = user.profile.preferred_username || user.profile.email || 'unknown';
    const expiresAt = user.expires_at ? new Date(user.expires_at * 1000).toISOString() : 'unknown';
    this.logger.info(`Token renewed successfully for user ${username}. Expires at: ${expiresAt}`);

    return user;
  }

  async getUser(): Promise<User | null> {
    if (!this.userManager) {
      throw new Error('AuthService not initialized');
    }
    return await this.userManager.getUser();
  }

  async getAccessToken(): Promise<string | null> {
    if (!this.userManager) {
      return null;
    }
    const user = await this.getUser();
    return user?.access_token || null;
  }

  async removeUser(): Promise<void> {
    if (!this.userManager) {
      throw new Error('AuthService not initialized');
    }
    await this.userManager.removeUser();

    // Reset storm suppression for next session.
    this.endOfSessionStormMode = false;
    this.endOfSessionStormLogged = false;
    this.silentRenewStoppedByStorm = false;
    this.renewalFailureCount = 0;
  }

  async completeSilentSignin(): Promise<void> {
    if (!this.userManager) {
      throw new Error('AuthService not initialized');
    }
    await this.userManager.signinSilentCallback();
  }

  /**
   * Clear stale OIDC state entries from storage.
   * Call this before restarting auth flow after state-related errors.
   */
  async clearStaleState(): Promise<void> {
    if (!this.userManager) {
      throw new Error('AuthService not initialized');
    }
    await this.userManager.clearStaleState();
    this.logger.info('Cleared stale OIDC state from storage');
  }

  /**
   * Check if an error is a state-related error that requires restarting the auth flow.
   * These errors occur when:
   * - User bookmarked/refreshed the callback page
   * - User delayed completing login (state expired)
   * - State storage was cleared (different browser tab, cleared storage)
   * - Configuration changed between signin start and callback
   */
  isStateError(error: Error | unknown): boolean {
    const message = error instanceof Error ? error.message : String(error);
    const stateErrorPatterns = [
      'No matching state found',
      'Incorrect redirect_uri',
      'No state in response',
      'state not found',
      'invalid state',
    ];
    return stateErrorPatterns.some(pattern =>
      message.toLowerCase().includes(pattern.toLowerCase())
    );
  }

  /**
   * Check if an error is an IdP error response indicating the auth request expired or was invalidated.
   * These errors come from the authorization server (e.g., Keycloak) when:
   * - The user took too long on the login page
   * - Another tab/session completed login, invalidating this auth request
   * - The IdP session expired while user was on login page
   *
   * These are returned as query params: ?error=temporarily_unavailable&error_description=authentication_expired
   */
  isAuthExpiredError(error: Error | unknown): boolean {
    const message = error instanceof Error ? error.message : String(error);
    const authExpiredPatterns = [
      'authentication_expired',
      'login_required',
      'session_expired',
      'temporarily_unavailable',
      'interaction_required',
      'expired_login_hint_token',
    ];
    return authExpiredPatterns.some(pattern =>
      message.toLowerCase().includes(pattern.toLowerCase())
    );
  }

  /**
   * Check if an error should trigger automatic restart of the auth flow.
   * Combines state errors and IdP auth-expired errors.
   */
  shouldRestartAuth(error: Error | unknown): boolean {
    return this.isStateError(error) || this.isAuthExpiredError(error);
  }

  /**
   * Reset the singleton instance (for testing only)
   */
  public static reset(): void {
    if (AuthService._instance) {
      AuthService._instance.userManager = null;
      AuthService._instance.config = null;
      AuthService._instance._logger = null;
    }
    AuthService._instance = null;
  }
}

// Export the class for getInstance() access and testing
export { AuthService };

// Export singleton instance for backward compatibility
export const authService = AuthService.getInstance();