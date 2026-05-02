import { Injectable, signal, computed, inject, NgZone, OnDestroy } from '@angular/core';
import { Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { lastValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthUser, AuthResponse } from '../models/auth.model';

declare const google: {
  accounts: {
    id: {
      initialize: (config: object) => void;
      renderButton: (element: HTMLElement, config: object) => void;
      disableAutoSelect: () => void;
    };
  };
} | undefined;

const TOKEN_KEY = 'trip-advisor-token';
const USER_KEY = 'trip-advisor-user';
const EXPIRES_AT_KEY = 'trip-advisor-expires-at';

/** Refresh the token this many milliseconds before it expires. */
const REFRESH_BEFORE_EXPIRY_MS = 2 * 60 * 1000; // 2 minutes

@Injectable({ providedIn: 'root' })
export class AuthService implements OnDestroy {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly zone = inject(NgZone);

  readonly user = signal<AuthUser | null>(this.loadStoredUser());
  readonly token = signal<string | null>(localStorage.getItem(TOKEN_KEY));
  readonly isAuthenticated = computed(() => this.token() !== null && this.user() !== null);
  readonly signingIn = signal(false);
  readonly signInError = signal<string | null>(null);

  private refreshTimerId: ReturnType<typeof setTimeout> | null = null;
  private refreshPromise: Promise<void> | null = null;

  constructor() {
    this.scheduleRefreshFromStorage();
  }

  ngOnDestroy(): void {
    this.clearRefreshTimer();
  }

  private loadStoredUser(): AuthUser | null {
    try {
      const raw = localStorage.getItem(USER_KEY);
      return raw ? (JSON.parse(raw) as AuthUser) : null;
    } catch {
      return null;
    }
  }

  initializeGoogleSignIn(buttonElementId: string): void {
    if (typeof google === 'undefined') return;

    google.accounts.id.initialize({
      client_id: environment.googleClientId,
      auto_select: false,
      callback: (response: { credential: string }) => {
        this.zone.run(() => this.handleGoogleCredential(response.credential));
      },
    });

    const buttonEl = document.getElementById(buttonElementId);
    if (buttonEl) {
      google.accounts.id.renderButton(buttonEl, {
        type: 'standard',
        shape: 'rectangular',
        theme: 'outline',
        text: 'continue_with',
        size: 'large',
        width: 280,
      });
    }
  }

  private async handleGoogleCredential(idToken: string): Promise<void> {
    this.signingIn.set(true);
    this.signInError.set(null);

    try {
      const response = await lastValueFrom(
        this.http.post<AuthResponse>(`${environment.apiUrl}/auth/google`, { idToken })
      );
      this.storeSession(response);
      this.router.navigate(['/']);
    } catch {
      this.signInError.set('Sign in failed. Please try again.');
    } finally {
      this.signingIn.set(false);
    }
  }

  async refreshToken(): Promise<void> {
    // Deduplicate concurrent refresh calls
    if (this.refreshPromise) {
      return this.refreshPromise;
    }

    this.refreshPromise = lastValueFrom(
      this.http.post<AuthResponse>(`${environment.apiUrl}/auth/refresh`, {})
    )
      .then((response) => {
        this.storeSession(response);
      })
      .catch(() => {
        // Refresh failed — sign the user out so they re-authenticate cleanly
        this.signOut();
      })
      .finally(() => {
        this.refreshPromise = null;
      });

    return this.refreshPromise;
  }

  private storeSession(response: AuthResponse): void {
    this.token.set(response.accessToken);
    this.user.set(response.user);
    localStorage.setItem(TOKEN_KEY, response.accessToken);
    localStorage.setItem(USER_KEY, JSON.stringify(response.user));
    localStorage.setItem(EXPIRES_AT_KEY, response.expiresAt);
    this.scheduleRefresh(new Date(response.expiresAt));
  }

  private scheduleRefreshFromStorage(): void {
    const raw = localStorage.getItem(EXPIRES_AT_KEY);
    if (!raw) return;
    this.scheduleRefresh(new Date(raw));
  }

  private scheduleRefresh(expiresAt: Date): void {
    this.clearRefreshTimer();

    const msUntilRefresh = expiresAt.getTime() - Date.now() - REFRESH_BEFORE_EXPIRY_MS;

    if (msUntilRefresh <= 0) {
      // Already at or past the refresh window — refresh immediately
      this.zone.run(() => this.refreshToken());
      return;
    }

    this.refreshTimerId = setTimeout(() => {
      this.zone.run(() => this.refreshToken());
    }, msUntilRefresh);
  }

  private clearRefreshTimer(): void {
    if (this.refreshTimerId !== null) {
      clearTimeout(this.refreshTimerId);
      this.refreshTimerId = null;
    }
  }

  signOut(): void {
    this.clearRefreshTimer();
    this.token.set(null);
    this.user.set(null);
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
    localStorage.removeItem(EXPIRES_AT_KEY);
    if (typeof google !== 'undefined') {
      google.accounts.id.disableAutoSelect();
    }
    this.router.navigate(['/login']);
  }

  getToken(): string | null {
    return this.token();
  }
}
