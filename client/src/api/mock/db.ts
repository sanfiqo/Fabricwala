import type {
  BusinessAddress, BusinessDetail, Category, City, Order, PlatformSettings, PriceSlab, ProductDetail,
  SellerOrder, ShippingRate, State, Unit, Variant,
} from '../types'

/** Fabric-like placeholder image (no network needed, works offline). */
export function swatch(a: string, b: string, angle = 45): string {
  const svg =
    `<svg xmlns='http://www.w3.org/2000/svg' width='600' height='600'>` +
    `<defs><pattern id='p' width='24' height='24' patternUnits='userSpaceOnUse' patternTransform='rotate(${angle})'>` +
    `<rect width='24' height='24' fill='${a}'/><rect width='12' height='24' fill='${b}' opacity='.55'/>` +
    `<rect y='10' width='24' height='3' fill='#fff' opacity='.18'/></pattern></defs>` +
    `<rect width='600' height='600' fill='url(#p)'/></svg>`
  return `data:image/svg+xml;utf8,${encodeURIComponent(svg)}`
}

export interface DbProduct extends ProductDetail {
  createdAt: string
}
export interface DbSellerOrder extends SellerOrder {
  manufacturerId: number
  retailerId: number
}
export interface DbOrder extends Omit<Order, 'sellerOrders'> {
  retailerId: number
  sellerOrders: DbSellerOrder[]
}
export interface DbBusiness extends BusinessDetail {
  legalName: string
  createdAt: string
  ownerPhone: string
}
export interface DbUser {
  phone: string
  fullName: string
  isAdmin: boolean
  businessId: number | null
}
export interface Db {
  version: number
  nextId: number
  users: DbUser[]
  businesses: DbBusiness[]
  categories: Category[]
  states: State[]
  cities: City[]
  products: DbProduct[]
  carts: Record<number, Array<{ id: number; variantId: number; quantity: number }>>
  orders: DbOrder[]
  settings: PlatformSettings
  rates: ShippingRate[]
}

const STORAGE_KEY = 'tc_mock_db_v1'
const SESSION_KEY = 'tc_mock_session'

export const states: State[] = [
  { id: 1, name: 'Jammu & Kashmir', code: 'JK' },
  { id: 2, name: 'Punjab', code: 'PB' },
  { id: 3, name: 'Haryana', code: 'HR' },
  { id: 4, name: 'Delhi', code: 'DL' },
  { id: 5, name: 'Gujarat', code: 'GJ' },
]
export const cities: City[] = [
  { id: 1, stateId: 1, name: 'Srinagar', isDeliveryZone: true, isSourcingHub: false },
  { id: 2, stateId: 1, name: 'Jammu', isDeliveryZone: true, isSourcingHub: false },
  { id: 3, stateId: 1, name: 'Anantnag', isDeliveryZone: true, isSourcingHub: false },
  { id: 4, stateId: 1, name: 'Baramulla', isDeliveryZone: true, isSourcingHub: false },
  { id: 10, stateId: 2, name: 'Ludhiana', isDeliveryZone: false, isSourcingHub: true },
  { id: 11, stateId: 5, name: 'Surat', isDeliveryZone: false, isSourcingHub: true },
  { id: 12, stateId: 3, name: 'Panipat', isDeliveryZone: false, isSourcingHub: true },
  { id: 13, stateId: 4, name: 'Delhi', isDeliveryZone: false, isSourcingHub: true },
]
export const categories: Category[] = [
  { id: 1, parentId: null, name: 'Shawls & Stoles', slug: 'shawls-stoles' },
  { id: 2, parentId: null, name: 'Woollen Fabric', slug: 'woollen-fabric' },
  { id: 3, parentId: null, name: 'Dress Material', slug: 'dress-material' },
  { id: 4, parentId: null, name: 'Blankets & Bedding', slug: 'blankets-bedding' },
  { id: 5, parentId: null, name: 'Winter Innerwear', slug: 'winter-innerwear' },
  { id: 6, parentId: null, name: 'Cotton & Rayon', slug: 'cotton-rayon' },
]

