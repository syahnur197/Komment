import { chrome } from './layout.js';

import home from './pages/home.js';
import login from './pages/login.js';
import register from './pages/register.js';
import sites from './pages/sites.js';
import siteEditor from './pages/site-editor.js';
import comments from './pages/comments.js';
import commentEditor from './pages/comment-editor.js';

// Blazor still owns routing: it matches the URL and renders the shell for that
// page, marked with data-page. This just runs the matching module against it.
// Imports are static so Vite emits one bundle under a stable filename, which is
// what Blazor's MapStaticAssets fingerprints.
const pages = {
  home,
  login,
  register,
  sites,
  'site-editor': siteEditor,
  comments,
  'comment-editor': commentEditor,
};

export function boot() {
  chrome();

  const root = document.querySelector('[data-page]');
  const page = pages[root?.dataset.page];

  page?.(root);
}
