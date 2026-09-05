import { auth, clearAuth } from './auth.js';
import { $ } from './dom.js';

// The signed-in shell. The server renders it for everyone because it no longer
// knows who is asking, so the parts that depend on identity start hidden and are
// revealed here. Elements are absent on the unauthenticated layout, hence the
// null checks rather than an assumption about which layout rendered.
export function chrome() {
  const account = auth();

  const name = $('[data-user-name]');
  const signOut = $('[data-sign-out]');
  const signIn = $('[data-sign-in]');
  const adminNav = $('[data-admin-nav]');

  if (account) {
    if (name) {
      name.textContent = account.name;
      name.hidden = false;
    }

    if (signOut) signOut.hidden = false;
    if (adminNav) adminNav.hidden = false;
  } else if (signIn) {
    signIn.hidden = false;
  }

  signOut?.addEventListener('click', () => {
    // ponytail: dropping the token is enough — it is the only credential, and
    // the API has no revocation list to tell. Call /api/auth/logout too if
    // server-side revocation ever exists to call.
    clearAuth();
    window.location.assign('/login');
  });
}
