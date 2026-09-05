import { api, OfflineError } from '../api.js';
import { setAuth } from '../auth.js';
import { $, banner, formData } from '../dom.js';

export default function login(root) {
  const form = $('[data-login-form]', root);
  const error = $('[data-error]', root);
  const submit = $('button[type=submit]', form);

  form.addEventListener('submit', async (event) => {
    event.preventDefault();

    banner(error, null);
    submit.disabled = true;

    try {
      const response = await api.post('/api/auth/token', formData(form));

      if (!response.ok) {
        // The API is deliberately vague here to avoid an account-enumeration
        // oracle; saying more on this side would give the same thing away.
        banner(error, 'Invalid username or password.');
        return;
      }

      const account = await response.json();

      // The API signs in readers and admins through the same endpoint. This is
      // the admin console, so a reader account is not enough — and keeping a
      // token we will refuse to use just means a confusing 403 later.
      if (!account.isSiteAdmin) {
        banner(error, 'This account cannot manage sites.');
        return;
      }

      setAuth(account);

      // Only ever bounce back inside this app — returnUrl comes from the query
      // string, so an absolute URL here would be an open redirect.
      const wanted = new URLSearchParams(window.location.search).get('returnUrl');
      window.location.assign(wanted?.startsWith('/') && !wanted.startsWith('//') ? wanted : '/');
    } catch (failure) {
      banner(error, failure instanceof OfflineError ? failure.message : 'Sign-in failed. Please try again.');
    } finally {
      submit.disabled = false;
    }
  });
}