const cityName = (id: number) => cities.find((c) => c.id === id)?.name ?? ''
const stateName = (cid: number) => states.find((s) => s.id === cities.find((c) => c.id === cid)?.stateId)?.name ?? ''

export function makeAddress(id: number, type: BusinessAddress['addressType'], line1: string, cityId: number, pincode: string, isDefault = true): BusinessAddress {
  return { id, addressType: type, line1, cityId, pincode, isDefault, cityName: cityName(cityId), stateName: stateName(cityId) }
}

function slabs(moq: number, steps: Array<[number | null, number]>): PriceSlab[] {
  let min = moq
  return steps.map(([max, price], i) => {
    const s = { id: i + 1, minQty: min, maxQty: max, pricePerUnit: price }
    if (max !== null) min = max + 1
    return s
  })
}

let vid = 1000
function variants(prefix: string, combos: Array<[string, string, number]>): Variant[] {
  return combos.map(([size, colour, stock]) => ({
    id: ++vid, sku: `${prefix}-${size}-${colour}`.replace(/\s+/g, '').toUpperCase(), size, colour, stockQty: stock, isActive: true,
  }))
}

interface Seed {
  id: number; title: string; cat: number; seller: number; sellerName: string; city: string
  moq: number; unit: Unit; gst: number; material: string; season: string; weight: number
  fabric: string; desc: string; colors: [string, string]; steps: Array<[number | null, number]>
  combos: Array<[string, string, number]>; dispatch: number
}
const seeds: Seed[] = [
  { id: 1, title: 'Merino Wool Shawl, Plain', cat: 1, seller: 101, sellerName: 'Ludhiana Woollen Mills', city: 'Ludhiana', moq: 50, unit: 'Piece', gst: 5, material: 'Wool', season: 'Winter', weight: 380, fabric: '100% merino, 2 x 1 m', desc: 'Soft plain shawl with a fine edge. Good for gifting and daily wear.', colors: ['#7a1f2b', '#a3434f'], steps: [[199, 420], [499, 395], [null, 370]], combos: [['Free', 'Maroon', 300], ['Free', 'Grey', 240], ['Free', 'Navy', 180]], dispatch: 4 },
  { id: 2, title: 'Wool Blend Suiting, 3 m cut', cat: 2, seller: 101, sellerName: 'Ludhiana Woollen Mills', city: 'Ludhiana', moq: 30, unit: 'Set', gst: 5, material: 'Wool blend', season: 'Winter', weight: 900, fabric: '60% wool, 40% poly, 150 cm', desc: 'Warm suiting cloth with a smooth finish. Each set is 3 metres.', colors: ['#2c3e50', '#4a6178'], steps: [[99, 1150], [299, 1080], [null, 1020]], combos: [['3 m', 'Charcoal', 120], ['3 m', 'Brown', 90]], dispatch: 5 },
  { id: 3, title: 'Thermal Innerwear Set, Men', cat: 5, seller: 101, sellerName: 'Ludhiana Woollen Mills', city: 'Ludhiana', moq: 60, unit: 'Set', gst: 12, material: 'Cotton blend', season: 'Winter', weight: 420, fabric: 'Cotton rich, brushed inside', desc: 'Top and lower set. Stretches well and keeps warm under shirts.', colors: ['#34495e', '#7f8c8d'], steps: [[199, 310], [null, 285]], combos: [['M', 'Black', 200], ['L', 'Black', 260], ['XL', 'Black', 150], ['L', 'Grey', 110]], dispatch: 3 },
  { id: 4, title: 'Acrylic Woollen Blanket, Single', cat: 4, seller: 101, sellerName: 'Ludhiana Woollen Mills', city: 'Ludhiana', moq: 40, unit: 'Piece', gst: 12, material: 'Acrylic', season: 'Winter', weight: 1600, fabric: '1.5 x 2.1 m, 1.6 kg', desc: 'Light but warm single bed blanket. Colour does not run.', colors: ['#c0792e', '#e2a45a'], steps: [[99, 520], [null, 480]], combos: [['Single', 'Rust', 160], ['Single', 'Green', 130]], dispatch: 4 },
  { id: 5, title: 'Printed Georgette Fabric', cat: 3, seller: 102, sellerName: 'Surat Silk House', city: 'Surat', moq: 100, unit: 'Metre', gst: 5, material: 'Georgette', season: 'All', weight: 120, fabric: '110 cm width, floral print', desc: 'Light georgette in bright floral prints. Good for suits and kurtis.', colors: ['#c0397b', '#f0a1c4'], steps: [[499, 135], [999, 120], [null, 110]], combos: [['Per metre', 'Pink', 2000], ['Per metre', 'Teal', 1500], ['Per metre', 'Yellow', 1200]], dispatch: 3 },
  { id: 6, title: 'Cotton Silk Dupatta', cat: 3, seller: 102, sellerName: 'Surat Silk House', city: 'Surat', moq: 60, unit: 'Piece', gst: 5, material: 'Cotton silk', season: 'All', weight: 160, fabric: '2.25 m, woven border', desc: 'Soft dupatta with a woven zari border.', colors: ['#1f6f5c', '#58b89b'], steps: [[199, 210], [null, 190]], combos: [['2.25 m', 'Green', 400], ['2.25 m', 'Red', 350], ['2.25 m', 'Black', 280]], dispatch: 3 },
  { id: 7, title: 'Viscose Rayon Plain Fabric', cat: 6, seller: 102, sellerName: 'Surat Silk House', city: 'Surat', moq: 150, unit: 'Metre', gst: 5, material: 'Rayon', season: 'Summer', weight: 140, fabric: '44 inch, 14 kg per 100 m', desc: 'Plain dyed rayon. Falls well and takes colour evenly.', colors: ['#2e86c1', '#85c1e9'], steps: [[999, 78], [null, 70]], combos: [['Per metre', 'Sky', 3000], ['Per metre', 'Mustard', 2500]], dispatch: 2 },
  { id: 8, title: 'Mink Blanket, Double Bed', cat: 4, seller: 103, sellerName: 'Panipat Home Textiles', city: 'Panipat', moq: 24, unit: 'Piece', gst: 12, material: 'Polyester', season: 'Winter', weight: 3200, fabric: '2.1 x 2.4 m, 3.2 kg', desc: 'Heavy double bed mink blanket with a printed design.', colors: ['#8e44ad', '#c39bd3'], steps: [[49, 1150], [149, 1090], [null, 1040]], combos: [['Double', 'Purple', 90], ['Double', 'Wine', 70], ['Double', 'Brown', 60]], dispatch: 4 },
  { id: 9, title: 'Cotton Bedsheet Set, 3 pc', cat: 6, seller: 103, sellerName: 'Panipat Home Textiles', city: 'Panipat', moq: 40, unit: 'Set', gst: 5, material: 'Cotton', season: 'All', weight: 950, fabric: '180 TC, 1 sheet + 2 covers', desc: 'Double bedsheet with two pillow covers. Pre-washed.', colors: ['#16a085', '#76d7c4'], steps: [[99, 360], [null, 335]], combos: [['Double', 'Floral', 200], ['Double', 'Check', 160]], dispatch: 3 },
  { id: 10, title: 'Pure Wool Stole, Woven', cat: 1, seller: 103, sellerName: 'Panipat Home Textiles', city: 'Panipat', moq: 50, unit: 'Piece', gst: 5, material: 'Wool', season: 'Winter', weight: 220, fabric: '70 x 200 cm, jacquard weave', desc: 'Woven stole with a traditional motif.', colors: ['#6e4b2a', '#b08a5b'], steps: [[199, 290], [null, 265]], combos: [['70x200', 'Camel', 220], ['70x200', 'Olive', 180]], dispatch: 4 },
]

