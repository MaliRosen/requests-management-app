import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../environments/environment';

interface LoginResponse {
  token: string;
}

export interface CurrentUser {
  userId: number;
  isAdmin: boolean;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly tokenKey = 'jwt_token';
  private readonly apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  login(userId: number, password: string): Observable<LoginResponse> {
    return this.http
      .post<LoginResponse>(`${this.apiUrl}/api/auth/login`, { userId, password })
      .pipe(tap(res => localStorage.setItem(this.tokenKey, res.token)));
  }

  logout(): void {
    localStorage.removeItem(this.tokenKey);
  }

  getToken(): string | null {
    return localStorage.getItem(this.tokenKey);
  }

  isLoggedIn(): boolean {
    return !!this.getToken();
  }

  getCurrentUser(): CurrentUser | null {
    const token = this.getToken();
    if (!token) return null;

    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      return {
        userId: Number(payload['userId']),
        isAdmin: payload['isAdmin'] === 'true' || payload['isAdmin'] === true,
      };
    } catch {
      // token פגום — מנקים אותו כדי לא להישאר במצב "מחובר" עם token לא תקין
      this.logout();
      return null;
    }
  }
}
