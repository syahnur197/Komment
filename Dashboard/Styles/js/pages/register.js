import { api, firstError, OfflineError } from '../api.js';
import { $, banner, formData } from '../dom.js';

export default function register(root) {
  const form = $('[data-register-form]', root);
  const error = $('[data-error]', root);
  const closed = $('[data-closed]', root);
  const submit = $('button[type=submit]', form);

  form.addEventListener('submit', async (event) => {
    event.preventDefault();

    banner(error, null);
    submit.disabled = true;

    try {
      const response = await api.post('/api/auth/register', formData(form));

      if (response.ok) {
        // Registering does not sign you in — the API creates the account and
        // stops there.
        window.location.assign('/login');
        return;
      }

      // MULTI_TENANCY=false and an admin already exists. The API owns that rule;
      // the console only reports it, and nothing on this form can change it.
      if (response.status === 403) {
        form.hidden = true;
        closed.hidden = false;
        return;
      }

      banner(error, await firstError(response, 'Could not create the account. Please try again.'));
    } catch (failure) {
      banner(error, failure instanceof OfflineError ? failure.message : 'Could not create the account.');
    } finally {
      submit.disabled = false;
    }
  });
}