function seedProducts(): DbProduct[] {
  return seeds.map((s, i) => {
    const sl = slabs(s.moq, s.steps)
    const prices = sl.map((x) => x.pricePerUnit)
    return {
      id: s.id, slug: s.title.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/(^-|-$)/g, ''), title: s.title, categoryId: s.cat,
      sellerBusinessId: s.seller, sellerName: s.sellerName, sellerCity: s.city,
      imageUrl: swatch(s.colors[0], s.colors[1]), moq: s.moq, unit: s.unit, priceFrom: Math.min(...prices), priceTo: Math.max(...prices),
      material: s.material, description: s.desc, fabricSpec: s.fabric, season: s.season, hsnCode: s.gst === 12 ? '6301' : '5112',
      gstRate: s.gst, weightPerUnitGrams: s.weight, dispatchDays: s.dispatch, returnPolicy: 'Only for wrong or damaged goods, within 3 days of delivery.',
      status: 'Active', createdAt: new Date(Date.now() - i * 86400000 * 3).toISOString(),
      media: [
        { id: s.id * 10 + 1, url: swatch(s.colors[0], s.colors[1]), mediaType: 'Image', sortOrder: 0 },
        { id: s.id * 10 + 2, url: swatch(s.colors[1], s.colors[0], 135), mediaType: 'Image', sortOrder: 1 },
        { id: s.id * 10 + 3, url: swatch(s.colors[0], '#ffffff', 90), mediaType: 'Image', sortOrder: 2 },
      ],
      variants: variants(`P${s.id}`, s.combos), slabs: sl,
    }
  })
}

