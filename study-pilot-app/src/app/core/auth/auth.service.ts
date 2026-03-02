import { Injectable, inject, signal, computed } from '@angular/core';
import { Router } from '@angular/router';
import { BehaviorSubject, Observable, tap, catchError, of, map } from 'rxjs';
import { StudyPilotApiService, AuthResponse } from '../services/study-pilot-api.service';

const TOKEN_KEY = 'study_pilot_access_token';
const REFRESH_KEY = 'study_pilot_refresh_token';
const EXPIRES_KEY = 'study_pilot_expires_at';
const REFRESH_BEFORE_MS = 60_000;

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(StudyPilotApiService);
  private readonly router = inject(Router);
  private refreshTimeout: ReturnType<typeof setTimeout> | null = null;
  private readonly tokenSubject = new BehaviorSubject<string | null>(this.getStoredAccessToken());
  readonly currentUser$ = this.tokenSubject.asObservable();
  readonly isAuthenticated = computed(() => !!this.tokenSubject.value);

  get token(): string | null {
    return this.tokenSubject.value;
  }

  getRefreshToken(): string | null {
    return typeof sessionStorage !== 'undefined' ? sessionStorage.getItem(REFRESH_KEY) : null;
  }

  private getStoredAccessToken(): string | null {
    return typeof localStorage !== 'undefined' ? localStorage.getItem(TOKEN_KEY) : null;
  }

  private setTokens(access: string, refresh: string, expiresAtUtc: string): void {
    if (typeof localStorage !== 'undefined') {
      localStorage.setItem(TOKEN_KEY, access);
      localStorage.setItem(EXPIRES_KEY, expiresAtUtc);
    }
    if (typeof sessionStorage !== 'undefined') sessionStorage.setItem(REFRESH_KEY, refresh);
    this.tokenSubject.next(access);
    this.scheduleRefresh(expiresAtUtc);
  }

  private clearTokens(): void {
    if (typeof localStorage !== 'undefined') {
      localStorage.removeItem(TOKEN_KEY);
      localStorage.removeItem(EXPIRES_KEY);
    }
    if (typeof sessionStorage !== 'undefined') sessionStorage.removeItem(REFRESH_KEY);
    this.tokenSubject.next(null);
    if (this.refreshTimeout) {
      clearTimeout(this.refreshTimeout);
      this.refreshTimeout = null;
    }
  }

  private scheduleRefresh(expiresAtUtc: string): void {
    if (this.refreshTimeout) clearTimeout(this.refreshTimeout);
    const expires = new Date(expiresAtUtc).getTime();
    const now = Date.now();
    const ms = Math.max(0, expires - now - REFRESH_BEFORE_MS);
    this.refreshTimeout = setTimeout(() => this.refresh().subscribe(), ms);
  }

  refresh(): Observable<boolean> {
    const refreshToken = this.getRefreshToken();
    if (!refreshToken) return of(false);
    return this.api.refresh(refreshToken).pipe(
      map(res => {
        this.setTokens(res.accessToken, res.refreshToken, res.expiresAtUtc);
        return true;
      }),
      catchError(() => of(false))
    );
  }

  login(email: string, password: string): Observable<AuthResponse> {
    return this.api.login({ email, password }).pipe(
      tap(res => {
        if (res.accessToken && res.refreshToken) {
          this.setTokens(res.accessToken, res.refreshToken, res.expiresAtUtc);
        }
      })
    );
  }

  register(email: string, password: string): Observable<AuthResponse> {
    return this.api.register({ email, password }).pipe(
      tap(res => {
        if (res.accessToken && res.refreshToken) {
          this.setTokens(res.accessToken, res.refreshToken, res.expiresAtUtc);
        }
      })
    );
  }

  logout(): void {
    const rt = this.getRefreshToken();
    if (rt) this.api.logout(rt).subscribe({ error: () => {} });
    this.clearTokens();
    this.router.navigate(['/auth/login']);
  }
}
