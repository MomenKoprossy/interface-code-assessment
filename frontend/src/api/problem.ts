export type FieldErrors = Record<string, string[]>;

export const NETWORK_ERROR_TITLE = "Can't reach the server";

/** Every failed API call surfaces as this, whatever the server (or the network) produced. */
export class ApiError extends Error {
  readonly status: number;
  readonly title: string;
  readonly detail: string | undefined;
  readonly fieldErrors: FieldErrors;
  readonly traceId: string | undefined;

  constructor(init: { status: number; title: string; detail?: string; fieldErrors?: FieldErrors; traceId?: string }) {
    super(init.detail ?? init.title);
    this.name = 'ApiError';
    this.status = init.status;
    this.title = init.title;
    this.detail = init.detail;
    this.fieldErrors = init.fieldErrors ?? {};
    this.traceId = init.traceId;
  }
}

const TOO_MANY_REQUESTS = 'Too many requests. Please wait a moment and try again.';

const defaultTitles: Record<number, string> = {
  400: 'Validation failed',
  401: 'Your session has expired. Please sign in again.',
  403: "You don't have permission to do that.",
  404: 'Not found',
  409: 'Conflict',
  422: 'This request cannot be processed.',
  429: TOO_MANY_REQUESTS,
};

function asString(value: unknown): string | undefined {
  return typeof value === 'string' && value.trim() !== '' ? value : undefined;
}

function asFieldErrors(value: unknown): FieldErrors {
  if (typeof value !== 'object' || value === null) return {};
  const result: FieldErrors = {};
  for (const [key, messages] of Object.entries(value)) {
    const list = Array.isArray(messages) ? messages.filter((m): m is string => typeof m === 'string') : [];
    if (list.length > 0) result[key] = list;
  }
  return result;
}

/** Retry-After is either delta-seconds or an HTTP date (RFC 9110 §10.2.3); anything unusable yields undefined. */
function retryAfterSeconds(header: string | null | undefined): number | undefined {
  const value = header?.trim();
  if (!value) return undefined;
  const seconds = /^\d+$/.test(value) ? Number(value) : Math.ceil((Date.parse(value) - Date.now()) / 1000);
  return Number.isFinite(seconds) && seconds > 0 ? seconds : undefined;
}

/** A 429's title from the server is the bare reason phrase, so ours always wins, with the wait when the server gave one. */
function tooManyRequestsTitle(retryAfter: string | null | undefined): string {
  const seconds = retryAfterSeconds(retryAfter);
  if (seconds === undefined) return TOO_MANY_REQUESTS;
  return `Too many requests. Try again in ${seconds} ${seconds === 1 ? 'second' : 'seconds'}.`;
}

/**
 * Builds an ApiError from a non-2xx response body. The body may be ProblemDetails, other JSON, or nothing at all.
 * The server's title wins over our defaults (a login 401 carries 'Invalid email or password.'), except on 429.
 */
export function toApiError(status: number, body: unknown, retryAfter?: string | null): ApiError {
  const problem = typeof body === 'object' && body !== null ? (body as Record<string, unknown>) : {};
  return new ApiError({
    status,
    title:
      status === 429
        ? tooManyRequestsTitle(retryAfter)
        : (asString(problem.title) ?? defaultTitles[status] ?? 'Something went wrong. Please try again.'),
    detail: asString(problem.detail),
    fieldErrors: asFieldErrors(problem.errors),
    traceId: asString(problem.traceId),
  });
}

export function isApiError(error: unknown): error is ApiError {
  return error instanceof ApiError;
}

export function isStatus(error: unknown, status: number): error is ApiError {
  return isApiError(error) && error.status === status;
}

/** The best single human message for an error: the server's detail, else its first field error, else the title. */
export function errorMessage(error: unknown): string {
  if (isApiError(error)) return error.detail ?? Object.values(error.fieldErrors)[0]?.[0] ?? error.title;
  return 'Something went wrong. Please try again.';
}
