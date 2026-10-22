import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';

const root = process.argv[3] ? path.resolve(process.argv[3]) : path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', 'website');
const base = process.argv[2] ?? 'http://localhost:8080/';
const must = (ok, message) => { if (!ok) throw new Error(message); };
const response = await fetch(base);
must(response.ok, `Site returned ${response.status}`);
const html = await response.text();
const ids = new Set([...html.matchAll(/\bid="([^"]+)"/g)].map(match => match[1]));
for (const match of html.matchAll(/\b(?:href|src)="([^"]+)"/g)) {
  const ref = match[1];
  if (ref.startsWith('#')) { must(ids.has(ref.slice(1)), `Broken in-page link: ${ref}`); continue; }
  if (/^https?:/i.test(ref)) continue;
  const filePath = path.resolve(root, decodeURIComponent(new URL(ref, base).pathname.replace(/^\//, '')));
  must(filePath.startsWith(root), `Path escapes the website root: ${ref}`);
  must(fs.existsSync(filePath), `Missing local asset or download: ${ref}`);
}
const releaseResponse = await fetch(new URL('release.json', base), { cache: 'no-store' });
must(releaseResponse.ok, 'Release manifest is missing.');
const release = await releaseResponse.json();
must(release.version === '2.0.0' && release.architecture === 'x64' && release.selfContained, 'Release information does not match the intended package.');
const head = await fetch(new URL(release.package, base), { method: 'HEAD', cache: 'no-store' });
must(head.ok, `Download URL returned ${head.status}`);
must(Number(head.headers.get('content-length')) === release.bytes, 'Download size does not match the release manifest.');
const pkg = await fetch(new URL(release.package, base), { cache: 'no-store' });
must(pkg.ok, `Package GET returned ${pkg.status}`);
const hash = crypto.createHash('sha256'); let bytes = 0;
for await (const chunk of pkg.body) { bytes += chunk.byteLength; hash.update(chunk); }
const checksum = hash.digest('hex').toUpperCase();
must(bytes === release.bytes, 'Downloaded package byte count differs from manifest.');
must(checksum === release.sha256, 'Downloaded package SHA-256 differs from manifest.');
console.log(`Site links and assets: OK\nDownload: ${release.package}\nBytes: ${bytes}\nSHA-256: ${checksum}`);
