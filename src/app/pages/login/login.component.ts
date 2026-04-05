import {
  Component,
  ChangeDetectionStrategy,
  inject,
  AfterViewInit,
} from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
})
export class LoginComponent implements AfterViewInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly signingIn = this.auth.signingIn;
  readonly signInError = this.auth.signInError;

  ngAfterViewInit(): void {
    if (this.auth.isAuthenticated()) {
      this.router.navigate(['/']);
      return;
    }

    const init = () => this.auth.initializeGoogleSignIn('google-btn');

    if ('google' in window) {
      init();
    } else {
      const script = document.querySelector('script[src*="accounts.google.com"]');
      script?.addEventListener('load', init);
    }
  }
}
