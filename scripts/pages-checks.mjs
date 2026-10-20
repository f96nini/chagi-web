import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import vm from 'node:vm';
import { fileURLToPath } from 'node:url';
import { preparePages } from './prepare-pages.mjs';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const temp = fs.mkdtempSync(path.join(os.tmpdir(), 'chagi-pages-checks-'));
try {
  fs.mkdirSync(path.join(temp, 'css'));
  fs.copyFileSync(path.join(root, 'Chagi.Web/wwwroot/css/app.css'), path.join(temp, 'css/app.css'));
  for (const base of ['/', '/chagi-web/', '/custom-project']) {
    fs.copyFileSync(path.join(root, 'Chagi.Web/wwwroot/index.html'), path.join(temp, 'index.html'));
    fs.copyFileSync(path.join(root, 'Chagi.Web/wwwroot/404.html'), path.join(temp, '404.html'));
    const normalized = preparePages(temp, base);
    const target = `${normalized}fighters/cHJvZmls?q=åäö&club=GBG#historik`;
    let redirected;
    vm.runInNewContext(fs.readFileSync(path.join(temp, '404.html'), 'utf8').match(/<script>([\s\S]*?)<\/script>/)[1], {
      document: {getElementById: () => ({})},
      location: {pathname: target.split('?')[0], search: '?' + target.split('?')[1].split('#')[0], hash: '#historik', replace: value => redirected = value},
      encodeURIComponent
    });
    let restored;
    vm.runInNewContext(fs.readFileSync(path.join(temp, 'index.html'), 'utf8').match(/<script>([\s\S]*?)<\/script>/)[1], {
      URL, URLSearchParams,
      document: {baseURI: `https://example.github.io${normalized}`},
      location: {search: redirected.slice(redirected.indexOf('?')), origin: 'https://example.github.io'},
      history: {replaceState: (_, __, value) => restored = value}
    });
    assert.equal(decodeURI(restored), target);
    assert.match(fs.readFileSync(path.join(temp, 'index.html'), 'utf8'), /href="css\/app\.css\?v=[a-f0-9]{12}"/);
    assert(fs.existsSync(path.join(temp, '.nojekyll')));
    console.log(`PASS: Direct-link fallback preserves route, query and hash under ${normalized}`);
  }
  assert.throws(() => preparePages(temp, '/bad<script>/'));
  console.log('All Pages routing checks passed.');
} finally {
  // Delete only the exact freshly-created temporary directory, after containment checks.
  const expectedRoot = path.resolve(os.tmpdir()) + path.sep;
  if (!path.resolve(temp).startsWith(expectedRoot) || !path.basename(temp).startsWith('chagi-pages-checks-'))
    throw new Error('Unexpected test directory.');
  fs.rmSync(temp, { recursive: true, force: true });
}