function doc(id: number, docType: 'GST' | 'PAN' | 'ShopLicence' | 'Udyam') {
  return { id, docType, status: 'Pending' as const, reviewNote: null, url: null }
}

function seedBusinesses(): DbBusiness[] {
  const base = { rejectionNote: null, pan: null, email: null, createdAt: new Date().toISOString() }
  return [
    { ...base, id: 101, type: 'Manufacturer', displayName: 'Ludhiana Woollen Mills', legalName: 'Ludhiana Woollen Mills Pvt Ltd', gstin: '03AAACL1234F1Z5', phone: '9000000002', verificationStatus: 'Approved', ownerPhone: '9000000002', addresses: [makeAddress(1, 'Pickup', 'Focal Point, Phase 5', 10, '141010')], documents: [{ ...doc(1, 'GST'), status: 'Accepted' }] },
    { ...base, id: 102, type: 'Manufacturer', displayName: 'Surat Silk House', legalName: 'Surat Silk House', gstin: '24AABCS5678K1Z2', phone: '9000000012', verificationStatus: 'Approved', ownerPhone: '9000000012', addresses: [makeAddress(2, 'Pickup', 'Ring Road', 11, '395002')], documents: [] },
    { ...base, id: 103, type: 'Manufacturer', displayName: 'Panipat Home Textiles', legalName: 'Panipat Home Textiles', gstin: '06AAHFP9012M1Z8', phone: '9000000013', verificationStatus: 'Approved', ownerPhone: '9000000013', addresses: [makeAddress(3, 'Pickup', 'GT Road', 12, '132103')], documents: [] },
    { ...base, id: 201, type: 'Retailer', displayName: 'Shah Cloth House', legalName: 'Shah Cloth House', gstin: '01AAAFS3456P1Z1', phone: '9000000001', verificationStatus: 'Approved', ownerPhone: '9000000001', addresses: [makeAddress(10, 'Delivery', 'Lal Chowk, Main Market', 1, '190001'), makeAddress(11, 'Delivery', 'Rajbagh', 1, '190008', false)], documents: [{ ...doc(2, 'GST'), status: 'Accepted' }] },
    { ...base, id: 202, type: 'Retailer', displayName: 'Mir Garments', legalName: 'Mir Garments', gstin: '01AAAFM7788Q1Z3', phone: '9000000005', verificationStatus: 'Pending', ownerPhone: '9000000005', addresses: [makeAddress(12, 'Delivery', 'Bus Stand Road', 3, '192101')], documents: [doc(3, 'GST'), doc(4, 'ShopLicence')] },
    { ...base, id: 203, type: 'Manufacturer', displayName: 'Amritsar Shawl Works', legalName: 'Amritsar Shawl Works LLP', gstin: '03AAEFA4411R1Z9', phone: '9000000021', verificationStatus: 'Pending', ownerPhone: '9000000021', addresses: [makeAddress(13, 'Pickup', 'Katra Jaimal Singh', 13, '110006')], documents: [doc(5, 'GST'), doc(6, 'PAN'), doc(7, 'Udyam')] },
    { ...base, id: 204, type: 'Retailer', displayName: 'Bhat Brothers', legalName: 'Bhat Brothers', gstin: '01AAAFB1100T1Z6', phone: '9000000022', verificationStatus: 'Rejected', rejectionNote: 'GST number does not match the certificate.', ownerPhone: '9000000022', addresses: [makeAddress(14, 'Delivery', 'Court Road', 2, '180001')], documents: [{ ...doc(8, 'GST'), status: 'Rejected', reviewNote: 'Not readable' }] },
  ]
}

