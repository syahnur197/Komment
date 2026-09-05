import { api, OfflineError } from '../api.js';
import { requireAdmin } from '../auth.js';
import { $, banner, formatDate, h, render } from '../dom.js';

export default function comments(root) {
  const account = requireAdmin();
  if (!account) return;

  const siteId = root.dataset.siteId;

  const error = $('[data-error]', root);
  const notFound = $('[data-not-found]', root);
  const status = $('[data-status]', root);
  const heading = $('[data-site-name]', root);
  const list = $('[data-list]', root);

  let slug = '';

  async function load() {
    banner(error, null);

    let site;

    try {
      // Owner-scoped at the API, so this fetch is both the authorisation check
      // and where the slug comes from — comment endpoints key off site=<slug>,
      // and a comment response carries no site of its own.
      site = await api.get(`/api/site/${siteId}`, 'Could not load that site.');
    } catch (failure) {
      status.hidden = true;

      if (failure instanceof OfflineError) banner(error, failure.message);
      else notFound.hidden = false;

      return;
    }

    slug = site.slug;
    heading.textContent = site.name;
    document.title = `Comments — ${site.name}`;

    try {
      const rows = await api.get(`/api/comment?site=${encodeURIComponent(slug)}`, 'Could not load comments.');

      status.hidden = true;
      render(list, rows.length ? byPost(rows).map(section) : empty());
    } catch (failure) {
      status.hidden = true;
      banner(error, failure.message);
      render(list);
    }
  }

  const empty = () =>
    h('div', { class: 'mt-6 rounded-lg border border-dashed border-slate-300 bg-white p-8 text-center' },
      h('p', { class: 'text-slate-600' }, 'No comments yet.'),
      h('p', { class: 'mt-1 text-sm text-slate-500' },
        'Comments arrive from the blog at ', h('code', { class: 'font-mono' }, slug), '.'));

  // Grouped by post because that is how a moderator reads them. The API returns
  // one flat list ordered by time, so this costs nothing extra.
  function byPost(rows) {
    const groups = new Map();

    for (const comment of rows) {
      if (!groups.has(comment.postSlug)) groups.set(comment.postSlug, []);
      groups.get(comment.postSlug).push(comment);
    }

    return [...groups.entries()].sort(([a], [b]) => a.localeCompare(b));
  }

  const section = ([postSlug, rows]) =>
    h('section', { class: 'mt-6' },
      h('h2', { class: 'font-mono text-sm text-slate-500' }, postSlug),
      h('ul', { class: 'mt-2 space-y-px overflow-hidden rounded-lg border border-slate-200 bg-white' },
        rows.map(item)));

  const item = (comment) =>
    h('li', {
      class: 'border-b border-slate-100 p-4 last:border-0'
        + (comment.parentCommentId ? ' border-l-2 border-l-sky-200 pl-8' : ''),
    },
      h('div', { class: 'flex items-baseline gap-2 text-sm' },
        h('span', { class: 'font-medium text-slate-900' }, comment.authorName),
        comment.parentCommentId ? h('span', { class: 'text-slate-400' }, 'replied') : null,
        h('time', { datetime: comment.createdAt, class: 'text-slate-400' }, formatDate(comment.createdAt)),
        comment.updatedAt > comment.createdAt ? h('span', { class: 'text-slate-400' }, '· edited') : null),

      h('p', { class: 'mt-1 whitespace-pre-wrap text-slate-700' }, comment.body),

      h('div', { class: 'mt-2 flex items-center gap-3 text-sm' },
        h('a', {
          href: `/sites/${siteId}/comments/${comment.commentId}?reply=true`,
          class: 'text-sky-700 no-underline hover:underline',
        }, 'Reply'),

        // The API refuses edits from anyone but the author, admin or not, so the
        // link only appears where it would actually work.
        comment.userId === account.userId
          ? h('a', {
              href: `/sites/${siteId}/comments/${comment.commentId}`,
              class: 'text-sky-700 no-underline hover:underline',
            }, 'Edit')
          : null,

        h('button', {
          type: 'button',
          class: 'text-red-600 hover:underline',
          onclick: () => remove(comment.commentId),
        }, 'Delete')));

  async function remove(commentId) {
    if (!window.confirm('Delete this comment? Its replies are deleted too.')) return;

    banner(error, null);

    try {
      const response = await api.delete(`/api/comment/${commentId}`);

      // Already gone is the outcome we wanted anyway.
      if (!response.ok && response.status !== 404) banner(error, 'Could not delete that comment.');
    } catch (failure) {
      banner(error, failure instanceof OfflineError ? failure.message : 'Could not delete that comment.');
    }

    await load();
  }

  load();
}
