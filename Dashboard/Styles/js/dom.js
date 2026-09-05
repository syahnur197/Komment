// Building markup out of API data is the one genuinely dangerous thing this app
// does. Razor escaped interpolated values for us; innerHTML does not, and a
// comment body is written by a stranger on the internet. So there is no
// innerHTML anywhere in this codebase: text goes in as a text node, always.

export function h(tag, attrs = {}, ...children) {
  const el = document.createElement(tag);

  for (const [name, value] of Object.entries(attrs)) {
    if (value === null || value === undefined || value === false) continue;

    if (name.startsWith('on') && typeof value === 'function') {
      el.addEventListener(name.slice(2).toLowerCase(), value);
    } else if (name === 'dataset') {
      Object.assign(el.dataset, value);
    } else if (value === true) {
      el.setAttribute(name, '');
    } else {
      el.setAttribute(name, String(value));
    }
  }

  append(el, children);

  return el;
}

function append(el, children) {
  for (const child of children.flat()) {
    if (child === null || child === undefined || child === false) continue;

    el.appendChild(child instanceof Node ? child : document.createTextNode(String(child)));
  }
}

export function clear(el) {
  while (el.firstChild) el.removeChild(el.firstChild);
}

// Replace an element's contents in one step — every list render in this app is
// "throw the old one away and build the new one", which is fast enough for a
// page of comments and removes any question of stale nodes.
export function render(el, ...children) {
  clear(el);
  append(el, children);
}

export const $ = (selector, root = document) => root.querySelector(selector);

// Error and status banners: one helper so every page reports failure the same
// way, and so an empty message reliably hides the banner rather than leaving an
// empty red box behind.
export function banner(el, message) {
  if (!el) return;

  el.textContent = message ?? '';
  el.hidden = !message;
}

export function formatDate(iso) {
  const parsed = new Date(iso);

  return Number.isNaN(parsed.valueOf())
    ? ''
    : parsed.toLocaleString(undefined, {
        day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit',
      });
}

// Reads a <form> into a plain object. Every form here posts JSON, so this is the
// whole of the "form library".
export const formData = (form) => Object.fromEntries(new FormData(form).entries());
