import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { saveSession, SESSION_EXPIRED_EVENT } from '../features/auth/session';
import { request } from './http';
import { ApiError, errorMessage, isStatus } from './problem';

function jsonResponse(status: number, body: unknown, contentType = 'application/json'): Response {
  return new Response(body === undefined ? null : JSON.stringify(body), { status, headers: { 'Content-Type': contentType } });
}

async function capture(promise: Promise<unknown>): Promise<ApiError> {
  try {
    await promise;
  } catch (error) {
    if (error instanceof ApiError) return error;
    throw error;
  }
  throw new Error('Expected the request to fail.');
}

const fetchMock = vi.fn<typeof fetch>();

beforeEach(() => {
  vi.stubGlobal('fetch', fetchMock);
});

afterEach(() => {
  vi.unstubAllGlobals();
  fetchMock.mockReset();
});

function signIn() {
  saveSession({
    accessToken: 'token-123',
    expiresAtUtc: '2999-01-01T00:00:00Z',
    user: { id: 'u1', email: 'hr@example.com', displayName: 'HR', role: 'HR', employeeId: null, permissions: [] },
  });
}

describe('request', () => {
  it('calls the relative /api base with the query string and parses JSON', async () => {
    fetchMock.mockResolvedValue(jsonResponse(200, { ok: true }));
    const result = await request('/employees', { query: { page: 2, search: '', department: undefined } });
    expect(result).toEqual({ ok: true });
    expect(fetchMock).toHaveBeenCalledWith('/api/employees?page=2', expect.objectContaining({ method: 'GET' }));
  });

  it('sends the bearer token and a JSON body', async () => {
    signIn();
    fetchMock.mockResolvedValue(jsonResponse(201, { id: '1' }));
    await request('/employees', { method: 'POST', body: { fullName: 'Ada' } });
    const init = fetchMock.mock.calls[0]?.[1];
    expect(init?.headers).toMatchObject({ Authorization: 'Bearer token-123', 'Content-Type': 'application/json' });
    expect(init?.body).toBe('{"fullName":"Ada"}');
  });

  it('returns undefined for 204 No Content', async () => {
    fetchMock.mockResolvedValue(new Response(null, { status: 204 }));
    await expect(request('/employees/1', { method: 'DELETE' })).resolves.toBeUndefined();
  });

  it('converts a 400 ProblemDetails into an ApiError with field errors', async () => {
    fetchMock.mockResolvedValue(
      jsonResponse(
        400,
        {
          title: 'Validation failed',
          status: 400,
          detail: 'One or more fields are invalid.',
          traceId: '00-abc',
          errors: { email: ['Email is required.'], 'allowances[0].amount': ['Amount must be greater than 0.'] },
        },
        'application/problem+json',
      ),
    );
    const error = await capture(request('/employees', { method: 'POST', body: {} }));
    expect(error).toMatchObject({
      status: 400,
      title: 'Validation failed',
      detail: 'One or more fields are invalid.',
      traceId: '00-abc',
      fieldErrors: { email: ['Email is required.'], 'allowances[0].amount': ['Amount must be greater than 0.'] },
    });
    expect(isStatus(error, 400)).toBe(true);
  });

  it('keeps the 422 detail for a friendly notice', async () => {
    fetchMock.mockResolvedValue(
      jsonResponse(422, { title: 'Business rule violated', status: 422, detail: 'Employee was not yet hired in 2019-01.' }),
    );
    const error = await capture(request('/employees/1/payslip', { query: { month: '2019-01' } }));
    expect(error.status).toBe(422);
    expect(errorMessage(error)).toBe('Employee was not yet hired in 2019-01.');
    expect(error.fieldErrors).toEqual({});
  });

  it('falls back to a default title when the body is not JSON (e.g. a proxy error page)', async () => {
    fetchMock.mockResolvedValue(new Response('<html>Bad gateway</html>', { status: 502 }));
    const error = await capture(request('/employees'));
    expect(error.status).toBe(502);
    expect(error.title).toBe('Something went wrong. Please try again.');
    expect(error.fieldErrors).toEqual({});
  });

  it('ignores malformed errors entries', async () => {
    fetchMock.mockResolvedValue(jsonResponse(400, { title: 'Bad', errors: { email: 'not-an-array', name: [1, 'Required.'] } }));
    const error = await capture(request('/employees'));
    expect(error.fieldErrors).toEqual({ name: ['Required.'] });
  });

  it('turns a network failure into status 0 "Can\'t reach the server"', async () => {
    fetchMock.mockRejectedValue(new TypeError('Failed to fetch'));
    const error = await capture(request('/employees'));
    expect(error.status).toBe(0);
    expect(error.title).toBe("Can't reach the server");
  });

  it('rethrows aborts untouched so callers can ignore them', async () => {
    fetchMock.mockRejectedValue(new DOMException('aborted', 'AbortError'));
    await expect(request('/employees')).rejects.toHaveProperty('name', 'AbortError');
  });

  it('clears the session and signals expiry on a 401 for an authenticated call', async () => {
    signIn();
    const onExpired = vi.fn();
    window.addEventListener(SESSION_EXPIRED_EVENT, onExpired);
    fetchMock.mockResolvedValue(jsonResponse(401, { title: 'Unauthorized', status: 401 }));

    const error = await capture(request('/employees'));

    window.removeEventListener(SESSION_EXPIRED_EVENT, onExpired);
    expect(error.status).toBe(401);
    expect(onExpired).toHaveBeenCalledOnce();
    expect(sessionStorage.length).toBe(0);
  });

  it('does not treat a failed login (401 without a token) as an expired session', async () => {
    const onExpired = vi.fn();
    window.addEventListener(SESSION_EXPIRED_EVENT, onExpired);
    fetchMock.mockResolvedValue(jsonResponse(401, { title: 'Invalid email or password.', status: 401 }));

    const error = await capture(request('/auth/login', { method: 'POST', body: {} }));

    window.removeEventListener(SESSION_EXPIRED_EVENT, onExpired);
    expect(errorMessage(error)).toBe('Invalid email or password.');
    expect(onExpired).not.toHaveBeenCalled();
  });
});
