import { useEffect, useRef, useState } from 'react'
import { useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Check, ChevronRight, MessageCircle, ShoppingBag } from 'lucide-react'
import { cartApi, productApi } from '@/api/endpoints'
import { CART_ADDED_EVENT, CartLink } from '@/components/HeaderButtons'
import { LandedBreakdown } from '@/components/product/LandedBreakdown'
import { PhotoGallery } from '@/components/product/PhotoGallery'
import { SlabPriceTable } from '@/components/product/SlabPriceTable'
import { VariantPicker } from '@/components/product/VariantPicker'
import { QtyStepper } from '@/components/QtyStepper'
import { StickyActionBar } from '@/components/StickyActionBar'
import { Button } from '@/components/ui/button'
import { BackButton, EmptyState, KeyValue, SavingChip, Skeleton } from '@/components/ui/misc'
import { useToast } from '@/components/ui/toast'
import { fmt, S } from '@/i18n'
import { haptic, CountUp } from '@/motion'
import { nextSlabHint, slabFor, totalSaving } from '@/lib/slabs'
import { money } from '@/lib/utils'
import { waLink } from '@/lib/whatsapp'

function Header() {
  return (
    <div className="flex items-center justify-between px-4 pt-[18px]">
      <BackButton />
      <span className="text-[15px] font-medium">{S.product.detailsTitle}</span>
      <CartLink />
    </div>
  )
}

function ProductSkeleton() {
  return (
    <div role="status" aria-label={S.app.loading}>
      <Header />
      <div className="space-y-4 px-5 pt-4">
        <Skeleton className="h-9 w-3/4" />
        <Skeleton className="h-4 w-1/2" />
        <Skeleton className="h-80 !rounded-[24px]" />
        <Skeleton className="h-11 w-2/3" />
        <Skeleton className="h-20" />
      </div>
    </div>
  )
}

