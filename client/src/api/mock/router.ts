import { ApiError } from '../errors'
import type {
  Claim, NotificationItem, AdminBusiness, AddressInput, Cart, CartGroup, CartItem, CreateBusinessInput, LandedCost, Me, Order, PaymentIntent, PriceSlab,
  ProductDetail, ProductInput, ProductQuery, ProductSummary, SellerOrder, ShippingRateInput, VerificationStatus,
  MyQuotationRow, Quotation, QuotationInput, RfqDetail, RfqInput, RfqStatus, RfqSummary,
} from '../types'
import {
  type Db, type DbBusiness, type DbOrder, type DbProduct, type DbQuotation, type DbRfq, type DbSellerOrder, type DbUser, getDb, getSession, makeAddress, nextId, saveDb, setSession, swatch,
} from './db'

interface Ctx {
  query: Record<string, string>
  body: any // eslint-disable-line @typescript-eslint/no-explicit-any
  form?: FormData
  params: string[]
  db: Db
}
type Handler = (ctx: Ctx) => unknown | Promise<unknown>

const fail = (status: number, msg: string, code?: string): never => {
  throw new ApiError(status, msg, undefined, code)
}
const clone = <T,>(v: T): T => JSON.parse(JSON.stringify(v)) as T
const wait = (ms: number) => new Promise((r) => setTimeout(r, ms))

// ---------- session helpers
function currentUser(db: Db): DbUser {
  const phone = getSession()
  const u = phone ? db.users.find((x) => x.phone === phone) : undefined
  return u ?? fail(401, 'Please log in', 'NOT_LOGGED_IN')
}
function currentBusiness(db: Db): DbBusiness {
  const u = currentUser(db)
  return db.businesses.find((b) => b.id === u.businessId) ?? fail(403, 'No business', 'NO_BUSINESS')
}
function requireApproved(db: Db, type: 'Retailer' | 'Manufacturer'): DbBusiness {
  const b = currentBusiness(db)
  if (b.type !== type) fail(403, 'Not allowed for this account', 'NOT_ALLOWED')
  if (b.verificationStatus !== 'Approved') fail(403, 'Business is not approved yet', 'NOT_APPROVED')
  return b
}
function requireAdmin(db: Db) {
  if (!currentUser(db).isAdmin) fail(403, 'Admin only', 'NOT_ALLOWED')
}
function toMe(db: Db, u: DbUser): Me {
  const b = db.businesses.find((x) => x.id === u.businessId)
  const roles: Me['roles'] = u.isAdmin ? ['Admin'] : b && b.verificationStatus === 'Approved' ? [b.type] : []
  return {
    userId: u.phone, phone: u.phone, fullName: u.fullName || null, roles,
    business: b ? { id: b.id, type: b.type, displayName: b.displayName, verificationStatus: b.verificationStatus, rejectionNote: b.rejectionNote } : null,
  }
}

// ---------- pricing helpers
const slabPrice = (p: DbProduct, qty: number) =>
  [...p.slabs].reverse().find((s) => qty >= s.minQty)?.pricePerUnit ?? p.slabs[0]?.pricePerUnit ?? 0

function freight(db: Db, p: DbProduct, qty: number, toCityId: number) {
  const seller = db.businesses.find((b) => b.id === p.sellerBusinessId)
  const from = seller?.addresses.find((a) => a.addressType === 'Pickup')?.cityId
  const rate = db.rates.find((r) => r.isActive && r.fromCityId === from && r.toCityId === toCityId)
  if (!rate) return { total: null as number | null, transit: null as number | null }
  const kg = (p.weightPerUnitGrams * qty) / 1000
  const total = rate.rateType === 'PerKg' ? Math.max(rate.rate * kg, rate.minCharge) : rate.rate * qty
  return { total: Math.round(total), transit: rate.transitDays }
}

function landedCost(db: Db, productId: number, qty: number, cityId: number): LandedCost {
  const p = db.products.find((x) => x.id === productId) ?? fail(404, 'Product not found', 'PRODUCT_NOT_FOUND')
  const price = slabPrice(p, qty)
  const f = freight(db, p, qty, cityId)
  const gst = (price * p.gstRate) / 100
  const ship = f.total === null ? null : f.total / qty
  const landed = ship === null ? null : price + ship + gst
  return {
    productId, qty, cityId, slabPrice: price, shippingPerUnit: ship === null ? null : Math.round(ship * 100) / 100,
    gstPerUnit: Math.round(gst * 100) / 100, platformFeePerUnit: 0,
    landedPerUnit: landed === null ? null : Math.round(landed * 100) / 100, total: landed === null ? null : Math.round(landed * qty), transitDays: f.transit,
  }
}

// ---------- cart
function buildCart(db: Db, business: DbBusiness): Cart {
  const raw = (db.carts[business.id] ??= [])
  const delivery = business.addresses.filter((a) => a.addressType === 'Delivery')
  const toCity = (delivery.find((a) => a.isDefault) ?? delivery[0])?.cityId ?? 1
  const found = raw
    .map((ci) => {
      const p = db.products.find((x) => x.variants.some((v) => v.id === ci.variantId))
      const v = p?.variants.find((x) => x.id === ci.variantId)
      return p && v ? { ci, p, v } : null
    })
    .filter((x): x is NonNullable<typeof x> => x !== null)
  const qtyByProduct = new Map<number, number>()
  for (const f of found) qtyByProduct.set(f.p.id, (qtyByProduct.get(f.p.id) ?? 0) + f.ci.quantity)

  const groups = new Map<number, CartGroup>()
  const shipDone = new Set<number>()
  for (const { ci, p, v } of found) {
    const total = qtyByProduct.get(p.id) ?? 0
    const unitPrice = slabPrice(p, total)
    const item: CartItem = {
      id: ci.id, variantId: v.id, productId: p.id, slug: p.slug, title: p.title, size: v.size, colour: v.colour, imageUrl: p.imageUrl,
      quantity: ci.quantity, moq: p.moq, unit: p.unit, unitPrice, lineTotal: unitPrice * ci.quantity,
    }
    let g = groups.get(p.sellerBusinessId)
    if (!g) {
      g = { manufacturerId: p.sellerBusinessId, manufacturerName: p.sellerName, items: [], subTotal: 0, shippingAmount: 0, taxAmount: 0, total: 0 }
      groups.set(p.sellerBusinessId, g)
    }
    g.items.push(item)
    g.subTotal += item.lineTotal
    g.taxAmount += Math.round((item.lineTotal * p.gstRate) / 100)
    if (!shipDone.has(p.id)) {
      shipDone.add(p.id)
      const f = freight(db, p, total, toCity)
      g.shippingAmount = g.shippingAmount === null || f.total === null ? null : g.shippingAmount + f.total
    }
  }
  const list = [...groups.values()]
  for (const g of list) g.total = g.subTotal + (g.shippingAmount ?? 0) + g.taxAmount
  const shipNull = list.some((g) => g.shippingAmount === null)
  return {
    groups: list, subTotal: list.reduce((a, g) => a + g.subTotal, 0),
    shippingTotal: shipNull ? null : list.reduce((a, g) => a + (g.shippingAmount ?? 0), 0),
    taxTotal: list.reduce((a, g) => a + g.taxAmount, 0), grandTotal: list.reduce((a, g) => a + g.total, 0), addresses: delivery,
  }
}

