import { request } from './http';
import type { LoginResponse } from './types';

export const login = (email: string, password: string) =>
  request<LoginResponse>('/auth/login', { method: 'POST', body: { email, password } });