export default function ProductPage() {
  const { slug = '' } = useParams()
  const qc = useQueryClient()
  const toast = useToast()
  const [qtys, setQtys] = useState<Record<number, number>>({})
  const [activeId, setActiveId] = useState<number | undefined>()
  const [justAdded, setJustAdded] = useState(false)
  const timer = useRef<number | undefined>(undefined)
  useEffect(() => () => window.clearTimeout(timer.current), [])
  const product = useQuery({ queryKey: ['product', slug], queryFn: () => productApi.bySlug(slug) })
  const p = product.data

  const add = useMutation({
    mutationFn: async () => {
      for (const [variantId, quantity] of Object.entries(qtys)) if (quantity > 0) await cartApi.add(Number(variantId), quantity)
    },
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['cart'] })
      setQtys({})
      setJustAdded(true)
      window.clearTimeout(timer.current)
      timer.current = window.setTimeout(() => setJustAdded(false), 1600)
      haptic()
      window.dispatchEvent(new Event(CART_ADDED_EVENT))
      toast.success(S.product.added)
    },
    onError: (e) => toast.error(e.message),
  })

  if (product.isLoading) return <ProductSkeleton />
  if (product.error || !p) {
    return (
      <div>
        <Header />
        <EmptyState title={S.product.notFound} />
      </div>
    )
  }

  const variants = p.variants.filter((v) => v.isActive)
  const active = variants.find((v) => v.id === activeId) ?? variants.find((v) => v.stockQty > 0) ?? variants[0]
  const total = Object.values(qtys).reduce((a, b) => a + b, 0)
  const unitPrice = slabFor(p.slabs, total)?.pricePerUnit
  const hint = nextSlabHint(p.slabs, total)
  const saving = totalSaving(p.slabs, total)
  const step = p.unit === 'Piece' ? 10 : 1
  const activeQty = active ? (qtys[active.id] ?? 0) : 0
  const setActiveQty = (n: number) => active && setQtys((x) => ({ ...x, [active.id]: Math.max(0, Math.min(active.stockQty, n)) }))

  const onAdd = () => {
    if (total < p.moq) return toast.error(fmt(S.product.moqError, { moq: `${p.moq} ${S.units[p.unit]}` }))
    add.mutate()
  }
  const fillToMoq = () => active && setActiveQty(activeQty + (p.moq - total))

  return (
    <div className="pb-56">
      <Header />

      <div className="px-5 pt-2.5">
        <h1 className="text-balance break-words text-[32px] font-semibold leading-[1.15] tracking-[-0.025em]">{p.title}</h1>
        <p className="mt-2 flex flex-wrap items-center gap-x-1.5 text-sm text-mute">
          <span className="font-medium text-ink">{p.sellerName}</span>
          <span>· {p.sellerCity}</span>
        </p>
      </div>

      <div className="mt-4">
        <PhotoGallery p={p} />
      </div>

      <div className="space-y-6 px-5 pt-5">
        {variants.length > 0 && <VariantPicker variants={variants} qtys={qtys} activeId={active?.id} onPick={setActiveId} />}

        <SlabPriceTable slabs={p.slabs} unit={p.unit} qty={total} />

        <LandedBreakdown p={p} qty={total} />

        <a
          href={waLink(fmt(S.product.waHelpMsg, { name: p.title, id: p.id }))}
          target="_blank"
          rel="noreferrer"
          className="press flex min-h-[52px] items-center gap-2.5 rounded-2xl border border-line bg-surface px-4 text-sm"
        >
          <MessageCircle className="size-5 shrink-0" aria-hidden />
          <span className="flex-1">{S.product.helpWa}</span>
          <ChevronRight className="size-4 shrink-0" aria-hidden />
        </a>

        <section>
          <h2 className="mb-1 text-base font-semibold">{S.product.details}</h2>
          <div className="divide-y divide-line">
            <KeyValue label={S.shop.moq} value={`${p.moq} ${S.units[p.unit]}`} />
            <KeyValue label={S.product.material} value={p.material} />
            <KeyValue label={S.product.fabric} value={p.fabricSpec} />
            <KeyValue label={S.product.season} value={S.seasons[p.season as keyof typeof S.seasons] ?? p.season} />
            <KeyValue label={S.product.weight} value={fmt(S.product.grams, { g: p.weightPerUnitGrams })} />
            <KeyValue label={S.product.hsn} value={p.hsnCode} />
            <KeyValue label={S.product.returns} value={p.returnPolicy} />
          </div>
          <p className="mt-3 text-sm text-mute">{fmt(S.shop.dispatch, { d: p.dispatchDays })}</p>
          <p className="mt-3 break-words text-mute">{p.description}</p>
        </section>
      </div>

      <StickyActionBar>
        <div className="flex items-center gap-2">
          <Button variant="soft" size="sm" className="!min-h-11 px-3.5 text-[13px]" disabled={!active || active.stockQty <= 0} onClick={() => setActiveQty(activeQty + 50)}>
            +50
          </Button>
          <Button variant="soft" size="sm" className="!min-h-11 px-3.5 text-[13px]" disabled={!active || active.stockQty <= 0} onClick={() => setActiveQty(activeQty + 100)}>
            +100
          </Button>
          <span className="flex-1" />
          <QtyStepper label={S.product.quantity} value={activeQty} max={active?.stockQty} step={step} onChange={setActiveQty} />
        </div>
        {(saving > 0 || hint || (total > 0 && total < p.moq)) && (
          <div className="flex flex-wrap gap-2">
            {total > 0 && total < p.moq && (
              <button type="button" onClick={fillToMoq} className="press rounded-xl bg-saving-bg px-2.5 py-2 text-left text-xs leading-snug text-saving">
                {fmt(S.product.moqError, { moq: `${p.moq} ${S.units[p.unit]}` })}
              </button>
            )}
            {saving > 0 && <SavingChip>{fmt(S.product.saveAt, { amt: money(saving), qty: total })}</SavingChip>}
            {hint && <SavingChip>{fmt(S.product.addMore, { n: hint.more, amt: money(hint.saveEach) })}</SavingChip>}
          </div>
        )}
        <Button size="lg" block onClick={onAdd} loading={add.isPending} disabled={total === 0} className="text-base">
          {justAdded ? (
            <>
              <Check className="size-5" aria-hidden />
              {S.product.added}
            </>
          ) : (
            <>
              <ShoppingBag className="size-5" aria-hidden />
              {total > 0 && unitPrice ? (
                <span>
                  {S.product.addToCart} · <CountUp value={unitPrice * total} format={money} />
                </span>
              ) : (
                S.product.addToCart
              )}
            </>
          )}
        </Button>
      </StickyActionBar>
    </div>
  )
}
