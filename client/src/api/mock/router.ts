import { ApiError } from '../errors'
import type {
  AdminBusiness, AddressInput, Cart, CartGroup, CartItem, CreateBusinessInput, LandedCost, Me, Order, PaymentIntent, PriceSlab,
  ProductDetail, ProductInput, ProductQuery, ProductSummary, SellerOrder, ShippingRateInput, VerificationStatus,
} from '../types'
import {
  type Db, type DbBusiness, type DbOrder, type DbProduct, type DbSellerOrder, type DbUser, getDb, getSession, makeAddress, nextId, saveDb, setSession, swatch,
} from './db'

interface Ctx {
  query: Record<string, string>
  body: any // eslint-disable-line @typescript-eslint/no-explicit-any
  form?: FormData
  params: string[]
  db: Db
}
type Handler = (ctx: Ctx) => unknown | Promise<unknown>

const fail = (status: number, msg: string): never => {
  throw new ApiError(status, msg)
}
const clone = <T,>(v: T): T => JSON.parse(JSON.stringify(v)) as T
const wait = (ms: number) => new Promise((r) => setTimeout(r, ms))

// ---------- session helpers
function currentUser(db: Db): DbUser {
  const phone = getSession()
  const u = phone ? db.users.find((x) => x.phone === phone) : undefined
  return u ?? fail(401, 'Please log in')
}
function currentBusiness(db: Db): DbBusiness {
  const u = currentUser(db)
  return db.businesses.find((b) => b.id === u.businessId) ?? fail(403, 'No business')
}
function requireApproved(db: Db, type: 'Retailer' | 'Manufacturer'): DbBusiness {
  const b = currentBusiness(db)
  if (b.type !== type) fail(403, 'Not allowed for this account')
  if (b.verificationStatus !== 'Approved') fail(403, 'Business is not approved yet')
  return b
}
function requireAdmin(db: Db) {
  if (!currentUser(db).isAdmin) fail(403, 'Admin only')
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
  const p = db.products.find((x) => x.id === productId) ?? fail(404, 'Product not found')
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
  if (!slabs.length) fail(422, 'Add at least one price slab')
  const s = [...slabs].sort((a, b) => a.minQty - b.minQty)
  if (s[0].minQty !== moq) fail(422, 'First slab must start at the MOQ')
  s.forEach((x, i) => {
    if (x.maxQty !== null && x.maxQty < x.minQty) fail(422, 'Slab max must be above min')
    if (i < s.length - 1 && (x.maxQty === null || x.maxQty + 1 !== s[i + 1].minQty)) fail(422, 'Price slabs overlap or leave a gap')
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
  return fail(404, 'Order not found')
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
  if (!/^\d{10}$/.test(String(body.phoneNumber))) fail(400, 'Enter a valid mobile number')
})
on('POST', 'auth/otp/verify', ({ body, db }) => {
  if (body.code !== '123456') fail(400, 'Wrong code. In demo mode use 123456.')
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
on('POST', 'businesses', ({ body, db }) => {
  const u = currentUser(db)
  if (u.businessId) fail(409, 'You already have a business')
  const input = body as CreateBusinessInput
  if (db.businesses.some((b) => b.gstin && b.gstin === input.gstin)) fail(409, 'This GST number is already registered')
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
  const p = db.products.find((x) => x.slug === params[0] && x.status === 'Active') ?? fail(404, 'Product not found')
  return detail(p)
})
on('GET', 'landed-cost', ({ db, query }) => {
  requireApproved(db, 'Retailer')
  return landedCost(db, Number(query.productId), Math.max(1, Number(query.qty)), Number(query.cityId))
})

// Seller products
on('GET', 'seller/products', ({ db }) => {
  const b = requireApproved(db, 'Manufacturer')
  return db.products.filter((p) => p.sellerBusinessId === b.id).map(summary)
})
const ownProduct = (db: Db, id: number) => {
  const b = requireApproved(db, 'Manufacturer')
  return db.products.find((p) => p.id === id && p.sellerBusinessId === b.id) ?? fail(404, 'Product not found')
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
  const p = db.products.find((x) => x.variants.some((v) => v.id === variantId)) ?? fail(404, 'Item not found')
  if (p.status !== 'Active') fail(422, 'This product is not available')
  const items = (db.carts[b.id] ??= [])
  const ex = items.find((i) => i.variantId === variantId)
  if (ex) ex.quantity += quantity
  else items.push({ id: nextId(), variantId, quantity })
  return buildCart(db, b)
})
on('PUT', 'cart/items/:id', ({ db, params, body }) => {
  const b = requireApproved(db, 'Retailer')
  const it = (db.carts[b.id] ?? []).find((i) => i.id === Number(params[0])) ?? fail(404, 'Item not found')
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
  if (!cart.groups.length) fail(422, 'Cart is empty')
  const addr = b.addresses.find((a) => a.id === (body as { deliveryAddressId: number }).deliveryAddressId) ?? fail(422, 'Choose a delivery address')
  const totals = new Map<number, number>()
  for (const g of cart.groups) for (const i of g.items) totals.set(i.productId, (totals.get(i.productId) ?? 0) + i.quantity)
  for (const g of cart.groups) for (const i of g.items) if ((totals.get(i.productId) ?? 0) < i.moq) fail(422, `Minimum order for ${i.title} is ${i.moq}`)
  const orderId = nextId()
  const number = `TC-${new Date().getFullYear() % 100}${orderId}`
  const now = new Date().toISOString()
  const sellerOrders: DbSellerOrder[] = cart.groups.map((g, idx) => ({
    id: nextId(), orderId, orderNumber: number, subOrderNumber: `${number}-${String.fromCharCode(65 + idx)}`, manufacturerName: g.manufacturerName,
    retailerName: b.displayName, deliveryCity: addr.cityName, status: 'PendingAcceptance', paymentStatus: 'Pending', placedAt: now,
    items: g.items.map((i) => ({ id: nextId(), productId: i.productId, title: i.title, size: i.size, colour: i.colour, quantity: i.quantity, unitPrice: i.unitPrice, gstRate: db.products.find((p) => p.id === i.productId)?.gstRate ?? 0, lineTotal: i.lineTotal })),
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
  return db.orders.find((o) => o.id === id && o.retailerId === b.id) ?? fail(404, 'Order not found')
}
on('GET', 'orders/:id', ({ db, params }) => publicOrder(ownOrder(db, Number(params[0]))))
on('POST', 'orders/:id/confirm-delivery', ({ db, params }) => {
  const o = ownOrder(db, Number(params[0]))
  for (const so of o.sellerOrders) if (so.status === 'ReachedKashmir' || so.status === 'Dispatched') pushStatus(so, 'Delivered', 'Confirmed by shop')
  return publicOrder(o)
})
on('POST', 'payments', ({ db, body }) => {
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
  if (so.status !== 'PendingAcceptance') fail(409, 'Order is not waiting for acceptance')
  if (so.paymentStatus !== 'Paid') fail(409, 'The shopkeeper has not paid yet')
  pushStatus(so, 'Accepted')
}))
on('POST', 'seller/orders/:id/reject', sellerAction((so, body) => {
  if (so.status !== 'PendingAcceptance') fail(409, 'Order is not waiting for acceptance')
  if (!body.reason?.trim()) fail(422, 'Give a reason')
  so.rejectReason = body.reason
  if (so.paymentStatus === 'Paid') so.paymentStatus = 'Refunded'
  pushStatus(so, 'Rejected', body.reason)
}))
on('POST', 'seller/orders/:id/dispatch', sellerAction((so, body) => {
  if (so.status !== 'Accepted') fail(409, 'Accept the order first')
  if (!body.lrNumber?.trim()) fail(422, 'LR number is required')
  so.shipment = { carrierName: body.carrierName ?? '', lrNumber: body.lrNumber, dispatchedAt: new Date().toISOString() }
  pushStatus(so, 'Dispatched')
}))
on('POST', 'seller/orders/:id/status', sellerAction((so, body) => {
  const next = body.status as SellerOrder['status']
  const ok = (next === 'ReachedKashmir' && so.status === 'Dispatched') || (next === 'Delivered' && ['Dispatched', 'ReachedKashmir'].includes(so.status))
  if (!ok) fail(409, 'This status change is not allowed')
  pushStatus(so, next)
}))

// Admin
on('GET', 'admin/businesses', ({ db, query }) => {
  requireAdmin(db)
  const s = query.status as VerificationStatus | undefined
  return db.businesses.filter((b) => !s || b.verificationStatus === s).sort((a, c) => c.createdAt.localeCompare(a.createdAt)).map(adminView)
})
const review = (status: 'Approved' | 'Rejected'): Handler => ({ db, params, body }) => {
  requireAdmin(db)
  const b = db.businesses.find((x) => x.id === Number(params[0])) ?? fail(404, 'Business not found')
  const note = (body as { note?: string } | undefined)?.note
  if (status === 'Rejected' && !note?.trim()) fail(422, 'A note is required')
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
  if (s.commissionDefaultRate < 0 || s.commissionDefaultRate > 50) fail(422, 'Commission must be between 0 and 50')
  if (s.settlementAutoReleaseDays < 1) fail(422, 'Days must be at least 1')
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
  const r = db.rates.find((x) => x.id === Number(params[0])) ?? fail(404, 'Rate not found')
  Object.assign(r, body)
  return clone(r)
})
on('DELETE', 'admin/shipping-rates/:id', ({ db, params }) => {
  requireAdmin(db)
  db.rates = db.rates.filter((x) => x.id !== Number(params[0]))
})

// ---------- entry
export async function mockRequest<T>(method: string, path: string, opts: { query?: object; body?: unknown; form?: FormData }): Promise<T> {
  await wait(220)
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
    return (result === undefined ? undefined : clone(result)) as T
  }
  throw new ApiError(404, `Mock: no route for ${method} ${path}`)
}