// ---------- products
const summary = (p: DbProduct): ProductSummary => {
  const prices = p.slabs.map((s) => s.pricePerUnit)
  return {
    id: p.id, slug: p.slug, title: p.title, categoryId: p.categoryId, sellerBusinessId: p.sellerBusinessId, sellerName: p.sellerName,
    sellerCity: p.sellerCity, imageUrl: p.media[0]?.url ?? null, moq: p.moq, unit: p.unit, priceFrom: prices.length ? Math.min(...prices) : 0,
    priceTo: prices.length ? Math.max(...prices) : 0, material: p.material, status: p.status,
  }
}
const detail = (p: DbProduct): ProductDetail => {
  const { createdAt: _c, ...rest } = p // eslint-disable-line @typescript-eslint/no-unused-vars
  return { ...clone(rest), ...summary(p) } as ProductDetail
}

function validateSlabs(moq: number, slabs: PriceSlab[]) {
  if (!slabs.length) fail(422, 'Add at least one price slab', 'SLAB_NONE')
  const s = [...slabs].sort((a, b) => a.minQty - b.minQty)
  if (s[0].minQty !== moq) fail(422, 'First slab must start at the MOQ', 'SLAB_FIRST')
  s.forEach((x, i) => {
    if (x.maxQty !== null && x.maxQty < x.minQty) fail(422, 'Slab max must be above min', 'SLAB_RANGE')
    if (i < s.length - 1 && (x.maxQty === null || x.maxQty + 1 !== s[i + 1].minQty)) fail(422, 'Price slabs overlap or leave a gap', 'SLAB_OVERLAP')
  })
}

async function thumb(file: File): Promise<string> {
  try {
    const bmp = await createImageBitmap(file)
    const scale = Math.min(1, 480 / Math.max(bmp.width, bmp.height))
    const c = document.createElement('canvas')
    c.width = Math.round(bmp.width * scale)
    c.height = Math.round(bmp.height * scale)
    c.getContext('2d')!.drawImage(bmp, 0, 0, c.width, c.height)
    return c.toDataURL('image/jpeg', 0.7)
  } catch {
    return swatch('#cbd5e1', '#e2e8f0')
  }
}

// ---------- orders
function pushStatus(so: DbSellerOrder, status: SellerOrder['status'], note?: string) {
  so.status = status
  so.history.push({ status, note: note ?? null, changedAt: new Date().toISOString() })
}
const publicSeller = (so: DbSellerOrder): SellerOrder => {
  const { manufacturerId: _m, retailerId: _r, ...rest } = so // eslint-disable-line @typescript-eslint/no-unused-vars
  return clone(rest)
}
const publicOrder = (o: DbOrder): Order => {
  const { retailerId: _r, sellerOrders, ...rest } = o // eslint-disable-line @typescript-eslint/no-unused-vars
  return { ...clone(rest), sellerOrders: sellerOrders.map(publicSeller) }
}
function findSellerOrder(db: Db, id: number, manufacturerId: number): DbSellerOrder {
  for (const o of db.orders) for (const so of o.sellerOrders) if (so.id === id && so.manufacturerId === manufacturerId) return so
  return fail(404, 'Order not found', 'ORDER_NOT_FOUND')
}

// ---------- admin
const adminView = (b: DbBusiness): AdminBusiness => ({
  id: b.id, type: b.type, legalName: b.legalName, displayName: b.displayName, gstin: b.gstin, phone: b.phone,
  cityName: b.addresses[0]?.cityName ?? '', verificationStatus: b.verificationStatus, rejectionNote: b.rejectionNote, createdAt: b.createdAt, documents: clone(b.documents),
})

// ---------- routes
const routes: Array<[string, RegExp, Handler]> = []
const on = (method: string, pattern: string, h: Handler) =>
  routes.push([method, new RegExp(`^${pattern.replace(/:id/g, '(\\d+)').replace(/:slug/g, '([^/]+)')}$`), h])

// Auth
on('GET', 'auth/csrf', () => ({ token: 'mock-token' }))
on('POST', 'auth/otp/request', ({ body }) => {
  if (!/^\d{10}$/.test(String(body.phoneNumber))) fail(400, 'Enter a valid mobile number', 'PHONE_INVALID')
})
on('POST', 'auth/otp/verify', ({ body, db }) => {
  if (body.code !== '123456') fail(400, 'Wrong code. In demo mode use 123456.', 'OTP_INVALID')
  const phone = String(body.phoneNumber)
  let u = db.users.find((x) => x.phone === phone)
  if (!u) {
    u = { phone, fullName: '', isAdmin: false, businessId: null }
    db.users.push(u)
  }
  setSession(phone)
  return toMe(db, u)
})
on('POST', 'auth/logout', () => setSession(null))
on('GET', 'auth/me', ({ db }) => toMe(db, currentUser(db)))

// Business
// Pincode lookup (mock): first three digits pick the city. The real one is a backend request.
const PIN_PREFIX: Record<string, number> = { '190': 1, '191': 1, '180': 2, '181': 2, '192': 3, '193': 4, '141': 10, '395': 11, '132': 12, '110': 13 }
on('GET', 'pincodes/:slug', ({ db, params }) => {
  const cityId = PIN_PREFIX[params[0].slice(0, 3)] ?? fail(404, 'Pincode not found', 'NOT_FOUND')
  const c = db.cities.find((x) => x.id === cityId)!
  const st = db.states.find((x) => x.id === c.stateId)!
  return { pincode: params[0], cityId: c.id, cityName: c.name, stateId: st.id, stateName: st.name }
})
on('POST', 'businesses', ({ body, db }) => {
  const u = currentUser(db)
  if (u.businessId) fail(409, 'You already have a business', 'BUSINESS_EXISTS')
  const input = body as CreateBusinessInput
  if (db.businesses.some((b) => b.gstin && b.gstin === input.gstin)) fail(409, 'This GST number is already registered', 'GST_DUPLICATE')
  const b: DbBusiness = {
    id: nextId(), type: input.type, displayName: input.displayName, legalName: input.legalName, gstin: input.gstin, pan: input.pan ?? null,
    phone: input.phone, email: input.email ?? null, verificationStatus: 'Pending', rejectionNote: null, addresses: [], documents: [],
    createdAt: new Date().toISOString(), ownerPhone: u.phone,
  }
  db.businesses.push(b)
  u.businessId = b.id
  return clone(b)
})
on('GET', 'businesses/me', ({ db }) => clone(currentBusiness(db)))
on('POST', 'businesses/me/addresses', ({ body, db }) => {
  const b = currentBusiness(db)
  const a = body as AddressInput
  const addr = makeAddress(nextId(), a.addressType, a.line1, a.cityId, a.pincode, a.isDefault)
  addr.line2 = a.line2
  b.addresses.push(addr)
  return addr
})
on('PUT', 'businesses/me/addresses/:id/default', ({ params, db }) => {
  const b = currentBusiness(db)
  const target = b.addresses.find((a) => a.id === Number(params[0])) ?? fail(404, 'Address not found', 'not_found')
  b.addresses.filter((a) => a.addressType === target.addressType).forEach((a) => (a.isDefault = a === target))
})
on('POST', 'businesses/me/documents', ({ form, db }) => {
  const b = currentBusiness(db)
  const d = { id: nextId(), docType: form!.get('docType') as 'GST', status: 'Pending' as const, reviewNote: null, url: null }
  b.documents.push(d)
  return d
})

