import { requireAdmin } from '../auth.js';
import { $ } from '../dom.js';

export default function home(root) {
  const account = requireAdmin();
  if (!account) return;

  $('[data-signed-in-as]', root).textContent = `Signed in as ${account.name}.`;
  $('[data-content]', root).hidden = false;
}