function seedRates(): ShippingRate[] {
  const out: ShippingRate[] = []
  let id = 1
  const table: Array<[number, number, 'PerKg' | 'PerPiece', number, number, number]> = [
    [10, 1, 'PerKg', 14, 800, 3], [10, 2, 'PerKg', 11, 700, 2], [10, 3, 'PerKg', 15, 850, 4], [10, 4, 'PerKg', 15, 850, 4],
    [11, 1, 'PerKg', 22, 1200, 6], [11, 2, 'PerKg', 19, 1100, 5],
    [12, 1, 'PerKg', 13, 750, 3], [12, 2, 'PerKg', 10, 650, 2], [12, 3, 'PerKg', 14, 800, 4], [12, 4, 'PerPiece', 18, 0, 4],
    [13, 1, 'PerKg', 12, 700, 3],
  ]
  for (const [from, to, type, rate, min, days] of table) out.push({ id: id++, fromCityId: from, toCityId: to, rateType: type, rate, minCharge: min, transitDays: days, isActive: true })
  return out
}

function seedOrders(products: DbProduct[]): DbOrder[] {
  const addr = makeAddress(10, 'Delivery', 'Lal Chowk, Main Market', 1, '190001')
  const mk = (id: number, status: DbSellerOrder['status'], pay: Order['paymentStatus'], daysAgo: number, productId: number, variantIdx: number, qty: number): DbOrder => {
    const p = products.find((x) => x.id === productId)!
    const unit = [...p.slabs].reverse().find((s) => qty >= s.minQty)?.pricePerUnit ?? p.slabs[0].pricePerUnit
    const v = p.variants[variantIdx]
    const sub = unit * qty
    const ship = Math.round(Math.max(14 * (p.weightPerUnitGrams * qty) / 1000, 800))
    const tax = Math.round((sub * p.gstRate) / 100)
    const placed = new Date(Date.now() - daysAgo * 86400000).toISOString()
    const history: DbSellerOrder['history'] = [{ status: 'PendingAcceptance', changedAt: placed, note: null }]
    const steps: DbSellerOrder['status'][] = ['Accepted', 'Dispatched', 'ReachedKashmir', 'Delivered']
    if (status === 'Rejected') history.push({ status: 'Rejected', changedAt: new Date(Date.now() - (daysAgo - 1) * 86400000).toISOString(), note: 'Out of stock' })
    else for (const st of steps.slice(0, steps.indexOf(status) + 1)) history.push({ status: st, changedAt: new Date(Date.now() - Math.max(daysAgo - 1 - history.length, 0) * 86400000).toISOString(), note: null })
    const so: DbSellerOrder = {
      id, orderId: id, orderNumber: `TC-24${1000 + id}`, subOrderNumber: `TC-24${1000 + id}-A`, manufacturerName: p.sellerName, retailerName: 'Shah Cloth House', deliveryCity: 'Srinagar',
      status, paymentStatus: pay, placedAt: placed, subTotal: sub, shippingAmount: ship, taxAmount: tax, total: sub + ship + tax, commissionRate: 3, commissionAmount: Math.round(sub * 0.03),
      items: [{ id, variantId: v.id, productId, title: p.title, size: v.size, colour: v.colour, quantity: qty, unitPrice: unit, gstRate: p.gstRate, lineTotal: sub }],
      history, rejectReason: status === 'Rejected' ? 'Out of stock' : null,
      shipment: ['Dispatched', 'ReachedKashmir', 'Delivered'].includes(status) ? { carrierName: 'VRL Logistics', lrNumber: `LR${770000 + id}`, dispatchedAt: placed } : null,
      manufacturerId: p.sellerBusinessId, retailerId: 201,
    }
    return { id, orderNumber: so.orderNumber, placedAt: placed, subTotal: sub, shippingTotal: ship, taxTotal: tax, grandTotal: so.total, paymentStatus: pay, deliveryAddress: addr, retailerId: 201, sellerOrders: [so] }
  }
  return [
    mk(1, 'PendingAcceptance', 'Paid', 0, 1, 0, 100),
    mk(2, 'Accepted', 'Paid', 2, 3, 1, 120),
    mk(3, 'Dispatched', 'Paid', 5, 2, 0, 40),
    mk(4, 'Delivered', 'Paid', 18, 4, 0, 60),
    mk(5, 'Rejected', 'Refunded', 9, 1, 1, 60),
  ]
}

