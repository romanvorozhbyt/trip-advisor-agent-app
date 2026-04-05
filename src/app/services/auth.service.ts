import { Injectable, signal, computed, inject, NgZone } from '@angular/core';
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
};

const TOKEN_KEY = 'trip-advisor-token';
const USER_KEY = 'trip-advisor-user';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly zone = inject(NgZone);

  readonly user = signal<AuthUser | null>(this.loadStoredUser());
  readonly token = signal<string | null>(localStorage.getItem(TOKEN_KEY));
  readonly isAuthenticated = computed(() => this.token() !== null && this.user() !== null);
  readonly signingIn = signal(false);
  readonly signInError = signal<string | null>(null);

  private loadStoredUser(): AuthUser | null {
    try {
      const raw = localStorage.getItem(USER_KEY);
      return raw ? (JSON.parse(raw) as AuthUser) : null;
    } catch {
      return null;
    }
  }

  initializeGoogleSignIn(buttonElementId: string): void {
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
      this.token.set(response.accessToken);
      this.user.set(response.user);
      localStorage.setItem(TOKEN_KEY, response.accessToken);
      localStorage.setItem(USER_KEY, JSON.stringify(response.user));
      this.router.navigate(['/']);
    } catch {
      this.signInError.set('Sign in failed. Please try again.');
    } finally {
      this.signingIn.set(false);
    }
  }

  signOut(): void {
    this.token.set(null);
    this.user.set(null);
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
    google.accounts.id.disableAutoSelect();
    this.router.navigate(['/login']);
  }

  getToken(): string | null {
    return this.token();
  }
}
