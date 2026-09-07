/* The blog-side widget.
 *
 *   <script src="https://komment.example/embed.js" data-site="my-blog"></script>
 *
 * One attribute is the whole configuration: the API base comes from this
 * script's own src and the post key from the page's path, because for a static
 * blog both are already correct. Renders into <div id="komment"> if the page
 * has one, otherwise into a div inserted where the script tag sits.
 *
 * No build step, no dependencies, no framework — it is dropped onto pages this
 * repo does not control, so it brings nothing with it.
 */
(function () {
    var script = document.currentScript;
    var site = script.dataset.site;
    var slug = script.dataset.slug || location.pathname;
    var api = (script.dataset.api || new URL(script.src, location.href).origin).replace(/\/+$/, '');

    var mount = document.getElementById('komment');
    if (!mount) {
        mount = document.createElement('div');
        script.parentNode.insertBefore(mount, script.nextSibling);
    }
    mount.className = 'km';

    var me = null;          // MeResponse, or null when signed out
    var comments = [];
    var replyTo = null;     // commentId the composer is currently attached to
    var pendingDelete = null;
    var draft = '';

    if (!site) return fail('komment: this script tag needs a data-site attribute.');

    style();
    load();

    // --- dom ---------------------------------------------------------------

    // textContent, never innerHTML — every string below is reader-supplied.
    function el(tag, cls, text) {
        var node = document.createElement(tag);
        if (cls) node.className = cls;
        if (text != null) node.textContent = text;
        return node;
    }

    function fail(message) {
        mount.replaceChildren(el('p', 'km-error', message));
    }

    // Npgsql hands back UTC, but a timestamp that lost its Kind would serialise
    // without the Z and parse as local time — hours out, silently.
    function when(iso) {
        var t = /[Z+]|-\d\d:\d\d$/.test(iso) ? iso : iso + 'Z';
        return new Date(t).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' });
    }

    function avatar(comment) {
        if (comment.authorAvatarUrl) {
            var img = el('img', 'km-avatar');
            img.src = comment.authorAvatarUrl;
            img.alt = '';
            img.loading = 'lazy';
            img.referrerPolicy = 'no-referrer';
            return img;
        }
        return el('span', 'km-avatar km-initial', (comment.authorName || '?').charAt(0).toUpperCase());
    }

    // --- rendering ---------------------------------------------------------

    function render() {
        var head = el('div', 'km-head');
        head.append(el('h2', 'km-count', comments.length === 1 ? '1 comment' : comments.length + ' comments'));

        if (me) {
            var who = el('span', 'km-who', me.name + ' · ');
            var out = el('button', 'km-link', 'Sign out');
            out.type = 'button';
            out.onclick = signOut;
            who.append(out);
            head.append(who);
        }

        mount.replaceChildren(head);
        if (replyTo === null) mount.append(composer(null));

        var list = el('ul', 'km-list');
        // Flat and oldest-first on the wire; the nesting is the client's job.
        var children = {};
        comments.forEach(function (c) {
            (children[c.parentCommentId || ''] ||= []).push(c);
        });
        thread(list, children, '');
        mount.append(list);
    }

    function thread(into, children, parentId) {
        (children[parentId] || []).forEach(function (comment) {
            var item = el('li', 'km-c');
            var meta = el('div', 'km-meta');
            meta.append(avatar(comment), el('span', 'km-name', comment.authorName), el('time', 'km-when', when(comment.createdAt)));

            item.append(meta, el('div', 'km-body', comment.body), actions(comment));
            if (replyTo === comment.commentId) item.append(composer(comment.commentId));

            var kids = el('ul', 'km-list km-kids');
            thread(kids, children, comment.commentId);
            if (kids.childElementCount) item.append(kids);

            into.append(item);
        });
    }

    function actions(comment) {
        var row = el('div', 'km-actions');

        if (me) {
            var reply = el('button', 'km-link', replyTo === comment.commentId ? 'Cancel' : 'Reply');
            reply.type = 'button';
            reply.onclick = function () {
                replyTo = replyTo === comment.commentId ? null : comment.commentId;
                pendingDelete = null;
                draft = '';
                render();
            };
            row.append(reply);
        }

        // Deleting someone else's is the site owner's job and belongs in the
        // console; here you only ever get your own.
        if (me && me.userId === comment.userId) {
            if (pendingDelete === comment.commentId) {
                var yes = el('button', 'km-link km-danger', 'Really delete?');
                yes.type = 'button';
                yes.onclick = function () { remove(comment.commentId); };
                row.append(yes);
            } else {
                var del = el('button', 'km-link', 'Delete');
                del.type = 'button';
                del.onclick = function () { pendingDelete = comment.commentId; render(); };
                row.append(del);
            }
        }

        return row;
    }

    function composer(parentId) {
        if (!me) {
            var prompt = el('p', 'km-signin');
            var link = el('a', 'km-button', 'Sign in with Google to comment');
            link.href = api + '/api/auth/login?returnUrl=' + encodeURIComponent(location.href);
            prompt.append(link);
            return prompt;
        }

        var form = el('form', 'km-form');
        var box = el('textarea', 'km-text');
        box.rows = 3;
        box.required = true;
        box.maxLength = 4000;
        box.placeholder = parentId ? 'Write a reply…' : 'Join the discussion…';
        box.value = draft;
        box.oninput = function () { draft = box.value; };

        var send = el('button', 'km-button', parentId ? 'Reply' : 'Post comment');
        send.type = 'submit';

        form.onsubmit = function (event) {
            event.preventDefault();
            send.disabled = true;
            post(box.value, parentId).finally(function () { send.disabled = false; });
        };

        form.append(box, send);
        return form;
    }

    // --- api ---------------------------------------------------------------

    // The reader cookie is third-party by definition — the blog is on another
    // origin — so every call is credentialed and CORS must allow it.
    function call(path, init) {
        return fetch(api + path, Object.assign({ credentials: 'include' }, init));
    }

    async function load() {
        mount.replaceChildren(el('p', 'km-muted', 'Loading comments…'));

        var [identity, thread] = await Promise.all([
            call('/api/auth/me'),
            call('/api/comment?site=' + encodeURIComponent(site) + '&postSlug=' + encodeURIComponent(slug))
        ]);

        // 401 is the signed-out answer, not a failure: reading is anonymous.
        me = identity.ok ? await identity.json() : null;

        if (!thread.ok) return fail('Could not load comments.');

        comments = await thread.json();
        replyTo = null;
        pendingDelete = null;
        draft = '';
        render();
    }

    async function post(body, parentId) {
        var response = await call('/api/comment', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ site: site, postSlug: slug, postUrl: location.href, body: body, parentCommentId: parentId })
        });

        if (response.ok) return load();

        // FastEndpoints' ErrorResponse: { errors: { field: ["message"] } }. The
        // service's messages are written for a reader, so show the first one.
        var problem = response.status === 400 ? await response.json().catch(function () { return null; }) : null;
        var field = problem && problem.errors && Object.keys(problem.errors)[0];

        mount.prepend(el('p', 'km-error',
            field ? problem.errors[field][0]
                  : response.status === 429 ? 'Too many requests — try again in a minute.'
                  : 'Could not post that comment.'));
    }

    async function remove(commentId) {
        var response = await call('/api/comment/' + commentId, { method: 'DELETE' });

        // Already gone counts as deleted — the reloaded thread is the answer.
        if (response.ok || response.status === 404) return load();

        pendingDelete = null;
        render();
        mount.prepend(el('p', 'km-error', 'Could not delete that comment.'));
    }

    async function signOut() {
        await call('/api/auth/logout', { method: 'POST' });
        load();
    }

    // --- styles ------------------------------------------------------------

    // Inherits the blog's font and colour on purpose: a comment thread should
    // look like part of the page it is on, not like an embedded product.
    function style() {
        var css = `
.km { font: inherit; color: inherit; line-height: 1.5; }
.km * { box-sizing: border-box; }
.km-head { display: flex; align-items: baseline; justify-content: space-between; gap: 1rem;
           border-bottom: 1px solid rgb(128 128 128 / .25); padding-bottom: .5rem; margin-bottom: 1rem; }
.km-count { font-size: 1.125rem; font-weight: 600; margin: 0; }
.km-who, .km-when, .km-muted { font-size: .8125rem; opacity: .65; }
.km-list { list-style: none; margin: 0; padding: 0; }
.km-kids { margin-top: 1rem; padding-left: 1.25rem; border-left: 2px solid rgb(128 128 128 / .2); }
.km-c { margin-bottom: 1.25rem; }
.km-meta { display: flex; align-items: center; gap: .5rem; }
.km-avatar { width: 1.75rem; height: 1.75rem; border-radius: 50%; flex: none; }
.km-initial { display: grid; place-items: center; background: rgb(128 128 128 / .2); font-size: .8125rem; font-weight: 600; }
.km-name { font-weight: 600; }
.km-body { white-space: pre-wrap; overflow-wrap: anywhere; margin: .375rem 0 .25rem 2.25rem; }
.km-actions { display: flex; gap: .75rem; margin-left: 2.25rem; }
.km-actions:empty { display: none; }
.km-link { background: none; border: 0; padding: 0; font: inherit; font-size: .8125rem;
           color: inherit; opacity: .65; cursor: pointer; text-decoration: underline; }
.km-link:hover { opacity: 1; }
.km-danger { color: #b91c1c; opacity: 1; }
.km-form { display: flex; flex-direction: column; align-items: flex-start; gap: .5rem; margin: 0 0 1.5rem 0; }
.km-kids .km-form, .km-c > .km-form { margin-left: 2.25rem; }
.km-text { width: 100%; font: inherit; padding: .5rem; border: 1px solid rgb(128 128 128 / .4);
           border-radius: .375rem; background: transparent; color: inherit; resize: vertical; }
.km-button { display: inline-block; font: inherit; font-size: .875rem; font-weight: 500; cursor: pointer;
             padding: .5rem .875rem; border: 0; border-radius: .375rem; background: #0369a1; color: #fff; text-decoration: none; }
.km-button:hover { background: #075985; }
.km-button:disabled { opacity: .5; cursor: default; }
.km-signin { margin: 0 0 1.5rem 0; }
.km-error { color: #b91c1c; font-size: .875rem; }
`;
        document.head.append(el('style', null, css));
    }
})();