// Master
on('GET', 'categories', ({ db }) => db.categories)
on('GET', 'states', ({ db }) => db.states)
on('GET', 'cities', ({ db, query }) =>
  db.cities.filter((c) => (query.zone === 'delivery' ? c.isDeliveryZone : query.zone === 'sourcing' ? c.isSourcingHub : true)))

// Products (public)
on('GET', 'products', ({ db, query }) => {
  const q = query as unknown as Record<string, string>
  const n = (k: string) => (q[k] ? Number(q[k]) : undefined)
  const term = (q.q ?? '').toLowerCase().trim()
  const sort = (q.sort as ProductQuery['sort']) ?? 'new'
  let list = db.products.filter((p) => p.status === 'Active')
  if (term) {
    list = list.filter((p) =>
      [p.title, p.material, p.sellerName, db.categories.find((c) => c.id === p.categoryId)?.name ?? ''].some((t) => t?.toLowerCase().includes(term)))
  }
  if (n('categoryId')) list = list.filter((p) => p.categoryId === n('categoryId'))
  if (q.material) list = list.filter((p) => p.material === q.material)
  if (q.season) list = list.filter((p) => p.season === q.season)
  if (n('maxMoq')) list = list.filter((p) => p.moq <= n('maxMoq')!)
  if (n('minPrice')) list = list.filter((p) => summary(p).priceFrom >= n('minPrice')!)
  if (n('maxPrice')) list = list.filter((p) => summary(p).priceFrom <= n('maxPrice')!)
  const by: Record<string, (a: DbProduct, b: DbProduct) => number> = {
    new: (a, b) => b.createdAt.localeCompare(a.createdAt),
    'price-asc': (a, b) => summary(a).priceFrom - summary(b).priceFrom,
    'price-desc': (a, b) => summary(b).priceFrom - summary(a).priceFrom,
    'moq-asc': (a, b) => a.moq - b.moq,
  }
  list = [...list].sort(by[sort] ?? by.new)
  const page = n('page') ?? 1
  const pageSize = n('pageSize') ?? 20
  return { items: list.slice((page - 1) * pageSize, page * pageSize).map(summary), total: list.length, page, pageSize }
})
on('GET', 'products/:slug', ({ db, params }) => {
  const p = db.products.find((x) => x.slug === params[0] && x.status === 'Active') ?? fail(404, 'Product not found', 'PRODUCT_NOT_FOUND')
  return detail(p)
})
on('GET', 'landed-cost', ({ db, query }) => {
  currentBusiness(db) // prices are visible while waiting for approval; ordering is not
  return landedCost(db, Number(query.productId), Math.max(1, Number(query.qty)), Number(query.cityId))
})

