// Identity, browser-side. The Dashboard server no longer knows who is signed in
// — it renders the same HTML for everyone and this decides what happens next.
//
// The token lives in localStorage: the alternative is memory, which loses the
// session on every refresh and every new tab. That makes XSS in this app able to
// read the token, so pages must never build markup out of API data by string
// concatenation — see dom.js, which is why it exists.
const KEY = 'komment.auth';

let cached;

export function auth() {
  if (cached !== undefined) return cached;

  try {
    const stored = JSON.parse(localStorage.getItem(KEY) ?? 'null');

    // Expiry is checked here as well as by the API, purely to skip a round trip
    // that is going to come back 401 anyway.
    cached = stored && Date.parse(stored.expiresAt) > Date.now() ? stored : null;
  } catch {
    // Private mode, blocked site data, or something else wrote nonsense here.
    cached = null;
  }

  return cached;
}

export function setAuth(value) {
  cached = value;

  try {
    localStorage.setItem(KEY, JSON.stringify(value));
  } catch {
    // Storage refused. The token still works for this page's lifetime; the user
    // will be asked to sign in again on the next navigation.
  }
}

export function clearAuth() {
  cached = null;

  try {
    localStorage.removeItem(KEY);
  } catch {
    // Nothing to do — if it cannot be removed it was probably never written.
  }
}

export const token = () => auth()?.accessToken ?? null;

// Every page behind the sign-in calls this first. Returns the signed-in user, or
// redirects and returns null — in which case the caller must render nothing.
export function requireAdmin() {
  const current = auth();

  if (current?.isSiteAdmin) return current;

  clearAuth();

  const returnUrl = window.location.pathname + window.location.search;
  const query = returnUrl === '/' ? '' : `?returnUrl=${encodeURIComponent(returnUrl)}`;

  window.location.replace(`/login${query}`);

  return null;
}
