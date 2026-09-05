import { api, firstError, OfflineError } from '../api.js';
import { requireAdmin } from '../auth.js';
import { $, banner, formData } from '../dom.js';

export default function siteEditor(root) {
  if (!requireAdmin()) return;

  const id = root.dataset.siteId || null;
  const isNew = !id;

  const error = $('[data-error]', root);
  const notFound = $('[data-not-found]', root);
  const status = $('[data-status]', root);
  const form = $('[data-site-form]', root);
  const submit = $('button[type=submit]', form);

  const slugInput = $('[name=slug]', form);
  const slugDisplay = $('[data-slug-display]', root);

  async function load() {
    if (isNew) {
      status.hidden = true;
      form.hidden = false;
      return;
    }

    try {
      const site = await api.get(`/api/site/${id}`, 'Could not load that site.');

      $('[name=name]', form).value = site.name;
      // The API refuses slug edits — blogs already embed the slug in their
      // requests — so on an existing site it is shown, not offered.
      slugDisplay.textContent = site.slug;
      $('[name=origins]', form).value = site.origins.join('\n');

      status.hidden = true;
      form.hidden = false;
    } catch (failure) {
      status.hidden = true;

      if (failure instanceof OfflineError) banner(error, failure.message);
      else notFound.hidden = false;
    }
  }

  form.addEventListener('submit', async (event) => {
    event.preventDefault();

    banner(error, null);
    submit.disabled = true;

    const input = formData(form);

    // The textarea is one origin per line; the API takes them comma-separated.
    const origins = (input.origins ?? '')
      .split(/[\n\r,]+/)
      .map((o) => o.trim())
      .filter(Boolean)
      .join(',');

    try {
      const response = isNew
        ? await api.post('/api/site', { slug: input.slug, name: input.name, origins })
        : await api.patch(`/api/site/${id}`, { name: input.name, origins });

      if (response.ok) {
        window.location.assign('/sites');
        return;
      }

      if (response.status === 404) {
        form.hidden = true;
        notFound.hidden = false;
        return;
      }

      banner(error, await firstError(response, 'Could not save the site.'));
    } catch (failure) {
      banner(error, failure instanceof OfflineError ? failure.message : 'Could not save the site.');
    } finally {
      submit.disabled = false;
    }
  });

  if (isNew) slugInput.required = true;

  load();
}
