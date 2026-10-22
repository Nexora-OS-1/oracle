import fs from 'node:fs';
import path from 'node:path';
import zlib from 'node:zlib';

// Draws the same focus-lens and compass mark as Assets/oracle-mark.svg.
const root = path.resolve(import.meta.dirname, '..');
const crcTable = new Uint32Array(256);
for (let n = 0; n < 256; n++) { let c = n; for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1; crcTable[n] = c >>> 0; }
function crc32(bytes) { let c = 0xffffffff; for (const b of bytes) c = crcTable[(c ^ b) & 255] ^ (c >>> 8); return (c ^ 0xffffffff) >>> 0; }
function chunk(type, data) { const name = Buffer.from(type); const body = Buffer.concat([name, data]); const len = Buffer.alloc(4); len.writeUInt32BE(data.length); const crc = Buffer.alloc(4); crc.writeUInt32BE(crc32(body)); return Buffer.concat([len, body, crc]); }
function insideRoundRect(x, y) { const dx = Math.max(0, Math.abs(x - .5) - .29), dy = Math.max(0, Math.abs(y - .5) - .29); return dx * dx + dy * dy <= .21 * .21; }
function distanceSegment(x, y, ax, ay, bx, by) { const dx = bx - ax, dy = by - ay; const t = Math.max(0, Math.min(1, ((x - ax) * dx + (y - ay) * dy) / (dx * dx + dy * dy))); return Math.hypot(x - ax - t * dx, y - ay - t * dy); }
function inTriangle(x, y, a, b, c) { const sign = (p1, p2, p3) => (p1[0] - p3[0]) * (p2[1] - p3[1]) - (p2[0] - p3[0]) * (p1[1] - p3[1]); const p = [x, y]; const d1 = sign(p, a, b), d2 = sign(p, b, c), d3 = sign(p, c, a); return !(d1 < 0 || d2 < 0 || d3 < 0) || !(d1 > 0 || d2 > 0 || d3 > 0); }
function pixelColor(x, y) {
  if (!insideRoundRect(x, y)) return [0, 0, 0, 0];
  const bg = [27 + Math.round(y * 4), 26 + Math.round(y * 3), 34 + Math.round(y * 5), 255];
  const r = Math.hypot(x - .4375, y - .4375); const ring = Math.abs(r - .28125) < .025;
  const handle = distanceSegment(x, y, .635, .635, .82, .82) < .031;
  const tri = inTriangle(x, y, [.25, .56], [.54, .25], [.46, .49]) && r < .24;
  if (ring || handle) return [183, 169, 238, 255];
  if (tri) return [228, 223, 255, 255];
  if (Math.hypot(x - .4375, y - .4375) < .022) return [141, 125, 196, 255];
  return bg;
}
function png(size) {
  const rows = Buffer.alloc((size * 4 + 1) * size); let p = 0;
  for (let y = 0; y < size; y++) { rows[p++] = 0; for (let x = 0; x < size; x++) { const sums = [0, 0, 0, 0], samples = 4; for (let sy = 0; sy < samples; sy++) for (let sx = 0; sx < samples; sx++) { const c = pixelColor((x + (sx + .5) / samples) / size, (y + (sy + .5) / samples) / size); for (let k = 0; k < 4; k++) sums[k] += c[k]; } for (let k = 0; k < 4; k++) rows[p++] = Math.round(sums[k] / (samples * samples)); } }
  const ihdr = Buffer.alloc(13); ihdr.writeUInt32BE(size, 0); ihdr.writeUInt32BE(size, 4); ihdr[8] = 8; ihdr[9] = 6;
  return Buffer.concat([Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]), chunk('IHDR', ihdr), chunk('IDAT', zlib.deflateSync(rows, { level: 9 })), chunk('IEND', Buffer.alloc(0))]);
}
const png256 = png(256); fs.writeFileSync(path.join(root, 'Assets', 'oracle-mark.png'), png256);
const sizes = [16, 24, 32, 48, 64, 256]; const images = sizes.map(png); const header = Buffer.alloc(6); header.writeUInt16LE(0, 0); header.writeUInt16LE(1, 2); header.writeUInt16LE(sizes.length, 4); const entries = Buffer.alloc(16 * sizes.length); let offset = 6 + entries.length;
images.forEach((image, i) => { const at = i * 16, size = sizes[i]; entries[at] = size === 256 ? 0 : size; entries[at + 1] = size === 256 ? 0 : size; entries[at + 2] = 0; entries[at + 3] = 0; entries.writeUInt16LE(1, at + 4); entries.writeUInt16LE(32, at + 6); entries.writeUInt32LE(image.length, at + 8); entries.writeUInt32LE(offset, at + 12); offset += image.length; });
fs.writeFileSync(path.join(root, 'Assets', 'oracle.ico'), Buffer.concat([header, entries, ...images]));
console.log('Generated oracle-mark.png and oracle.ico');
