import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const packageName = 'ORACLE-2.0.0-win-x64-build4.zip';
const packagePath = path.join(root, 'website', 'downloads', packageName);
if (!fs.existsSync(packagePath)) throw new Error(`Build the release package first: ${packagePath}`);
const stat = fs.statSync(packagePath);
const hash = await new Promise((resolve, reject) => { const sha = crypto.createHash('sha256'); const stream = fs.createReadStream(packagePath); stream.on('data', data => sha.update(data)); stream.on('error', reject); stream.on('end', () => resolve(sha.digest('hex').toUpperCase())); });
const sizeMB = (stat.size / 1_000_000).toFixed(1);
const sizeMiB = (stat.size / 1_048_576).toFixed(1);
const release = { version: '2.0.0', architecture: 'x64', selfContained: true, packageType: 'Self-contained ZIP', package: `downloads/${packageName}`, bytes: stat.size, size: `${sizeMB} MB (${sizeMiB} MiB)`, sha256: hash };
fs.writeFileSync(path.join(root, 'website', 'release.json'), `${JSON.stringify(release, null, 2)}\n`);
console.log(JSON.stringify(release, null, 2));
