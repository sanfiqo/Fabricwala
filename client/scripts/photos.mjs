// npm run photos: scans public/photos for <productId>-<n>.jpg and writes public/photos/manifest.json.
import fs from 'node:fs'
import path from 'node:path'

const dir = path.resolve('public/photos')
fs.mkdirSync(dir, { recursive: true })
const manifest = {}
for (const f of fs.readdirSync(dir)) {
  const m = /^(\d+)-(\d+)\.jpe?g$/i.exec(f)
  if (m) (manifest[m[1]] ??= []).push(Number(m[2]))
}
for (const k of Object.keys(manifest)) manifest[k].sort((a, b) => a - b)
fs.writeFileSync(path.join(dir, 'manifest.json'), JSON.stringify(manifest, null, 2) + '\n')
console.log(`Photos found for ${Object.keys(manifest).length} products. Wrote public/photos/manifest.json`)
