import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, tap } from 'rxjs';
import { Router } from '@angular/router';
import { environment } from '../../../environments/environment';
import { AuthResponse, LoginRequest, UserInfo } from '../models/auth.models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly TOKEN_KEY = 'lt_token';
  private readonly USER_KEY  = 'lt_user';

  /** Standard .NET role-claim URI emitted by ASP.NET Identity / System.Security.Claims. */
  private readonly DOTNET_ROLE_CLAIM =
    'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';

  private currentUserSubject = new BehaviorSubject<UserInfo | null>(this.getStoredUser());
  currentUser$ = this.currentUserSubject.asObservable();

  constructor(private http: HttpClient, private router: Router) {}

  login(req: LoginRequest): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${environment.apiUrl}/auth/login`, req)
      .pipe(
        tap(res => {
          localStorage.setItem(this.TOKEN_KEY, res.token);
          localStorage.setItem(this.USER_KEY, JSON.stringify(res.user));
          this.currentUserSubject.next(res.user);
        })
      );
  }

  /** Called after patient self-registration to store the returned token + user. */
  handleAuthResponse(res: AuthResponse): void {
    localStorage.setItem(this.TOKEN_KEY, res.token);
    localStorage.setItem(this.USER_KEY, JSON.stringify(res.user));
    this.currentUserSubject.next(res.user);
  }

  logout(): void {
    localStorage.removeItem(this.TOKEN_KEY);
    localStorage.removeItem(this.USER_KEY);
    this.currentUserSubject.next(null);
    this.router.navigate(['/auth/login']);
  }

  getToken(): string | null {
    return localStorage.getItem(this.TOKEN_KEY);
  }

  /** True only if a token exists AND it has not expired client-side. */
  isLoggedIn(): boolean {
    const token = this.getToken();
    if (!token) return false;
    const exp = this.getTokenExpiry();
    return exp ? exp > Date.now() : true;
  }

  get currentUser(): UserInfo | null {
    return this.currentUserSubject.value;
  }

  // ── JWT-claim accessors (single source of truth for role/userId) ────────
  //
  // These read directly from the JWT payload, NOT from the cached user object.
  // The user object in localStorage is tamperable — a malicious user could
  // edit "role":"Patient" to "role":"Admin" in DevTools to bypass the
  // client-side RoleGuard. The JWT signature is verified by the backend on
  // every request, so its claims are authoritative.

  /** Decoded payload of the current JWT, or null if no/invalid token. */
  private decodeToken(): Record<string, any> | null {
    const token = this.getToken();
    if (!token) return null;
    try {
      const parts = token.split('.');
      if (parts.length !== 3) return null;
      const payload = parts[1]
        .replace(/-/g, '+')
        .replace(/_/g, '/');
      const padded = payload + '='.repeat((4 - payload.length % 4) % 4);
      const json = atob(padded);
      return JSON.parse(json);
    } catch {
      return null;
    }
  }

  /** The role claim from the JWT (authoritative for guard checks). */
  getRoleFromToken(): string | null {
    const claims = this.decodeToken();
    if (!claims) return null;
    return claims['role']
        ?? claims[this.DOTNET_ROLE_CLAIM]
        ?? null;
  }

  /** The user-id (sub/nameid) claim from the JWT. */
  getUserIdFromToken(): number | null {
    const claims = this.decodeToken();
    if (!claims) return null;
    const raw = claims['sub']
             ?? claims['nameid']
             ?? claims['nameId']
             ?? claims['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'];
    const n = Number(raw);
    return Number.isFinite(n) ? n : null;
  }

  /** Token expiry as ms since epoch, or null if no exp claim. */
  getTokenExpiry(): number | null {
    const claims = this.decodeToken();
    if (!claims?.['exp']) return null;
    return Number(claims['exp']) * 1000;     // JWT exp is seconds since epoch
  }

  private getStoredUser(): UserInfo | null {
    try {
      const raw = localStorage.getItem(this.USER_KEY);
      return raw ? JSON.parse(raw) : null;
    } catch {
      return null;
    }
  }
}
