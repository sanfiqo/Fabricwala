import { useState } from 'react'
import { useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { MapPin, ShoppingCart, Truck } from 'lucide-react'
import { cartApi, productApi } from '@/api/endpoints'
import type { PriceSlab, ProductDetail } from '@/api/types'
import { QtyStepper } from '@/components/QtyStepper'
import { Button } from '@/components/ui/button'
import { Badge, Card, EmptyState, ErrorState, KeyValue, PageHeader, PageLoading, SectionTitle } from '@/components/ui/misc'
import { useToast } from '@/components/ui/toast'
import { useDebounced, useDeliveryCity } from '@/hooks'
import { fmt, S } from '@/i18n'
import { cn, money } from '@/lib/utils'

const slabLabel = (s: PriceSlab) =>
  s.maxQty === null ? `${s.minQty}+ (${S.product.andAbove})` : `${s.minQty} – ${s.maxQty}`

function Gallery({ p }: { p: ProductDetail }) {
  const [i, setI] = useState(0)
  const media = p.media.filter((m) => m.mediaType === 'Image')
  const cur = media[i] ?? media[0]
  return (
    <div>
      <div className="aspect-square bg-navy-50">{cur && <img src={cur.url} alt={p.title} className="size-full object-cover" />}</div>
      {media.length > 1 && (
        <div className="flex gap-2 overflow-x-auto px-4 py-3">
          {media.map((m, idx) => (
            <button key={m.id} type="button" aria-label={`${idx + 1}`} onClick={() => setI(idx)} className={cn('size-16 shrink-0 overflow-hidden rounded-xl border-2', idx === i ? 'border-leaf-600' : 'border-transparent')}>
              <img src={m.url} alt="" className="size-full object-cover" />
            </button>
          ))}
        </div>
      )}
    </div>
  )
}

function LandedPanel({ p, qty }: { p: ProductDetail; qty: number }) {
  const city = useDeliveryCity()
  const debounced = useDebounced(qty)
  const enabled = debounced >= p.moq && !!city.cityId
  const q = useQuery({
    queryKey: ['landed', p.id, debounced, city.cityId],
    queryFn: () => productApi.landedCost(p.id, debounced, city.cityId!),
    enabled,
  })

  if (!city.loading && !city.cityId) return null
  const c = q.data
  return (
    <Card className="overflow-hidden border-leaf-600/40">
      <div className="flex items-center gap-2 bg-leaf-50 px-4 py-3">
        <MapPin className="size-5 text-leaf-700" />
        <div>
          <p className="font-extrabold text-navy-900">{fmt(S.product.landed, { city: city.cityName ?? '' })}</p>
          <p className="text-xs text-mute">{S.product.landedHint}</p>
        </div>
      </div>
      <div className="px-4 py-3">
        {!enabled || q.isLoading ? (
          <p className="py-2 text-sm text-mute">{qty > 0 && qty < p.moq ? fmt(S.product.moqError, { moq: p.moq }) : q.isLoading ? S.app.loading : `${S.product.qty}: ≥ ${p.moq}`}</p>
        ) : q.error || !c ? (
          <ErrorState error={q.error} onRetry={() => void q.refetch()} />
        ) : (
          <>
            <KeyValue label={S.product.slabPrice} value={money(c.slabPrice)} />
            <KeyValue label={S.product.freight} value={c.shippingPerUnit === null ? <Badge tone="warn">{S.product.shippingOnRequest}</Badge> : money(c.shippingPerUnit)} />
            <KeyValue label={`${S.product.gst} (${p.gstRate}%)`} value={money(c.gstPerUnit)} />
            {c.platformFeePerUnit > 0 && <KeyValue label={S.product.platformFee} value={money(c.platformFeePerUnit)} />}
            <hr className="stitch my-2" />
            {c.landedPerUnit === null ? (
              <p className="py-1 font-bold text-warn">{S.product.shippingOnRequest}</p>
            ) : (
              <>
                <KeyValue strong label={S.product.perPiece} value={money(c.landedPerUnit)} />
                {c.total !== null && <KeyValue label={fmt(S.product.total, { qty: c.qty })} value={money(c.total)} />}
              </>
            )}
            {c.transitDays !== null && (
              <p className="mt-2 flex items-center gap-1.5 text-sm text-mute"><Truck className="size-4" />{fmt(S.product.transit, { d: c.transitDays })}</p>
            )}
          </>
        )}
      </div>
    </Card>
  )
}

export default function ProductPage() {
  const { slug = '' } = useParams()
  const qc = useQueryClient()
  const toast = useToast()
  const [qtys, setQtys] = useState<Record<number, number>>({})
  const product = useQuery({ queryKey: ['product', slug], queryFn: () => productApi.bySlug(slug) })
  const p = product.data

  const total = Object.values(qtys).reduce((a, b) => a + b, 0)
  const unitPrice = p ? ([...p.slabs].reverse().find((s) => total >= s.minQty) ?? p.slabs[0])?.pricePerUnit : undefined

  const add = useMutation({
    mutationFn: async () => {
      for (const [variantId, quantity] of Object.entries(qtys)) if (quantity > 0) await cartApi.add(Number(variantId), quantity)
    },
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['cart'] })
      setQtys({})
      toast.success(S.product.added)
    },
    onError: (e) => toast.error(e.message),
  })

  if (product.isLoading) return <PageLoading />
  if (product.error || !p) {
    return (
      <>
        <PageHeader title="" back="/shop" />
        <EmptyState title={S.product.notFound} />
      </>
    )
  }

  const onAdd = () => {
    if (total < p.moq) return toast.error(fmt(S.product.moqError, { moq: `${p.moq} ${S.units[p.unit]}` }))
    add.mutate()
  }
  const activeVariants = p.variants.filter((v) => v.isActive)
  const prices = p.slabs.map((s) => s.pricePerUnit)

  return (
    <div>
      <PageHeader title={p.title} back />
      <Gallery p={p} />

      <div className="space-y-1 px-4">
        <p className="text-2xl font-extrabold text-navy-900">
          {money(Math.min(...prices))}
          {Math.max(...prices) !== Math.min(...prices) && <span> – {money(Math.max(...prices))}</span>}
          <span className="text-sm font-semibold text-mute"> {fmt(S.shop.perUnit, { unit: S.units[p.unit] })}</span>
        </p>
        <p className="font-semibold text-leaf-700">{S.shop.moq}: {p.moq} {S.units[p.unit]}</p>
        <p className="text-mute">{S.shop.seller} {p.sellerName}, {p.sellerCity}</p>
        <p className="text-sm text-mute">{fmt(S.shop.dispatch, { d: p.dispatchDays })}</p>
      </div>

      <div className="px-4">
        <SectionTitle>{S.product.slabs}</SectionTitle>
        <Card className="overflow-hidden">
          <table className="w-full text-left">
            <thead className="bg-navy-50 text-xs font-bold uppercase tracking-wide text-mute">
              <tr><th className="px-4 py-2">{S.product.qty}</th><th className="px-4 py-2 text-right">{S.product.price}</th></tr>
            </thead>
            <tbody>
              {p.slabs.map((s) => {
                const on = total > 0 && total >= s.minQty && (s.maxQty === null || total <= s.maxQty)
                return (
                  <tr key={s.minQty} className={cn('border-t border-line', on && 'bg-leaf-50 font-extrabold')}>
                    <td className="px-4 py-3">{slabLabel(s)}</td>
                    <td className="px-4 py-3 text-right">{money(s.pricePerUnit)}</td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </Card>

        <SectionTitle>{S.product.variants}</SectionTitle>
        <div className="space-y-2">
          {activeVariants.map((v) => (
            <Card key={v.id} className="flex items-center gap-3 p-3">
              <div className="min-w-0 flex-1">
                <p className="font-bold text-navy-900">{v.colour} · {v.size}</p>
                <p className={cn('text-sm', v.stockQty > 0 ? 'text-mute' : 'font-semibold text-bad')}>
                  {v.stockQty > 0 ? fmt(S.product.inStock, { n: v.stockQty }) : S.product.outOfStock}
                </p>
              </div>
              <QtyStepper label={`${v.colour} ${v.size}`} value={qtys[v.id] ?? 0} max={v.stockQty} step={p.unit === 'Piece' ? 10 : 1} onChange={(n) => setQtys((x) => ({ ...x, [v.id]: n }))} />
            </Card>
          ))}
        </div>
        {total > 0 && total < p.moq && (
          <button type="button" className="mt-2 text-sm font-bold text-leaf-700 underline" onClick={() => {
            const first = activeVariants.find((v) => v.stockQty >= p.moq - total + (qtys[v.id] ?? 0))
            if (first) setQtys((x) => ({ ...x, [first.id]: (x[first.id] ?? 0) + (p.moq - total) }))
          }}>
            {fmt(S.product.moqError, { moq: p.moq })}
          </button>
        )}

        <div className="mt-5"><LandedPanel p={p} qty={total} /></div>

        <SectionTitle>{S.product.details}</SectionTitle>
        <Card className="px-4 py-2">
          <KeyValue label={S.product.material} value={p.material} />
          <KeyValue label={S.product.fabric} value={p.fabricSpec} />
          <KeyValue label={S.product.season} value={S.seasons[p.season as keyof typeof S.seasons] ?? p.season} />
          <KeyValue label={S.product.weight} value={fmt(S.product.grams, { g: p.weightPerUnitGrams })} />
          <KeyValue label={S.product.hsn} value={p.hsnCode} />
          <KeyValue label={S.product.gst} value={`${p.gstRate}%`} />
          <KeyValue label={S.product.returns} value={p.returnPolicy} />
        </Card>
        <p className="mt-3 text-mute">{p.description}</p>
      </div>

      <div className="sticky bottom-16 z-20 mt-6 border-t border-line bg-white/95 p-3 backdrop-blur">
        <div className="mx-auto flex max-w-2xl items-center gap-3">
          <div className="min-w-0 flex-1 leading-tight">
            <p className="text-sm text-mute">{total} {S.units[p.unit]}</p>
            <p className="text-lg font-extrabold text-navy-900">{unitPrice && total > 0 ? money(unitPrice * total) : '—'}</p>
          </div>
          <Button variant="accent" onClick={onAdd} loading={add.isPending} disabled={total === 0}>
            <ShoppingCart className="size-5" />
            {S.product.addToCart}
          </Button>
        </div>
      </div>
    </div>
  )
}
