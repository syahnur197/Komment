// The single door to the API. Everything the Dashboard knows about talking to
// the backend lives here: where it is, how to prove who you are, and how to read
// a failure. Pages call these and nothing else.
import { clearAuth, token } from './auth.js';

// Rendered into the page by App.razor. The browser cannot resolve Aspire's
// "https+http://backend" — that name only means something inside the server
// process — so the server writes the public URL into a meta tag for us.
const base = (document.querySelector('meta[name="komment-api"]')?.content ?? '').replace(/\/+$/, '');

export class ApiError extends Error {
  constructor(status, message) {
    super(message);
    this.status = status;
  }
}

// Thrown when the API cannot be reached at all — a different problem from the
// API answering with a failure, and a different message for the user.
export class OfflineError extends Error {}

async function request(method, path, body) {
  const headers = {};
  const auth = token();

  if (auth) headers.Authorization = `Bearer ${auth}`;
  if (body !== undefined) headers['Content-Type'] = 'application/json';

  let response;

  try {
    response = await fetch(base + path, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
    });
  } catch {
    // fetch rejects for network failure, DNS, and a CORS rejection alike — the
    // browser deliberately does not say which.
    throw new OfflineError('Cannot reach the Komment API. Please try again in a moment.');
  }

  // The token is gone or expired. Nothing on the current page can recover, and
  // staying here would just fail every subsequent call the same way.
  if (response.status === 401 && auth) {
    clearAuth();
    window.location.assign('/login');
    throw new ApiError(401, 'Signed out.');
  }

  return response;
}

// FastEndpoints' Send.ErrorsAsync shape is { errors: { field: [message] } };
// Results.Problem gives ProblemDetails with a detail or title. Try both, and let
// the caller supply the fallback since only it knows what was being attempted.
export async function firstError(response, fallback) {
  try {
    const body = await response.json();
    const fields = body?.errors && Object.values(body.errors).flat();

    return fields?.[0] ?? body?.detail ?? body?.title ?? fallback;
  } catch {
    return fallback;
  }
}

async function json(response) {
  return response.status === 204 ? null : response.json();
}

export const api = {
  // Reads: the caller nearly always wants the body or a thrown failure, so these
  // fold the status check in. Writes return the response — status codes carry
  // meaning there (404 on delete is fine, 403 on register means "closed").
  async get(path, fallback) {
    const response = await request('GET', path);

    if (!response.ok) throw new ApiError(response.status, await firstError(response, fallback));

    return json(response);
  },

  post: (path, body) => request('POST', path, body),
  patch: (path, body) => request('PATCH', path, body),
  delete: (path) => request('DELETE', path),
};
