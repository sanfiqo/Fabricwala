// Shapes the frontend expects from /api/v1 (see ARCHITECTURE.md section 7).
// Enums are expected as strings (e.g. "Approved"), ids as numbers, money as numbers, dates as ISO strings.

export type Role = 'Admin' | 'Retailer' | 'Manufacturer'
export type BusinessType = 'Retailer' | 'Manufacturer'
export type VerificationStatus = 'Pending' | 'Approved' | 'Rejected' | 'Suspended'
export type DocType = 'GST' | 'PAN' | 'ShopLicence' | 'Udyam'
export type Unit = 'Piece' | 'Set' | 'Metre' | 'Kg'
export type ProductStatus = 'Draft' | 'PendingReview' | 'Active' | 'Inactive'
export type PaymentStatus = 'Pending' | 'Paid' | 'Failed' | 'Refunded' | 'PartiallyRefunded'
export type SellerOrderStatus =
  | 'PendingAcceptance'
  | 'Accepted'
  | 'Rejected'
  | 'Dispatched'
  | 'ReachedKashmir'
  | 'Delivered'
  | 'Cancelled'

export interface Page<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
}

// ---- Auth / business
export interface BusinessSummary {
  id: number
  type: BusinessType
  displayName: string
  verificationStatus: VerificationStatus
  rejectionNote?: string | null
}
export interface Me {
  userId: string
  phone: string
  fullName?: string | null
  roles: Role[]
  business: BusinessSummary | null
}
export interface CreateBusinessInput {
  type: BusinessType
  legalName: string
  displayName: string
  gstin: string
  pan?: string
  phone: string
  email?: string
}
export interface AddressInput {
  addressType: 'Registered' | 'Pickup' | 'Delivery'
  line1: string
  line2?: string
  cityId: number
  pincode: string
  isDefault: boolean
}
export interface BusinessAddress extends AddressInput {
  id: number
  cityName: string
  stateName: string
}
export interface BusinessDocument {
  id: number
  docType: DocType
  status: 'Pending' | 'Accepted' | 'Rejected'
  reviewNote?: string | null
  /** Short-lived signed URL (documents are private). */
  url?: string | null
}
export interface BusinessDetail extends BusinessSummary {
  legalName: string
  gstin: string | null
  pan?: string | null
  phone: string
  email?: string | null
  addresses: BusinessAddress[]
  documents: BusinessDocument[]
}

// ---- Master data
export interface Category {
  id: number
  parentId: number | null
  name: string
  slug: string
  imageUrl?: string | null
}
export interface State {
  id: number
  name: string
  code: string
}
export interface City {
  id: number
  stateId: number
  name: string
  isDeliveryZone: boolean
  isSourcingHub: boolean
}

// ---- Bulk product upload (seller). Real shapes: BulkProductEndpoints.cs
export interface BulkRow {
  categoryId: number
  title: string
  slug: string
  description?: string | null
  gstRate: number
  moq: number
  unit: string
  status: string
}
/** `POST /seller/products/bulk/preview`: the rows that would be created and one text per problem (empty `errors` means ready to import). */
export interface BulkPreview {
  rows: BulkRow[]
  errors: string[]
}
export interface BulkImportResult {
  imported: number
}

// ---- RFQ: requests for quotation (feature switch `Rfq`). Real shapes: src/TextileConnect.Application/Rfq/IRfqService.cs, docs/RFQ.md
export type RfqStatus = 'Open' | 'Expired' | 'Awarded' | 'Cancelled' | 'Closed'
export type QuotationStatus = 'Submitted' | 'Withdrawn' | 'Accepted' | 'Rejected'
export interface RfqInput {
  title: string
  description?: string
  categoryId?: number
  quantity: number
  unit?: string
  targetPricePerUnit?: number
  deliveryPincode: string
  neededBy?: string
  expiresInDays?: number
}
export interface RfqSummary {
  id: number
  title: string
  quantity: number
  unit: string
  status: RfqStatus
  expiresAt: string
  quotationCount: number
  deliveryCity: string
  deliveryState: string
  createdAt: string
  categoryId?: number | null
  categoryName?: string | null
  targetPricePerUnit?: number | null
  /** Only filled for a manufacturer. */
  iHaveQuoted?: boolean | null
}
export interface RfqContact {
  name: string
  phone: string
}
export interface Quotation {
  id: number
  sellerBusinessId: number
  sellerName: string
  pricePerUnit: number
  quantity: number
  total: number
  leadTimeDays: number
  note?: string | null
  status: QuotationStatus
  createdAt: string
  isMine: boolean
  /** Only for the accepted quotation. */
  contact?: RfqContact | null
}
export interface RfqDetail {
  summary: RfqSummary
  description?: string | null
  neededBy?: string | null
  quotations: Quotation[]
  /** Only for the winning manufacturer, after the request is awarded. */
  buyerContact?: RfqContact | null
}
export interface QuotationInput {
  pricePerUnit: number
  quantity: number
  leadTimeDays: number
  note?: string
}
export interface MyQuotationRow {
  rfq: RfqSummary
  quotation: Quotation
}

