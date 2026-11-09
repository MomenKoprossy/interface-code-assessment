import { LOGIN_PATH, request } from './http';
import type { LoginResponse } from './types';

export const login = (email: string, password: string) =>
  request<LoginResponse>(LOGIN_PATH, { method: 'POST', body: { email, password } });
