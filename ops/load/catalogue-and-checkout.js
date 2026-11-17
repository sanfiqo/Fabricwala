// Load test for the shop front and the checkout, with k6 (https://k6.io).
//
//   k6 run -e BASE=http://localhost:5075 -e VARIANT_ID=1 ops/load/catalogue-and-checkout.js
//
// Run it ONLY against a database you can throw away: the checkout part places real (fake-paid) orders and uses up stock.
// It needs the API in Development (one-time code 123456, the fake payment gateway and the demo data of docs/RUN_LOCAL.md: the retailer is +919999900001).
// Two groups of virtual users run at the same time:
//   browse:   product list, search, filter counts, product page          p95 must stay under 300 ms
//   checkout: sign in once, then cart -> checkout -> payment, in a loop   p95 must stay under 800 ms
// The rate limits are per client address, so one machine running many virtual users will hit them (429): raise them in a test environment or run k6 from several addresses.
// Not run yet: no k6 and no MySQL server were available when this was written. Results go into docs/PERFORMANCE.md.
import http from 'k6/http';
import { check, sleep, group } from 'k6';
import { Counter } from 'k6/metrics';

const BASE = __ENV.BASE || 'http://localhost:5075';
const VARIANT_ID = Number(__ENV.VARIANT_ID || 1);
const PHONE = __ENV.PHONE || '+919999900001';
const PINCODE = __ENV.PINCODE || '452001';
const rejected = new Counter('rate_limited_responses');

export const options = {
  scenarios: {
    browse: { executor: 'ramping-vus', exec: 'browse', startVUs: 1, stages: [{ duration: '30s', target: 20 }, { duration: '2m', target: 20 }, { duration: '30s', target: 0 }] },
    checkout: { executor: 'constant-vus', exec: 'checkout', vus: 5, duration: '3m', startTime: '10s' },
  },
  thresholds: {
    'http_req_duration{group:::browse}': ['p(95)<300'],
    'http_req_duration{group:::checkout}': ['p(95)<800'],
    http_req_failed: ['rate<0.01'],
    rate_limited_responses: ['count<1'],
  },
};

const WORDS = ['jacket', 'hoodie', 'kurti', 'shawl', 'wool', 'cotton', 'fleece', 'sweater'];
const json = (extra = {}) => ({ headers: { 'Content-Type': 'application/json', ...extra } });
const ok = (res, name) => { if (res.status === 429) rejected.add(1); return check(res, { [`${name} 2xx`]: (r) => r.status >= 200 && r.status < 300 }); };

export function browse() {
  group('browse', () => {
    ok(http.get(`${BASE}/api/v1/products?pageSize=20`), 'list');
    ok(http.get(`${BASE}/api/v1/products?pageSize=20&sort=popular&page=${1 + Math.floor(Math.random() * 5)}`), 'list popular');
    const word = WORDS[Math.floor(Math.random() * WORDS.length)];
    ok(http.get(`${BASE}/api/v1/products?q=${word}`), 'search');
    ok(http.get(`${BASE}/api/v1/products/filters?q=${word}`), 'filters');
    ok(http.get(`${BASE}/api/v1/products/filters`), 'filters all');
  });
  sleep(1 + Math.random() * 2);
}

function signIn() {
  http.post(`${BASE}/api/v1/auth/otp/request`, JSON.stringify({ phoneNumber: PHONE }), json());
  const verify = http.post(`${BASE}/api/v1/auth/otp/verify`, JSON.stringify({ phoneNumber: PHONE, code: '123456' }), json());
  check(verify, { 'signed in': (r) => r.status === 200 });
  const csrf = http.get(`${BASE}/api/v1/auth/csrf`).json('token');
  return csrf;
}

let csrfToken; // per virtual user: k6 gives every virtual user its own copy of this variable and its own cookie jar

export function checkout() {
  if (!csrfToken) csrfToken = signIn();
  const headers = { 'X-CSRF-TOKEN': csrfToken };
  group('checkout', () => {
    ok(http.post(`${BASE}/api/v1/cart/items`, JSON.stringify({ variantId: VARIANT_ID, quantity: 50 }), json(headers)), 'cart add');
    const address = http.get(`${BASE}/api/v1/account/addresses`).json();
    const addressId = Array.isArray(address) && address.length ? address[0].id : 0;
    const key = `k6-${__VU}-${__ITER}-${Date.now()}`;
    const order = http.post(`${BASE}/api/v1/checkout`, JSON.stringify({ deliveryAddressId: addressId }), json({ ...headers, 'Idempotency-Key': key }));
    if (!ok(order, 'checkout')) return;
    const orderId = order.json('id');
    const payment = http.post(`${BASE}/api/v1/payments`, JSON.stringify({ orderId }), json({ ...headers, 'Idempotency-Key': `${key}-pay` }));
    if (!ok(payment, 'payment start')) return;
    ok(http.post(`${BASE}/api/v1/payments/${payment.json('gatewayOrderId')}/dev-complete`, null, json(headers)), 'payment complete');
  });
  sleep(2 + Math.random() * 3);
}
