import { api, OfflineError } from '../api.js';
import { requireAdmin } from '../auth.js';
import { $, banner, h, render } from '../dom.js';

export default function sites(root) {
  if (!requireAdmin()) return;

  const error = $('[data-error]', root);
  const status = $('[data-status]', root);
  const list = $('[data-list]', root);

  async function load() {
    banner(error, null);

    try {
      const rows = await api.get('/api/site', 'Could not load your sites.');

      status.hidden = true;
      render(list, rows.length ? table(rows) : empty());
    } catch (failure) {
      status.hidden = true;
      banner(error, failure.message);
    }
  }

  const empty = () =>
    h('div', { class: 'mt-6 rounded-lg border border-dashed border-slate-300 bg-white p-8 text-center' },
      h('p', { class: 'text-slate-600' }, 'No sites yet.'),
      h('p', { class: 'mt-1 text-sm text-slate-500' },
        'A site is one blog. Registering it also allows its origin through CORS.'));

  const table = (rows) =>
    h('div', { class: 'mt-6 overflow-x-auto rounded-lg border border-slate-200 bg-white' },
      h('table', { class: 'w-full text-left text-sm' },
        h('thead', { class: 'border-b border-slate-200 text-slate-500' },
          h('tr', {},
            h('th', { class: 'px-4 py-3 font-medium' }, 'Name'),
            h('th', { class: 'px-4 py-3 font-medium' }, 'Slug'),
            h('th', { class: 'px-4 py-3 font-medium' }, 'Origins'),
            h('th', { class: 'px-4 py-3' }, h('span', { class: 'sr-only' }, 'Actions')))),
        h('tbody', {}, rows.map(row))));

  const row = (site) =>
    h('tr', { class: 'border-b border-slate-100 last:border-0' },
      h('td', { class: 'px-4 py-3 text-slate-900' }, site.name),
      h('td', { class: 'px-4 py-3' },
        h('code', { class: 'rounded bg-slate-100 px-1.5 py-0.5 font-mono text-slate-700' }, site.slug)),
      h('td', { class: 'px-4 py-3 text-slate-600' }, site.origins.map((o) => h('div', {}, o))),
      h('td', { class: 'px-4 py-3 text-right whitespace-nowrap' },
        h('a', { href: `/sites/${site.siteId}/comments`, class: 'text-sky-700 no-underline hover:underline' }, 'Comments'),
        h('a', { href: `/sites/${site.siteId}`, class: 'ml-3 text-sky-700 no-underline hover:underline' }, 'Edit'),
        h('button', {
          type: 'button',
          class: 'ml-3 text-red-600 hover:underline',
          onclick: () => remove(site.siteId),
        }, 'Delete')));

  async function remove(id) {
    // ponytail: native confirm() is the guard, as it was in the Razor version.
    // Deleting a site cascades every comment on it, so promote this to a
    // confirmation page if it ever needs to survive JS being off.
    if (!window.confirm('Delete this site? Every comment on it is deleted too.')) return;

    banner(error, null);

    try {
      const response = await api.delete(`/api/site/${id}`);

      // Already gone, or never yours — either way the list below is the answer.
      if (!response.ok && response.status !== 404) banner(error, 'Could not delete that site.');
    } catch (failure) {
      banner(error, failure instanceof OfflineError ? failure.message : 'Could not delete that site.');
    }

    await load();
  }

  load();
}