// Seller dashboard (mock). The real one is computed on the server (a backend request).
on('GET', 'seller/dashboard', ({ db }) => {
  const b = requireApproved(db, 'Manufacturer')
  const mine = db.orders.flatMap((o) => o.sellerOrders.map((s) => ({ o, s }))).filter(({ s }) => s.manufacturerId === b.id)
  const now = new Date()
  const monthKey = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`
  const sold = mine.filter(({ s }) => s.paymentStatus === 'Paid' && !['Rejected', 'Cancelled'].includes(s.status))
  const byMonth = new Map<string, number>()
  for (const { s } of sold) byMonth.set(monthKey(new Date(s.placedAt)), (byMonth.get(monthKey(new Date(s.placedAt))) ?? 0) + s.subTotal)
  const months = Array.from({ length: 6 }, (_, i) => new Date(now.getFullYear(), now.getMonth() - (5 - i), 1))
  const salesByMonth = months.map((d, i) => ({ month: d.toLocaleDateString('en-IN', { month: 'short' }), amount: byMonth.get(monthKey(d)) ?? Math.round(120000 * (0.4 + i * 0.12)) }))
  const thisMonth = salesByMonth[5].amount
  const last = salesByMonth[4].amount
  const waiting = mine.filter(({ s }) => s.status === 'PendingAcceptance')
  const delivered = mine.filter(({ s }) => s.status === 'ReachedKashmir' || s.status === 'Delivered')
  const topMap = new Map<number, { productId: number; title: string; pieces: number; amount: number }>()
  for (const { s } of sold) for (const i of s.items) {
    const t = topMap.get(i.productId) ?? { productId: i.productId, title: i.title, pieces: 0, amount: 0 }
    t.pieces += i.quantity
    t.amount += i.lineTotal
    topMap.set(i.productId, t)
  }
  const low = db.products.filter((p) => p.sellerBusinessId === b.id).flatMap((p) => p.variants.filter((v) => v.stockQty > 0 && v.stockQty < 60).map((v) => ({ productId: p.id, title: p.title, variant: `${v.colour} ${v.size}`, qty: v.stockQty })))
  return {
    newOrders: { count: waiting.length, value: waiting.reduce((a, { s }) => a + s.subTotal, 0), cities: [...new Set(waiting.map(({ s }) => s.deliveryCity))], acceptBy: waiting.length ? new Date(Math.min(...waiting.map(({ s }) => new Date(s.placedAt).getTime())) + 24 * 3_600_000).toISOString() : null },
    toPack: mine.filter(({ s }) => s.status === 'Accepted').length,
    salesThisMonth: thisMonth,
    salesChangePct: last > 0 ? Math.round(((thisMonth - last) / last) * 100) : null,
    payoutDue: delivered.filter(({ s }) => s.status === 'ReachedKashmir').reduce((a, { s }) => a + (s.subTotal - s.commissionAmount), 0),
    nextPayoutDate: delivered.length ? new Date(Date.now() + 3 * 86_400_000).toISOString() : null,
    rating: 4.6,
    ratingCount: 212,
    salesByMonth,
    lowStock: low.slice(0, 3),
    topProducts: [...topMap.values()].sort((a, c) => c.amount - a.amount).slice(0, 3),
  } satisfies import('../types').SellerDashboard
})

// Payouts (mock). The real numbers come from the server (a backend request).
on('GET', 'seller/payouts', ({ db, query }) => {
  const b = requireApproved(db, 'Manufacturer')
  const mine = db.orders.flatMap((o) => o.sellerOrders.map((s) => ({ o, s }))).filter(({ s }) => s.manufacturerId === b.id && s.paymentStatus === 'Paid' && !['Rejected', 'Cancelled', 'PendingAcceptance'].includes(s.status))
  const rows = mine.map(({ o, s }) => {
    const reached = s.history.find((h) => h.status === 'ReachedKashmir')?.changedAt ?? null
    const delivered = s.history.find((h) => h.status === 'Delivered')?.changedAt ?? reached
    const status: 'Paid' | 'Scheduled' | 'OnHold' = s.status === 'Delivered' ? 'Paid' : s.status === 'ReachedKashmir' ? 'Scheduled' : 'OnHold'
    const date = status === 'OnHold' ? null : new Date(new Date(delivered ?? s.placedAt).getTime() + (status === 'Scheduled' ? 7 * 86_400_000 : 0)).toISOString()
    return { id: s.id, orderNumber: s.subOrderNumber, buyer: s.retailerName, placedAt: o.placedAt, deliveredAt: delivered, amount: s.subTotal - s.commissionAmount, status, date }
  }).sort((a, c) => c.placedAt.localeCompare(a.placedAt))
  if (query.csv) return ['Order,Shop,Amount,Status,Date', ...rows.map((r) => [r.orderNumber, r.buyer, r.amount, r.status, r.date ?? ''].join(','))].join('\n')
  const sched = rows.filter((r) => r.status === 'Scheduled')
  const month = new Date().getMonth()
  return {
    next: { amount: sched.reduce((a, r) => a + r.amount, 0), date: sched[0]?.date ?? null },
    onHold: rows.filter((r) => r.status === 'OnHold').reduce((a, r) => a + r.amount, 0),
    paidThisMonth: rows.filter((r) => r.status === 'Paid' && r.date && new Date(r.date).getMonth() === month).reduce((a, r) => a + r.amount, 0),
    rows,
  }
})
on('PUT', 'seller/products/:id/status', ({ db, params, body }) => {
  const b = requireApproved(db, 'Manufacturer')
  const p = db.products.find((x) => x.id === Number(params[0]) && x.sellerBusinessId === b.id) ?? fail(404, 'Product not found', 'PRODUCT_NOT_FOUND')
  p.status = (body as { status: 'Active' | 'Inactive' }).status
  return summary(p)
})

// Seller products
on('GET', 'seller/products', ({ db }) => {
  const b = requireApproved(db, 'Manufacturer')
  return db.products.filter((p) => p.sellerBusinessId === b.id).map(summary)
})
const ownProduct = (db: Db, id: number) => {
  const b = requireApproved(db, 'Manufacturer')
  return db.products.find((p) => p.id === id && p.sellerBusinessId === b.id) ?? fail(404, 'Product not found', 'PRODUCT_NOT_FOUND')
}
on('GET', 'seller/products/:id', ({ db, params }) => detail(ownProduct(db, Number(params[0]))))
const applyInput = (p: DbProduct, i: ProductInput) => {
  Object.assign(p, {
    title: i.title, categoryId: i.categoryId, description: i.description, material: i.material, fabricSpec: i.fabricSpec, season: i.season,
    hsnCode: i.hsnCode, gstRate: i.gstRate, moq: i.moq, unit: i.unit, weightPerUnitGrams: i.weightPerUnitGrams, dispatchDays: i.dispatchDays,
    returnPolicy: i.returnPolicy, status: i.status,
  })
  p.variants = i.variants.map((v) => ({ ...v, id: v.id ?? nextId() }))
}
on('POST', 'seller/products', ({ db, body }) => {
  const b = requireApproved(db, 'Manufacturer')
  const i = body as ProductInput
  const id = nextId()
  const p = {
    id, slug: `${i.title.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/(^-|-$)/g, '')}-${id}`, sellerBusinessId: b.id, sellerName: b.displayName,
    sellerCity: b.addresses.find((a) => a.addressType === 'Pickup')?.cityName ?? '', imageUrl: null, priceFrom: 0, priceTo: 0, media: [], slabs: [],
    createdAt: new Date().toISOString(),
  } as unknown as DbProduct
  applyInput(p, i)
  db.products.push(p)
  return detail(p)
})
on('PUT', 'seller/products/:id', ({ db, params, body }) => {
  const p = ownProduct(db, Number(params[0]))
  applyInput(p, body as ProductInput)
  return detail(p)
})
on('DELETE', 'seller/products/:id', ({ db, params }) => {
  const p = ownProduct(db, Number(params[0]))
  db.products.splice(db.products.indexOf(p), 1)
})
on('PUT', 'seller/products/:id/slabs', ({ db, params, body }) => {
  const p = ownProduct(db, Number(params[0]))
  const slabs = (body as { slabs: PriceSlab[] }).slabs
  validateSlabs(p.moq, slabs)
  p.slabs = [...slabs].sort((a, b) => a.minQty - b.minQty).map((s) => ({ ...s, id: s.id ?? nextId() }))
  return clone(p.slabs)
})
on('POST', 'seller/products/:id/media', async ({ db, params, form }) => {
  const p = ownProduct(db, Number(params[0]))
  const m = { id: nextId(), url: await thumb(form!.get('file') as File), mediaType: 'Image' as const, sortOrder: p.media.length }
  p.media.push(m)
  return m
})

// Cart
on('GET', 'cart', ({ db }) => buildCart(db, requireApproved(db, 'Retailer')))
on('POST', 'cart/items', ({ db, body }) => {
  const b = requireApproved(db, 'Retailer')
  const { variantId, quantity } = body as { variantId: number; quantity: number }
  const p = db.products.find((x) => x.variants.some((v) => v.id === variantId)) ?? fail(404, 'Item not found', 'ITEM_NOT_FOUND')
  if (p.status !== 'Active') fail(422, 'This product is not available', 'PRODUCT_UNAVAILABLE')
  const items = (db.carts[b.id] ??= [])
  const ex = items.find((i) => i.variantId === variantId)
  if (ex) ex.quantity += quantity
  else items.push({ id: nextId(), variantId, quantity })
  return buildCart(db, b)
})
on('PUT', 'cart/items/:id', ({ db, params, body }) => {
  const b = requireApproved(db, 'Retailer')
  const it = (db.carts[b.id] ?? []).find((i) => i.id === Number(params[0])) ?? fail(404, 'Item not found', 'ITEM_NOT_FOUND')
  it.quantity = Math.max(1, Number((body as { quantity: number }).quantity))
  return buildCart(db, b)
})
on('DELETE', 'cart/items/:id', ({ db, params }) => {
  const b = requireApproved(db, 'Retailer')
  db.carts[b.id] = (db.carts[b.id] ?? []).filter((i) => i.id !== Number(params[0]))
  return buildCart(db, b)
})

// Orders
on('POST', 'checkout', ({ db, body }) => {
  const b = requireApproved(db, 'Retailer')
  const u = currentUser(db)
  const cart = buildCart(db, b)
  if (!cart.groups.length) fail(422, 'Cart is empty', 'CART_EMPTY')
  const addr = b.addresses.find((a) => a.id === (body as { deliveryAddressId: number }).deliveryAddressId) ?? fail(422, 'Choose a delivery address', 'ADDRESS_REQUIRED')
  const totals = new Map<number, number>()
  for (const g of cart.groups) for (const i of g.items) totals.set(i.productId, (totals.get(i.productId) ?? 0) + i.quantity)
  for (const g of cart.groups) for (const i of g.items) if ((totals.get(i.productId) ?? 0) < i.moq) fail(422, `Minimum order for ${i.title} is ${i.moq}`, 'MOQ_NOT_MET')
  const orderId = nextId()
  const number = `TC-${new Date().getFullYear() % 100}${orderId}`
  const now = new Date().toISOString()
  const sellerOrders: DbSellerOrder[] = cart.groups.map((g, idx) => ({
    id: nextId(), orderId, orderNumber: number, subOrderNumber: `${number}-${String.fromCharCode(65 + idx)}`, manufacturerName: g.manufacturerName,
    retailerName: b.displayName, deliveryCity: addr.cityName, status: 'PendingAcceptance', paymentStatus: 'Pending', placedAt: now,
    items: g.items.map((i) => ({ id: nextId(), variantId: i.variantId, productId: i.productId, title: i.title, size: i.size, colour: i.colour, quantity: i.quantity, unitPrice: i.unitPrice, gstRate: db.products.find((p) => p.id === i.productId)?.gstRate ?? 0, lineTotal: i.lineTotal })),
    subTotal: g.subTotal, shippingAmount: g.shippingAmount ?? 0, taxAmount: g.taxAmount, total: g.total, commissionRate: db.settings.commissionDefaultRate,
    commissionAmount: Math.round((g.subTotal * db.settings.commissionDefaultRate) / 100), history: [{ status: 'PendingAcceptance', note: null, changedAt: now }],
    shipment: null, manufacturerId: g.manufacturerId, retailerId: b.id,
  }))
  const order: DbOrder = {
    id: orderId, orderNumber: number, placedAt: now, subTotal: cart.subTotal, shippingTotal: cart.shippingTotal ?? 0, taxTotal: cart.taxTotal,
    grandTotal: cart.grandTotal, paymentStatus: 'Pending', deliveryAddress: addr, retailerId: b.id, sellerOrders,
  }
  void u
  db.orders.unshift(order)
  db.carts[b.id] = []
  return publicOrder(order)
})
on('GET', 'orders', ({ db }) => {
  const b = requireApproved(db, 'Retailer')
  return db.orders.filter((o) => o.retailerId === b.id).sort((a, c) => c.placedAt.localeCompare(a.placedAt)).map(publicOrder)
})
const ownOrder = (db: Db, id: number) => {
  const b = requireApproved(db, 'Retailer')
  return db.orders.find((o) => o.id === id && o.retailerId === b.id) ?? fail(404, 'Order not found', 'ORDER_NOT_FOUND')
}
on('GET', 'orders/:id', ({ db, params }) => publicOrder(ownOrder(db, Number(params[0]))))
on('POST', 'orders/:id/confirm-delivery', ({ db, params }) => {
  const o = ownOrder(db, Number(params[0]))
  for (const so of o.sellerOrders) if (so.status === 'ReachedKashmir' || so.status === 'Dispatched') pushStatus(so, 'Delivered', 'Confirmed by shop')
  return publicOrder(o)
})
on('GET', 'orders/:id/rating', ({ db, params }) => {
  const o = ownOrder(db, Number(params[0]))
  return db.ratings?.[o.id] ?? fail(404, 'Not rated yet', 'not_found')
})
on('POST', 'orders/:id/rating', ({ db, params, body }) => {
  const o = ownOrder(db, Number(params[0]))
  const { stars, comment } = body as { stars: number; comment?: string }
  if (!Number.isInteger(stars) || stars < 1 || stars > 5) fail(400, 'Stars must be 1 to 5', 'validation_failed')
  if (!o.sellerOrders.some((s) => s.status === 'Delivered')) fail(409, 'You can rate an order after it is delivered.', 'order_not_delivered')
  db.ratings ??= {}
  db.ratings[o.id] = { orderId: o.id, stars, comment: comment?.trim() || null, updatedAt: new Date().toISOString() }
  return db.ratings[o.id]
})
on('POST', 'payments', ({ db, body }) => {
  // Test switch: localStorage tc_mock_payfail=1 makes payments fail, to see the failed screen.
  if (typeof localStorage !== 'undefined' && localStorage.getItem('tc_mock_payfail') === '1') fail(402, 'Payment declined', 'PAYMENT_FAILED')
  const o = ownOrder(db, (body as { orderId: number }).orderId)
  o.paymentStatus = 'Paid'
  for (const so of o.sellerOrders) so.paymentStatus = 'Paid'
  return { orderId: o.id, gatewayOrderId: `order_mock_${o.id}`, amount: o.grandTotal, currency: 'INR', keyId: null } satisfies PaymentIntent
})

// Seller orders
on('GET', 'seller/orders', ({ db }) => {
  const b = requireApproved(db, 'Manufacturer')
  return db.orders.flatMap((o) => o.sellerOrders).filter((s) => s.manufacturerId === b.id).sort((a, c) => c.placedAt.localeCompare(a.placedAt)).map(publicSeller)
})
const sellerAction = (fn: (so: DbSellerOrder, body: Record<string, string>, db: Db) => void): Handler => ({ db, params, body }) => {
  const b = requireApproved(db, 'Manufacturer')
  const so = findSellerOrder(db, Number(params[0]), b.id)
  fn(so, (body ?? {}) as Record<string, string>, db)
  return publicSeller(so)
}
on('GET', 'seller/orders/:id', ({ db, params }) => publicSeller(findSellerOrder(db, Number(params[0]), requireApproved(db, 'Manufacturer').id)))
on('POST', 'seller/orders/:id/accept', sellerAction((so) => {
  if (so.status !== 'PendingAcceptance') fail(409, 'Order is not waiting for acceptance', 'ORDER_STATE')
  if (so.paymentStatus !== 'Paid') fail(409, 'The shopkeeper has not paid yet', 'NOT_PAID')
  pushStatus(so, 'Accepted')
}))
on('POST', 'seller/orders/:id/reject', sellerAction((so, body) => {
  if (so.status !== 'PendingAcceptance') fail(409, 'Order is not waiting for acceptance', 'ORDER_STATE')
  if (!body.reason?.trim()) fail(422, 'Give a reason', 'REASON_REQUIRED')
  so.rejectReason = body.reason
  if (so.paymentStatus === 'Paid') so.paymentStatus = 'Refunded'
  pushStatus(so, 'Rejected', body.reason)
}))
on('POST', 'seller/orders/:id/dispatch', sellerAction((so, body) => {
  if (so.status !== 'Accepted') fail(409, 'Accept the order first', 'ORDER_STATE')
  if (!body.lrNumber?.trim()) fail(422, 'LR number is required', 'LR_REQUIRED')
  so.shipment = { carrierName: body.carrierName ?? '', lrNumber: body.lrNumber, dispatchedAt: new Date().toISOString() }
  pushStatus(so, 'Dispatched')
}))
on('POST', 'seller/orders/:id/status', sellerAction((so, body) => {
  const next = body.status as SellerOrder['status']
  const ok = (next === 'ReachedKashmir' && so.status === 'Dispatched') || (next === 'Delivered' && ['Dispatched', 'ReachedKashmir'].includes(so.status))
  if (!ok) fail(409, 'This status change is not allowed', 'STATUS_NOT_ALLOWED')
  pushStatus(so, next)
}))

// Admin
// Admin dashboard (mock). The real one is computed on the server.
on('GET', 'admin/dashboard', ({ db, query }) => {
  requireAdmin(db)
  const days = Number(query.days) || 1
  const since = Date.now() - days * 86_400_000
  const sos = db.orders.flatMap((o) => o.sellerOrders.map((s) => ({ o, s }))).filter(({ o }) => new Date(o.placedAt).getTime() >= since)
  const pending = db.businesses.filter((b) => b.verificationStatus === 'Pending')
  return {
    days,
    gmv: sos.reduce((a, { s }) => a + s.total, 0),
    orders: sos.length,
    commission: sos.reduce((a, { s }) => a + s.commissionAmount, 0),
    newBusinesses: db.businesses.filter((b) => new Date(b.createdAt).getTime() >= since).length,
    pendingApprovals: pending.length,
    decisions: pending.slice(0, 5).map((b) => ({ kind: 'business' as const, title: b.displayName, sub: `${b.type === 'Manufacturer' ? 'Manufacturer' : 'Shopkeeper'} · ${b.documents.length} documents`, link: `/admin/businesses/${b.id}` })),
  } satisfies import('../types').AdminDashboard
})

on('GET', 'admin/businesses', ({ db, query }) => {
  requireAdmin(db)
  const s = query.status as VerificationStatus | undefined
  return db.businesses.filter((b) => !s || b.verificationStatus === s).sort((a, c) => c.createdAt.localeCompare(a.createdAt)).map(adminView)
})
const review = (status: 'Approved' | 'Rejected'): Handler => ({ db, params, body }) => {
  requireAdmin(db)
  const b = db.businesses.find((x) => x.id === Number(params[0])) ?? fail(404, 'Business not found', 'BUSINESS_NOT_FOUND')
  const note = (body as { note?: string } | undefined)?.note
  if (status === 'Rejected' && !note?.trim()) fail(422, 'A note is required', 'NOTE_REQUIRED')
  b.verificationStatus = status
  b.rejectionNote = status === 'Rejected' ? note! : null
  b.documents.forEach((d) => { if (d.status === 'Pending') d.status = status === 'Approved' ? 'Accepted' : 'Rejected' })
  return adminView(b)
}
on('POST', 'admin/businesses/:id/approve', review('Approved'))
on('POST', 'admin/businesses/:id/reject', review('Rejected'))
on('GET', 'admin/settings', ({ db }) => (requireAdmin(db), clone(db.settings)))
on('PUT', 'admin/settings', ({ db, body }) => {
  requireAdmin(db)
  const s = body as Db['settings']
  if (s.commissionDefaultRate < 0 || s.commissionDefaultRate > 50) fail(422, 'Commission must be between 0 and 50', 'COMMISSION_RANGE')
  if (s.settlementAutoReleaseDays < 1) fail(422, 'Days must be at least 1', 'DAYS_RANGE')
  db.settings = { ...s }
  return clone(db.settings)
})
on('GET', 'admin/shipping-rates', ({ db }) => (requireAdmin(db), clone(db.rates)))
on('POST', 'admin/shipping-rates', ({ db, body }) => {
  requireAdmin(db)
  const r = { ...(body as ShippingRateInput), id: nextId() }
  db.rates.push(r)
  return r
})
on('PUT', 'admin/shipping-rates/:id', ({ db, params, body }) => {
  requireAdmin(db)
  const r = db.rates.find((x) => x.id === Number(params[0])) ?? fail(404, 'Rate not found', 'RATE_NOT_FOUND')
  Object.assign(r, body)
  return clone(r)
})
on('DELETE', 'admin/shipping-rates/:id', ({ db, params }) => {
  requireAdmin(db)
  db.rates = db.rates.filter((x) => x.id !== Number(params[0]))
})

// ---------- Notifications and problem reports (mock; shapes are a backend request)
const ago = (h: number) => new Date(Date.now() - h * 3_600_000).toISOString()
function seedNotes(u: DbUser): NotificationItem[] {
  if (u.isAdmin) return [{ id: nextId(), type: 'account', title: '2 businesses are waiting for approval', body: 'Open Approvals to review their documents.', createdAt: ago(1), isRead: false, link: '/admin/businesses' }]
  const b = u.businessId
  if (b === 101) return [
    { id: nextId(), type: 'order', title: 'New order TC-241001', body: '100 Merino Wool Shawl, Plain. Reply within 24 hours.', createdAt: ago(2), isRead: false, link: '/seller/orders' },
    { id: nextId(), type: 'payment', title: 'Payment received for TC-241002', body: 'You can accept the order now.', createdAt: ago(30), isRead: true, link: '/seller/orders' },
  ]
  return [
    { id: nextId(), type: 'order', title: 'Your order TC-241003 is on the way', body: 'Kapoor Knits shipped 40 pieces.', createdAt: ago(3), isRead: false, link: '/shop/orders' },
    { id: nextId(), type: 'order', title: 'Seller accepted order TC-241002', body: 'They are packing 120 pieces for you.', createdAt: ago(5), isRead: false, link: '/shop/orders' },
    { id: nextId(), type: 'system', title: 'Welcome to Textile Connect', body: 'Prices you see are landed in your city: factory price, delivery and GST.', createdAt: ago(80), isRead: true, link: null },
  ]
}
function notesFor(db: Db): NotificationItem[] {
  const u = currentUser(db)
  db.notifications ??= {}
  db.notifications[u.phone] ??= seedNotes(u)
  return db.notifications[u.phone]
}
on('GET', 'notifications', ({ db }) => clone([...notesFor(db)].sort((a, b) => b.createdAt.localeCompare(a.createdAt))))
on('GET', 'notifications/unread-count', ({ db }) => ({ count: notesFor(db).filter((n) => !n.isRead).length }))
on('POST', 'notifications/read-all', ({ db }) => {
  notesFor(db).forEach((n) => (n.isRead = true))
})
on('POST', 'notifications/:id/read', ({ db, params }) => {
  const n = notesFor(db).find((x) => x.id === Number(params[0]))
  if (n) n.isRead = true
})
on('POST', 'seller-orders/:id/claims', ({ db, params, body }) => {
  const b = requireApproved(db, 'Retailer')
  const o = db.orders.find((x) => x.sellerOrders.some((s) => s.id === Number(params[0])) && x.retailerId === b.id) ?? fail(404, 'Order not found', 'ORDER_NOT_FOUND')
  const so = o.sellerOrders.find((s) => s.id === Number(params[0]))!
  if (so.status !== 'ReachedKashmir' && so.status !== 'Delivered') fail(409, 'Order is not delivered', 'ORDER_STATE')
  const input = body as { reason: Claim['reason']; pieces: number; note?: string }
  const claim = { id: nextId(), sellerOrderId: so.id, reason: input.reason, pieces: input.pieces, note: input.note, status: 'Open' as const, createdAt: new Date().toISOString() }
  ;(db.claims ??= []).push(claim)
  return clone(claim)
})
on('POST', 'claims/:id/photos', () => undefined)

// ---------- Saved products and storefront (mock; real shapes are in docs/API_REFERENCE.md)
on('GET', 'saved-products', ({ db }) => {
  const b = requireApproved(db, 'Retailer')
  const rows = (db.saved?.[b.id] ?? [])
    .map((id) => db.products.find((p) => p.id === id))
    .filter((p): p is DbProduct => !!p)
    .map((p) => ({ id: p.id, slug: p.slug, title: p.title, sellerName: p.sellerName, moq: p.moq, priceFrom: summary(p).priceFrom || null, available: p.status === 'Active' }))
  return { items: rows, total: rows.length, page: 1, pageSize: 50 }
})
on('PUT', 'saved-products/:id', ({ db, params }) => {
  const b = requireApproved(db, 'Retailer')
  const id = Number(params[0])
  if (!db.products.some((p) => p.id === id)) fail(404, 'Product not found', 'PRODUCT_NOT_FOUND')
  db.saved ??= {}
  db.saved[b.id] = [id, ...(db.saved[b.id] ?? []).filter((x) => x !== id)]
  return { saved: true }
})
on('DELETE', 'saved-products/:id', ({ db, params }) => {
  const b = requireApproved(db, 'Retailer')
  db.saved ??= {}
  db.saved[b.id] = (db.saved[b.id] ?? []).filter((x) => x !== Number(params[0]))
})
on('GET', 'storefronts/:id', ({ db, params }) => {
  const seller = db.businesses.find((x) => x.id === Number(params[0]) && x.type === 'Manufacturer' && x.verificationStatus === 'Approved') ?? fail(404, 'Seller not found', 'NOT_FOUND')
  const products = db.products.filter((p) => p.sellerBusinessId === seller.id && p.status === 'Active').map(summary)
  return { business: { id: seller.id, displayName: seller.displayName, email: null, city: seller.addresses[0]?.cityName ?? null }, rating: 0, reviewCount: 0, products }
})

// ---------- RFQ and bulk upload (mock; real shapes are in docs/RFQ.md and BulkProductEndpoints.cs)
on('GET', 'features', () => ({ Rfq: true }))

const DAY = 86_400_000
function rfqsOf(db: Db): DbRfq[] {
  db.rfqs ??= [
    {
      id: nextId(), retailerId: 201, title: 'School sweaters, navy blue', description: 'Round neck, full sleeve, sizes 28 to 40. Need the first lot before school reopens.',
      categoryId: db.categories[0]?.id, quantity: 500, unit: 'Piece', targetPricePerUnit: 240, pincode: '190001', city: 'Srinagar', state: 'Jammu & Kashmir',
      expiresAt: new Date(Date.now() + 9 * DAY).toISOString(), status: 'Open', createdAt: new Date(Date.now() - DAY).toISOString(),
      quotes: [{ id: nextId(), sellerBusinessId: 101, pricePerUnit: 228, quantity: 500, leadTimeDays: 12, note: 'Acrylic wool blend. Free delivery to the transporter.', status: 'Submitted', createdAt: new Date(Date.now() - 6 * 3_600_000).toISOString() }],
    },
  ]
  return db.rfqs
}
const rfqStatusNow = (r: DbRfq): RfqStatus => (r.status === 'Open' && new Date(r.expiresAt).getTime() < Date.now() ? 'Expired' : r.status)
const sellerOf = (db: Db, id: number) => db.businesses.find((b) => b.id === id)
const rfqSummary = (db: Db, r: DbRfq, viewerSellerId?: number): RfqSummary => ({
  id: r.id, title: r.title, quantity: r.quantity, unit: r.unit, status: rfqStatusNow(r), expiresAt: r.expiresAt,
  quotationCount: r.quotes.filter((q) => q.status !== 'Withdrawn').length, deliveryCity: r.city, deliveryState: r.state, createdAt: r.createdAt,
  categoryId: r.categoryId ?? null, categoryName: db.categories.find((c) => c.id === r.categoryId)?.name ?? null, targetPricePerUnit: r.targetPricePerUnit ?? null,
  iHaveQuoted: viewerSellerId ? r.quotes.some((q) => q.sellerBusinessId === viewerSellerId && q.status !== 'Withdrawn') : null,
})
const quotationView = (db: Db, q: DbQuotation, viewerSellerId?: number): Quotation => {
  const s = sellerOf(db, q.sellerBusinessId)
  return {
    id: q.id, sellerBusinessId: q.sellerBusinessId, sellerName: s?.displayName ?? '', pricePerUnit: q.pricePerUnit, quantity: q.quantity, total: q.pricePerUnit * q.quantity,
    leadTimeDays: q.leadTimeDays, note: q.note ?? null, status: q.status, createdAt: q.createdAt, isMine: q.sellerBusinessId === viewerSellerId,
    contact: q.status === 'Accepted' && s ? { name: s.displayName, phone: s.phone } : null,
  }
}
const retailerRfqDetail = (db: Db, r: DbRfq): RfqDetail => ({
  summary: rfqSummary(db, r), description: r.description ?? null, neededBy: r.neededBy ?? null,
  quotations: r.quotes.filter((q) => q.status !== 'Withdrawn').sort((a, b) => a.pricePerUnit - b.pricePerUnit).map((q) => quotationView(db, q)), buyerContact: null,
})
const sellerRfqDetail = (db: Db, r: DbRfq, sellerId: number): RfqDetail => {
  const mine = r.quotes.filter((q) => q.sellerBusinessId === sellerId)
  const won = mine.some((q) => q.status === 'Accepted')
  const buyer = db.businesses.find((b) => b.id === r.retailerId)
  return {
    summary: rfqSummary(db, r, sellerId), description: r.description ?? null, neededBy: r.neededBy ?? null,
    quotations: mine.map((q) => quotationView(db, q, sellerId)), buyerContact: won && buyer ? { name: buyer.displayName, phone: buyer.phone } : null,
  }
}
const paged = <T,>(items: T[]) => ({ items, total: items.length, page: 1, pageSize: 50 })
const myRfq = (db: Db, id: number): DbRfq => {
  const b = requireApproved(db, 'Retailer')
  return rfqsOf(db).find((r) => r.id === id && r.retailerId === b.id) ?? fail(404, 'Request not found', 'not_found')
}
const openRfq = (db: Db, id: number): DbRfq => {
  const r = rfqsOf(db).find((x) => x.id === id) ?? fail(404, 'Request not found', 'not_found')
  if (rfqStatusNow(r) !== 'Open') fail(409, 'This request is not open any more', 'rfq_not_open')
  return r
}

on('POST', 'rfqs', ({ db, body }) => {
  const b = requireApproved(db, 'Retailer')
  const input = body as RfqInput
  const fields: Record<string, string[]> = {}
  if (!input.title?.trim()) fields.title = ['required']
  if (!(input.quantity > 0)) fields.quantity = ['invalid_value']
  if (Object.keys(fields).length) throw new ApiError(400, 'Check the form', fields, 'validation_failed')
  if (!/^\d{6}$/.test(String(input.deliveryPincode ?? ''))) fail(400, 'Enter a 6 digit pincode', 'pincode_invalid')
  const place = db.businesses.flatMap((x) => x.addresses).find((a) => a.pincode === input.deliveryPincode)
  if (!place) fail(400, 'We do not know this pincode', 'pincode_unknown')
  if (rfqsOf(db).filter((r) => r.retailerId === b.id && rfqStatusNow(r) === 'Open').length >= 10) fail(409, 'You have 10 open requests', 'rfq_limit_reached')
  const r: DbRfq = {
    id: nextId(), retailerId: b.id, title: input.title.trim(), description: input.description?.trim() || undefined, categoryId: input.categoryId,
    quantity: input.quantity, unit: input.unit || 'Piece', targetPricePerUnit: input.targetPricePerUnit, pincode: input.deliveryPincode,
    city: place!.cityName, state: 'Jammu & Kashmir', neededBy: input.neededBy, expiresAt: new Date(Date.now() + (input.expiresInDays ?? 14) * DAY).toISOString(),
    status: 'Open', quotes: [], createdAt: new Date().toISOString(),
  }
  rfqsOf(db).unshift(r)
  return retailerRfqDetail(db, r)
})
on('GET', 'rfqs', ({ db, query }) => {
  const b = requireApproved(db, 'Retailer')
  const list = rfqsOf(db).filter((r) => r.retailerId === b.id && (!query.status || rfqStatusNow(r) === query.status)).map((r) => rfqSummary(db, r))
  return paged(list)
})
on('GET', 'rfqs/:id', ({ db, params }) => retailerRfqDetail(db, myRfq(db, Number(params[0]))))
on('POST', 'rfqs/:id/cancel', ({ db, params }) => {
  const r = myRfq(db, Number(params[0]))
  if (rfqStatusNow(r) !== 'Open') fail(409, 'This request is not open any more', 'rfq_not_open')
  r.status = 'Cancelled'
  r.quotes.forEach((q) => q.status !== 'Withdrawn' && (q.status = 'Rejected'))
  return retailerRfqDetail(db, r)
})
on('POST', 'rfqs/:id/quotations/:id/accept', ({ db, params }) => {
  const r = myRfq(db, Number(params[0]))
  if (rfqStatusNow(r) !== 'Open') fail(409, 'This request is not open any more', 'rfq_not_open')
  const win = r.quotes.find((q) => q.id === Number(params[1]) && q.status === 'Submitted') ?? fail(409, 'This quotation is not available', 'quotation_not_available')
  r.quotes.forEach((q) => q.status !== 'Withdrawn' && (q.status = q === win ? 'Accepted' : 'Rejected'))
  r.status = 'Awarded'
  return retailerRfqDetail(db, r)
})

on('GET', 'seller/rfqs', ({ db, query }) => {
  const b = requireApproved(db, 'Manufacturer')
  const term = (query.q ?? '').toLowerCase()
  const list = rfqsOf(db)
    .filter((r) => rfqStatusNow(r) === 'Open' && (!query.categoryId || r.categoryId === Number(query.categoryId)) && (!term || r.title.toLowerCase().includes(term)))
    .map((r) => rfqSummary(db, r, b.id))
  return paged(list)
})
on('GET', 'seller/rfqs/:id', ({ db, params }) => {
  const b = requireApproved(db, 'Manufacturer')
  const r = rfqsOf(db).find((x) => x.id === Number(params[0])) ?? fail(404, 'Request not found', 'not_found')
  return sellerRfqDetail(db, r, b.id)
})
on('PUT', 'seller/rfqs/:id/quotation', ({ db, params, body }) => {
  const b = requireApproved(db, 'Manufacturer')
  const r = openRfq(db, Number(params[0]))
  const input = body as QuotationInput
  const fields: Record<string, string[]> = {}
  if (!(input.pricePerUnit > 0)) fields.pricePerUnit = ['invalid_value']
  if (!(input.quantity > 0) || input.quantity > r.quantity) fields.quantity = ['invalid_value']
  if (!(input.leadTimeDays >= 1 && input.leadTimeDays <= 365)) fields.leadTimeDays = ['invalid_value']
  if (Object.keys(fields).length) throw new ApiError(400, 'Check the form', fields, 'validation_failed')
  const mine = r.quotes.find((q) => q.sellerBusinessId === b.id)
  if (mine && (mine.status === 'Accepted' || mine.status === 'Rejected')) fail(409, 'This quotation is final', 'quotation_final')
  if (mine) Object.assign(mine, { pricePerUnit: input.pricePerUnit, quantity: input.quantity, leadTimeDays: input.leadTimeDays, note: input.note?.trim() || undefined, status: 'Submitted' })
  else r.quotes.push({ id: nextId(), sellerBusinessId: b.id, pricePerUnit: input.pricePerUnit, quantity: input.quantity, leadTimeDays: input.leadTimeDays, note: input.note?.trim() || undefined, status: 'Submitted', createdAt: new Date().toISOString() })
  return sellerRfqDetail(db, r, b.id)
})
on('DELETE', 'seller/rfqs/:id/quotation', ({ db, params }) => {
  const b = requireApproved(db, 'Manufacturer')
  const r = openRfq(db, Number(params[0]))
  const mine = r.quotes.find((q) => q.sellerBusinessId === b.id && q.status === 'Submitted') ?? fail(409, 'There is no quotation to withdraw', 'quotation_not_available')
  mine.status = 'Withdrawn'
  return sellerRfqDetail(db, r, b.id)
})
on('GET', 'seller/quotations', ({ db, query }) => {
  const b = requireApproved(db, 'Manufacturer')
  const rows: MyQuotationRow[] = rfqsOf(db)
    .flatMap((r) => r.quotes.filter((q) => q.sellerBusinessId === b.id && q.status !== 'Withdrawn' && (!query.status || q.status === query.status)).map((q) => ({ rfq: rfqSummary(db, r, b.id), quotation: quotationView(db, q, b.id) })))
  return paged(rows)
})

// Bulk product upload: reads the CSV text and checks each row the way the server does (columns in docs: categoryId,title,slug,description,gstRate,moq,unit,status).
async function bulkParse(db: Db, form?: FormData) {
  const file = form?.get('file')
  if (!(file instanceof File)) fail(400, 'Choose a CSV file', 'validation_failed')
  const lines = (await (file as File).text()).split(/\r?\n/).filter((l) => l.trim())
  const rows: Array<{ categoryId: number; title: string; slug: string; description: string | null; gstRate: number; moq: number; unit: string; status: string }> = []
  const errors: string[] = []
  lines.slice(1).forEach((line, i) => {
    const [categoryId, title, slug, description, gstRate, moq, unit, status] = line.split(',').map((c) => c.trim())
    const n = i + 2
    if (!db.categories.some((c) => c.id === Number(categoryId))) errors.push(`Row ${n}: category ${categoryId || '(empty)'} does not exist`)
    if (!title) errors.push(`Row ${n}: title is empty`)
    if (!(Number(moq) >= 1)) errors.push(`Row ${n}: minimum order must be 1 or more`)
    rows.push({ categoryId: Number(categoryId), title, slug: slug || title.toLowerCase().replace(/[^a-z0-9]+/g, '-'), description: description || null, gstRate: Number(gstRate) || 0, moq: Number(moq) || 0, unit: unit || 'Piece', status: status || 'Draft' })
  })
  if (rows.length === 0) errors.push('The file has no product rows')
  return { rows, errors }
}
on('POST', 'seller/products/bulk/preview', ({ db, form }) => {
  requireApproved(db, 'Manufacturer')
  return bulkParse(db, form)
})
on('POST', 'seller/products/bulk/import', async ({ db, form }) => {
  requireApproved(db, 'Manufacturer')
  const parsed = await bulkParse(db, form)
  if (parsed.errors.length) fail(400, parsed.errors[0], 'validation_failed')
  return { imported: parsed.rows.length }
})

// ---------- entry
/** Same Idempotency-Key gives the same answer, like the real server: a double tap never places two orders. */
const idem = new Map<string, unknown>()

export async function mockRequest<T>(method: string, path: string, opts: { query?: object; body?: unknown; form?: FormData; idempotencyKey?: string }): Promise<T> {
  await wait(220)
  if (opts.idempotencyKey && idem.has(opts.idempotencyKey)) return clone(idem.get(opts.idempotencyKey)) as T
  const db = getDb()
  const clean = path.replace(/^\//, '')
  for (const [m, re, handler] of routes) {
    if (m !== method) continue
    const match = re.exec(clean)
    if (!match) continue
    const query: Record<string, string> = {}
    for (const [k, v] of Object.entries(opts.query ?? {})) if (v !== undefined && v !== null && v !== '') query[k] = String(v)
    const result = await handler({ db, query, body: opts.body ?? {}, form: opts.form, params: match.slice(1) })
    if (method !== 'GET') saveDb()
    if (opts.idempotencyKey) idem.set(opts.idempotencyKey, result)
    return (result === undefined ? undefined : clone(result)) as T
  }
  throw new ApiError(404, `Mock: no route for ${method} ${path}`)
}