// ---- Saved products and seller storefront (retailer extras)
/** One row of `GET /saved-products`. The real row has no photo; `available` is false when the product or its seller is no longer live. */
export interface SavedProduct {
  id: number
  slug: string
  title: string
  sellerName: string
  moq: number
  priceFrom: number | null
  available: boolean
}
/** `GET /storefronts/{businessId}`: public seller page. `rating` is 0 until the first rating. */
export interface Storefront {
  business: { id: number; displayName: string; email?: string | null; city?: string | null }
  rating: number
  reviewCount: number
  products: ProductSummary[]
}

// ---- Catalogue
export interface ProductSummary {
  id: number
  slug: string
  title: string
  categoryId: number
  sellerBusinessId: number
  sellerName: string
  sellerCity: string
  imageUrl: string | null
  /** Tiny blurred preview for the blur-up placeholder. TODO(BE). */
  imageLqip?: string | null
  moq: number
  unit: Unit
  priceFrom: number
  priceTo: number
  material?: string | null
  status?: ProductStatus
}
export interface ProductMedia {
  id: number
  url: string
  /** Tiny blurred preview. TODO(BE). */
  lqip?: string | null
  mediaType: 'Image' | 'Video'
  sortOrder: number
}
export interface Variant {
  id: number
  sku: string
  size: string
  colour: string
  stockQty: number
  isActive: boolean
}
export interface PriceSlab {
  id?: number
  minQty: number
  maxQty: number | null
  pricePerUnit: number
}
export interface ProductDetail extends ProductSummary {
  description: string
  material: string
  fabricSpec: string
  season: string
  hsnCode: string
  gstRate: number
  weightPerUnitGrams: number
  dispatchDays: number
  returnPolicy: string
  status: ProductStatus
  media: ProductMedia[]
  variants: Variant[]
  slabs: PriceSlab[]
}
export interface ProductQuery {
  q?: string
  categoryId?: number
  material?: string
  season?: string
  minPrice?: number
  maxPrice?: number
  maxMoq?: number
  sort?: 'new' | 'price-asc' | 'price-desc' | 'moq-asc'
  page?: number
  pageSize?: number
}
export interface ProductInput {
  title: string
  categoryId: number
  description: string
  material: string
  fabricSpec: string
  season: string
  hsnCode: string
  gstRate: number
  moq: number
  unit: Unit
  weightPerUnitGrams: number
  dispatchDays: number
  returnPolicy: string
  status: ProductStatus
  variants: Array<Omit<Variant, 'id'> & { id?: number }>
  /** Price slabs, sent together with the product (the real API has no separate slab route). */
  slabs?: PriceSlab[]
}
export interface LandedCost {
  productId: number
  qty: number
  cityId: number
  slabPrice: number
  /** null when no freight rate exists for this city pair: show "shipping on request". */
  shippingPerUnit: number | null
  gstPerUnit: number
  platformFeePerUnit: number
  landedPerUnit: number | null
  total: number | null
  transitDays: number | null
}