export function seedDb(): Db {
  const products = seedProducts()
  const phones: DbUser[] = [
    { phone: '9000000001', fullName: 'Irfan Shah', isAdmin: false, businessId: 201 },
    { phone: '9000000002', fullName: 'Rajesh Kumar', isAdmin: false, businessId: 101 },
    { phone: '9000000003', fullName: 'Admin', isAdmin: true, businessId: null },
    { phone: '9000000004', fullName: '', isAdmin: false, businessId: null },
    { phone: '9000000005', fullName: 'Tariq Mir', isAdmin: false, businessId: 202 },
  ]
  return {
    version: 1, nextId: 5000, users: phones, businesses: seedBusinesses(), categories, states, cities, products,
    carts: { 201: [] }, orders: seedOrders(products), settings: { commissionDefaultRate: 3, settlementAutoReleaseDays: 7 }, rates: seedRates(),
  }
}

let db: Db | null = null

export function getDb(): Db {
  if (db) return db
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    if (raw) {
      const parsed = JSON.parse(raw) as Db
      if (parsed.version === 1) return (db = parsed)
    }
  } catch { /* ignore */ }
  return (db = seedDb())
}
export function saveDb() {
  try { localStorage.setItem(STORAGE_KEY, JSON.stringify(db)) } catch { /* storage full or blocked */ }
}
export function resetDb() {
  db = seedDb()
  saveDb()
}
export const nextId = () => {
  const d = getDb()
  return ++d.nextId
}
export function getSession(): string | null {
  try { return localStorage.getItem(SESSION_KEY) } catch { return null }
}
export function setSession(phone: string | null) {
  try {
    if (phone) localStorage.setItem(SESSION_KEY, phone)
    else localStorage.removeItem(SESSION_KEY)
  } catch { /* ignore */ }
}
