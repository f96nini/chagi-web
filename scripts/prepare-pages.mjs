import fs from 'node:fs';
import path from 'node:path';
import { createHash } from 'node:crypto';
import { pathToFileURL } from 'node:url';

export function preparePages(directory, requestedBase = '/') {
  const base = `/${requestedBase.split('/').filter(Boolean).join('/')}${requestedBase.split('/').filter(Boolean).length ? '/' : ''}`;
  if (!/^\/(?:[A-Za-z0-9._~-]+\/)*$/.test(base)) throw new Error('Base path must be an absolute URL path, for example /chagi-web/.');
  const index = path.join(directory, 'index.html');
  const original = fs.readFileSync(index, 'utf8');
  if (!/<base href="[^"]*"\s*\/>/.test(original)) throw new Error('index.html is missing its base element.');
  const stylesheet = /<link rel="stylesheet" href="css\/app\.css(?:\?v=[a-f0-9]+)?"\s*\/>/;
  if (!stylesheet.test(original)) throw new Error('index.html is missing its app stylesheet.');
  const css = fs.readFileSync(path.join(directory, 'css', 'app.css'));
  const version = createHash('sha256').update(css).digest('hex').slice(0, 12);
  fs.writeFileSync(index, original
    .replace(/<base href="[^"]*"\s*\/>/, `<base href="${base}" />`)
    .replace(stylesheet, `<link rel="stylesheet" href="css/app.css?v=${version}" />`));
  const fallback = path.join(directory, '404.html');
  fs.writeFileSync(fallback, fs.readFileSync(fallback, 'utf8').replaceAll('__BASE_PATH__', base));
  fs.writeFileSync(path.join(directory, '.nojekyll'), '');
  console.log(`Prepared GitHub Pages assets for ${base}`);
  return base;
}

if (process.argv[1] && import.meta.url === pathToFileURL(path.resolve(process.argv[1])).href)
  preparePages(path.resolve(process.argv[2] ?? 'artifacts/site/wwwroot'), process.argv[3] ?? '/');
