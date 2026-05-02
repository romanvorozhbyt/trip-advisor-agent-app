export interface AuthUser {
  id: string;
  displayName: string;
  email: string;
  pictureUrl: string | null;
}

export interface AuthResponse {
  accessToken: string;
  expiresAt: string;
  user: AuthUser;
}