// ---- Cart / orders
export interface CartItem {
  id: number
  variantId: number
  productId: number
  slug: string
  title: string
  size: string
  colour: string
  imageUrl: string | null
  quantity: number
  moq: number
  unit: Unit
  unitPrice: number
  lineTotal: number
}
export interface CartGroup {
  manufacturerId: number
  manufacturerName: string
  items: CartItem[]
  subTotal: number
  shippingAmount: number | null
  taxAmount: number
  total: number
}
export interface Cart {
  groups: CartGroup[]
  subTotal: number
  shippingTotal: number | null
  taxTotal: number
  grandTotal: number
  /** Business delivery addresses the retailer may pick at checkout. */
  addresses: BusinessAddress[]
}
export interface OrderItem {
  id: number
  /** Needed for "Order again". TODO(BE): include variantId in order item responses. */
  variantId?: number
  productId: number
  title: string
  size: string
  colour: string
  quantity: number
  unitPrice: number
  gstRate: number
  lineTotal: number
}
export interface StatusHistory {
  status: SellerOrderStatus
  note?: string | null
  changedAt: string
}
export interface Shipment {
  carrierName: string
  lrNumber: string
  dispatchedAt: string
}
export interface SellerOrder {
  /** Server-calculated money for the seller (real API). Shown as sent. */
  orderValue?: number
  platformFee?: number
  youGet?: number
  acceptDeadline?: string | null
  id: number
  orderId: number
  orderNumber: string
  subOrderNumber: string
  manufacturerName: string
  retailerName: string
  deliveryCity: string
  status: SellerOrderStatus
  paymentStatus: PaymentStatus
  placedAt: string
  items: OrderItem[]
  subTotal: number
  shippingAmount: number
  taxAmount: number
  total: number
  commissionRate: number
  commissionAmount: number
  rejectReason?: string | null
  history: StatusHistory[]
  shipment?: Shipment | null
}
export interface Order {
  id: number
  orderNumber: string
  placedAt: string
  subTotal: number
  shippingTotal: number
  taxTotal: number
  grandTotal: number
  paymentStatus: PaymentStatus
  deliveryAddress: BusinessAddress
  sellerOrders: SellerOrder[]
}
export interface PaymentIntent {
  orderId: number
  gatewayOrderId: string
  amount: number
  currency: string
  /** Public gateway key id for checkout.js. */
  keyId: string | null
}

// ---- Admin
export interface AdminBusiness {
  id: number
  type: BusinessType
  legalName: string
  displayName: string
  gstin: string | null
  phone: string
  cityName: string
  verificationStatus: VerificationStatus
  rejectionNote?: string | null
  createdAt: string
  documents: BusinessDocument[]
}
export interface PlatformSettings {
  commissionDefaultRate: number
  settlementAutoReleaseDays: number
}
export interface ShippingRate {
  id: number
  fromCityId: number
  toCityId: number
  rateType: 'PerKg' | 'PerPiece'
  rate: number
  minCharge: number
  transitDays: number
  isActive: boolean
}
export type ShippingRateInput = Omit<ShippingRate, 'id'>

export interface PincodeInfo {
  pincode: string
  cityId: number
  cityName: string
  stateId: number
  stateName: string
}

// ---- Notifications and problem reports. TODO(BE): shapes are my proposal, see BACKEND_REQUESTS.md.
export interface NotificationItem {
  id: number
  type: 'order' | 'payment' | 'account' | 'system'
  title: string
  body: string
  createdAt: string
  isRead: boolean
  /** In-app link to open, for example /shop/orders/12 */
  link?: string | null
}
export type ClaimReason = 'Damaged' | 'WrongItem' | 'LessQuantity' | 'NotAsShown' | 'Other'
export interface ClaimInput {
  reason: ClaimReason
  pieces: number
  note?: string
}
export interface Claim {
  id: number
  sellerOrderId: number
  reason: ClaimReason
  pieces: number
  status: 'Open' | 'SellerReplied' | 'Refunded' | 'Rejected'
  createdAt: string
}

// ---- Seller dashboard. TODO(BE): shape is my proposal (API_SCREENS.md: "partly"). Every number is computed by the server.
export interface SellerDashboard {
  newOrders: { count: number; value: number; cities: string[]; acceptBy: string | null }
  toPack: number
  salesThisMonth: number
  salesChangePct: number | null
  payoutDue: number
  nextPayoutDate: string | null
  rating: number | null
  ratingCount: number
  salesByMonth: Array<{ month: string; amount: number }>
  lowStock: Array<{ productId: number; title: string; variant: string; qty: number }>
  topProducts: Array<{ productId: number; title: string; pieces: number; amount: number }>
}

// ---- Payouts. TODO(BE): shape is my proposal (API_SCREENS.md: "partly"). Money is computed by the server.
export interface PayoutRow {
  id: number
  orderNumber: string
  buyer: string
  placedAt: string
  deliveredAt: string | null
  /** What the seller gets (order value minus platform fee). */
  amount: number
  status: 'Paid' | 'Scheduled' | 'OnHold'
  /** Paid date, or the date it will be released. */
  date: string | null
}
export interface Payouts {
  next: { amount: number; date: string | null }
  onHold: number
  paidThisMonth: number
  rows: PayoutRow[]
}

// ---- Admin dashboard. TODO(BE): shape is my proposal (API_SCREENS.md: one window only today).
export interface AdminDashboard {
  days: number
  gmv: number
  orders: number
  commission: number
  newBusinesses: number
  pendingApprovals: number
  decisions: Array<{ kind: 'business' | 'claim'; title: string; sub: string; link: string }>
}
