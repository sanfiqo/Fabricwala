// node scripts/probe.mjs <phone> <path>...   Prints real API answers (first 1500 chars). Dev helper, no secrets.
const BASE = process.env.API ?? 'http://127.0.0.1:5075'
const [phone, ...paths] = process.argv.slice(2)
let jar = {}
const cookie = () => Object.entries(jar).map(([k, v]) => `${k}=${v}`).join('; ')
const keep = (res) => { for (const c of res.headers.getSetCookie?.() ?? []) { const [kv] = c.split(';'); const i = kv.indexOf('='); jar[kv.slice(0, i)] = kv.slice(i + 1) } }
async function call(method, path, body, csrf) {
  const res = await fetch(BASE + '/api/v1/' + path, { method, headers: { 'Content-Type': 'application/json', 'Idempotency-Key': crypto.randomUUID(), Cookie: cookie(), ...(csrf ? { 'X-CSRF-TOKEN': csrf } : {}) }, body: body ? JSON.stringify(body) : undefined })
  keep(res)
  const text = await res.text()
  return { status: res.status, text }
}
import fs from 'node:fs'
const JARFILE = (process.env.TEMP ?? '.') + `/tc-probe-${phone.replace('+', '')}.json`
try { jar = JSON.parse(fs.readFileSync(JARFILE, 'utf8')) } catch { jar = {} }
let t = JSON.parse((await call('GET', 'auth/csrf')).text)
const token = t.token ?? t.requestToken
const meRes = await call('GET', 'auth/me')
if (meRes.status !== 200) {
  console.log('otp request', (await call('POST', 'auth/otp/request', { phoneNumber: phone }, token)).status)
  const v = await call('POST', 'auth/otp/verify', { phoneNumber: phone, code: '123456' }, token)
  console.log('verify', v.status, v.text.slice(0, 200))
  fs.writeFileSync(JARFILE, JSON.stringify(jar))
} else console.log('session reused')
t = JSON.parse((await call('GET', 'auth/csrf')).text)
for (const p of paths) {
  const [m, path, b] = p.includes(' ') ? [p.split(' ')[0], p.split(' ')[1], p.split(' ').slice(2).join(' ')] : ['GET', p]
  const r = await call(m, path, b ? JSON.parse(b) : undefined, t.token ?? t.requestToken)
  console.log(`\n== ${m} ${path} -> ${r.status}\n${r.text.slice(0, 1500)}`)
}
