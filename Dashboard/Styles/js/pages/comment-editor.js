import { api, firstError, OfflineError } from '../api.js';
import { requireAdmin } from '../auth.js';
import { $, banner, formData } from '../dom.js';

export default function commentEditor(root) {
  const account = requireAdmin();
  if (!account) return;

  const { siteId, commentId } = root.dataset;

  // One route, two jobs: edit this comment, or reply to it. A query flag keeps
  // it to a single page rather than two near-identical ones.
  const reply = new URLSearchParams(window.location.search).get('reply') === 'true';

  const error = $('[data-error]', root);
  const blocked = $('[data-blocked]', root);
  const status = $('[data-status]', root);
  const form = $('[data-comment-form]', root);
  const body = $('[name=body]', form);
  const submit = $('button[type=submit]', form);
  const quote = $('[data-target]', root);

  let target = null;
  let slug = null;

  function block(message) {
    status.hidden = true;
    form.hidden = true;
    banner(blocked, message);
  }

  async function load() {
    try {
      // Owner-scoped, so it doubles as the check that this site is yours.
      slug = (await api.get(`/api/site/${siteId}`, 'Could not load that site.')).slug;
    } catch (failure) {
      block(failure instanceof OfflineError ? failure.message : 'That site does not exist.');
      return;
    }

    try {
      target = await api.get(`/api/comment/${commentId}`, 'Could not load that comment.');
    } catch (failure) {
      block(failure instanceof OfflineError ? failure.message : 'That comment does not exist.');
      return;
    }

    // Editing is author-only at the API. Saying so here beats a 403 after typing.
    if (!reply && target.userId !== account.userId) {
      block('You can only edit your own comments. You can still delete this one from the comments list.');
      return;
    }

    $('[data-target-slug]', quote).textContent = target.postSlug;
    $('[data-target-author]', quote).textContent = target.authorName;
    $('[data-target-body]', quote).textContent = target.body;
    quote.hidden = false;

    if (!reply) body.value = target.body;

    status.hidden = true;
    form.hidden = false;
  }

  form.addEventListener('submit', async (event) => {
    event.preventDefault();

    banner(error, null);
    submit.disabled = true;

    try {
      const input = formData(form);

      const response = reply
        ? await api.post('/api/comment', {
            site: slug,
            postSlug: target.postSlug,
            body: input.body,
            parentCommentId: commentId,
          })
        : await api.patch(`/api/comment/${commentId}`, { body: input.body });

      if (response.ok) {
        window.location.assign(`/sites/${siteId}/comments`);
        return;
      }

      if (response.status === 403) {
        block('You can only edit your own comments.');
        return;
      }

      banner(error, await firstError(response, 'Could not save the comment.'));
    } catch (failure) {
      banner(error, failure instanceof OfflineError ? failure.message : 'Could not save the comment.');
    } finally {
      submit.disabled = false;
    }
  });

  if (reply) {
    document.title = 'Reply';
    $('[data-title]', root).textContent = 'Reply';
    $('[data-body-label]', root).textContent = 'Your reply';
    submit.textContent = 'Post reply';
  }

  load();
}
