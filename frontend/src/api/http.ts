import { ApiError, NETWORK_ERROR_TITLE, toApiError } from './problem';
import { expireSession, getAccessToken } from '../features/auth/session';

// Relative base: the Vite dev proxy and nginx (Docker) both forward /api to the backend.
const BASE_URL = '/api';
export const LOGIN_PATH = '/auth/login';

type Query = Record<string, string | number | undefined>;

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE';
  body?: unknown;
  query?: Query;
  signal?: AbortSignal;
}

function buildUrl(path: string, query?: Query): string {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(query ?? {})) {
    if (value !== undefined && value !== '') params.set(key, String(value));
  }
  const qs = params.toString();
  return `${BASE_URL}${path}${qs ? `?${qs}` : ''}`;
}

async function readBody(response: Response): Promise<unknown> {
  const text = await response.text();
  if (!text) return undefined;
  try {
    return JSON.parse(text);
  } catch {
    return undefined;
  }
}

export async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const token = getAccessToken();
  const headers: Record<string, string> = { Accept: 'application/json, application/problem+json' };
  if (options.body !== undefined) headers['Content-Type'] = 'application/json';
  if (token) headers.Authorization = `Bearer ${token}`;

  let response: Response;
  try {
    response = await fetch(buildUrl(path, options.query), {
      method: options.method ?? 'GET',
      headers,
      body: options.body === undefined ? undefined : JSON.stringify(options.body),
      signal: options.signal,
    });
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') throw error;
    throw new ApiError({ status: 0, title: NETWORK_ERROR_TITLE, detail: 'Check your connection and try again.' });
  }

  const body = await readBody(response);
  if (response.ok) return body as T;

  // Any 401 except login ends the session: the token was rejected, or it already expired client-side and wasn't sent.
  // A 401 on login is just bad credentials.
  if (response.status === 401 && path !== LOGIN_PATH) expireSession();
  throw toApiError(response.status, body, response.headers.get('Retry-After'));
}
