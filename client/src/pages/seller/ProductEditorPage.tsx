import { useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ImagePlus, Plus, Trash2 } from 'lucide-react'
import { sellerProductApi } from '@/api/endpoints'
import type { ProductDetail, ProductInput, ProductStatus, Unit } from '@/api/types'
import { Button } from '@/components/ui/button'
import { Field, Input, Select, Textarea } from '@/components/ui/field'
import { Card, ErrorState, PageHeader, PageLoading, SectionTitle } from '@/components/ui/misc'
import { useToast } from '@/components/ui/toast'
import { useCategories } from '@/hooks'
import { S } from '@/i18n'
import { GST_RATES, MATERIALS, SEASONS, UNITS } from '@/lib/constants'
import { checkSlabs, rowsFromSlabs, slabsFromRows, type SlabRow } from '@/lib/slabs'
import { cn } from '@/lib/utils'

interface VariantRow {
  id?: number
  sku: string
  size: string
  colour: string
  stockQty: string
  isActive: boolean
}
interface FormState {
  title: string
  categoryId: string
  description: string
  material: string
  fabricSpec: string
  season: string
  hsnCode: string
  gstRate: string
  moq: string
  unit: Unit
  weightPerUnitGrams: string
  dispatchDays: string
  returnPolicy: string
  status: ProductStatus
}

const blank: FormState = {
  title: '', categoryId: '', description: '', material: 'Wool', fabricSpec: '', season: 'Winter', hsnCode: '', gstRate: '5', moq: '50',
  unit: 'Piece', weightPerUnitGrams: '', dispatchDays: '4', returnPolicy: '', status: 'Draft',
}

function fromProduct(p: ProductDetail): FormState {
  return {
    title: p.title, categoryId: String(p.categoryId), description: p.description, material: p.material, fabricSpec: p.fabricSpec, season: p.season,
    hsnCode: p.hsnCode, gstRate: String(p.gstRate), moq: String(p.moq), unit: p.unit, weightPerUnitGrams: String(p.weightPerUnitGrams),
    dispatchDays: String(p.dispatchDays), returnPolicy: p.returnPolicy, status: p.status,
  }
}

const digits = (v: string) => v.replace(/\D/g, '')

function Editor({ product }: { product?: ProductDetail }) {
  const nav = useNavigate()
  const qc = useQueryClient()
  const toast = useToast()
  const cats = useCategories()
  const [f, setF] = useState<FormState>(product ? fromProduct(product) : blank)
  const [variants, setVariants] = useState<VariantRow[]>(
    product ? product.variants.map((v) => ({ ...v, stockQty: String(v.stockQty) })) : [{ sku: '', size: '', colour: '', stockQty: '100', isActive: true }],
  )
  const [media, setMedia] = useState(product?.media ?? [])
  const moq = Number(f.moq) || 0
  const [slabs, setSlabs] = useState<SlabRow[]>(product ? rowsFromSlabs(product.slabs) : [{ minQty: f.moq, maxQty: '', price: '' }])
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [showSlabErrors, setShowSlabErrors] = useState(false)
  const slabCheck = checkSlabs(moq, slabs)

  const set = <K extends keyof FormState>(k: K, v: FormState[K]) => {
    setF((x) => ({ ...x, [k]: v }))
    setErrors((e) => ({ ...e, [k]: '' }))
  }

  const save = useMutation({
    mutationFn: async () => {
      const input: ProductInput = {
        title: f.title.trim(), categoryId: Number(f.categoryId), description: f.description.trim(), material: f.material, fabricSpec: f.fabricSpec.trim(),
        season: f.season, hsnCode: f.hsnCode.trim(), gstRate: Number(f.gstRate), moq, unit: f.unit, weightPerUnitGrams: Number(f.weightPerUnitGrams) || 0,
        dispatchDays: Number(f.dispatchDays) || 0, returnPolicy: f.returnPolicy.trim(), status: f.status,
        variants: variants.map((v, i) => ({
          id: v.id, size: v.size.trim(), colour: v.colour.trim(), stockQty: Number(v.stockQty) || 0, isActive: v.isActive,
          sku: v.sku.trim() || `${(v.colour || 'X').slice(0, 3)}-${(v.size || 'X').slice(0, 4)}-${Date.now().toString(36)}${i}`.toUpperCase().replace(/\s+/g, ''),
        })),
      }
      const saved = product ? await sellerProductApi.update(product.id, input) : await sellerProductApi.create(input)
      await sellerProductApi.saveSlabs(saved.id, slabsFromRows(slabCheck.sorted))
      return saved
    },
    onSuccess: async (saved) => {
      await Promise.all([
        qc.invalidateQueries({ queryKey: ['seller-products'] }),
        qc.invalidateQueries({ queryKey: ['seller-product', String(saved.id)] }),
      ])
      toast.success(S.seller.saved)
      // Keep the server's ids so the next save updates these rows instead of adding new ones.
      setVariants(saved.variants.map((v) => ({ ...v, stockQty: String(v.stockQty) })))
      if (!product) nav(`/seller/products/${saved.id}`, { replace: true })
    },
    onError: (e) => toast.error(e.message),
  })

  const addPhoto = useMutation({
    mutationFn: (file: File) => sellerProductApi.addMedia(product!.id, file),
    onSuccess: (m) => setMedia((x) => [...x, m]),
    onError: (e) => toast.error(e.message),
  })

  const onSave = () => {
    const e: Record<string, string> = {}
    if (!f.title.trim()) e.title = S.seller.needTitle
    if (!f.categoryId) e.categoryId = S.seller.needCategory
    if (!(moq >= 1)) e.moq = S.reg.required
    if (variants.length === 0 || variants.some((v) => !v.size.trim() || !v.colour.trim())) e.variants = S.seller.variantNone
    setErrors(e)
    setShowSlabErrors(true)
    if (Object.keys(e).length || slabCheck.errors.length) {
      toast.error([...Object.values(e), ...slabCheck.errors][0])
      return
    }
    save.mutate()
  }

  const patchSlab = (i: number, k: keyof SlabRow, v: string) => {
    setSlabs((rows) => rows.map((r, idx) => (idx === i ? { ...r, [k]: digits(v) } : r)))
  }
  const addSlab = () => {
    const last = [...slabs].sort((a, b) => Number(a.minQty) - Number(b.minQty)).at(-1)
    const prevMax = last && last.maxQty ? Number(last.maxQty) : null
    setSlabs((rows) => {
      // Close the open-ended last row and start a new row right after it.
      const closed = last && !last.maxQty ? rows.map((r) => (r === last ? { ...r, maxQty: String(Number(r.minQty || 0) + 99) } : r)) : rows
      const lastMax = prevMax ?? (last ? Number(last.minQty) + 99 : moq - 1)
      return [...closed, { minQty: String(lastMax + 1), maxQty: '', price: '' }]
    })
  }

  return (
    <div>
      <PageHeader title={product ? S.seller.editTitle : S.seller.newTitle} back="/seller/products" />
      <div className="space-y-4 px-4 pb-8 pt-2">
        <SectionTitle>{S.seller.basic}</SectionTitle>
        <Field label={S.seller.title} error={errors.title}>{(id, inv) => <Input id={id} invalid={inv} value={f.title} onChange={(e) => set('title', e.target.value)} />}</Field>
        <Field label={S.seller.category} error={errors.categoryId}>
          {(id, inv) => (
            <Select id={id} invalid={inv} value={f.categoryId} onChange={(e) => set('categoryId', e.target.value)}>
              <option value="">{S.seller.chooseCategory}</option>
              {(cats.data ?? []).map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
            </Select>
          )}
        </Field>
        <Field label={S.seller.description}>{(id) => <Textarea id={id} value={f.description} onChange={(e) => set('description', e.target.value)} />}</Field>
        <div className="grid grid-cols-2 gap-3">
          <Field label={S.seller.material}>
            {(id) => (
              <Select id={id} value={f.material} onChange={(e) => set('material', e.target.value)}>
                {[...new Set([...MATERIALS, f.material])].map((m) => <option key={m}>{m}</option>)}
              </Select>
            )}
          </Field>
          <Field label={S.seller.season}>
            {(id) => <Select id={id} value={f.season} onChange={(e) => set('season', e.target.value)}>{SEASONS.map((m) => <option key={m} value={m}>{S.seasons[m]}</option>)}</Select>}
          </Field>
        </div>
        <Field label={S.seller.fabric}>{(id) => <Input id={id} value={f.fabricSpec} onChange={(e) => set('fabricSpec', e.target.value)} />}</Field>
        <div className="grid grid-cols-2 gap-3">
          <Field label={S.seller.hsn}>{(id) => <Input id={id} inputMode="numeric" value={f.hsnCode} onChange={(e) => set('hsnCode', digits(e.target.value).slice(0, 8))} />}</Field>
          <Field label={S.seller.gst}>
            {(id) => <Select id={id} value={f.gstRate} onChange={(e) => set('gstRate', e.target.value)}>{GST_RATES.map((g) => <option key={g} value={g}>{g}%</option>)}</Select>}
          </Field>
          <Field label={S.seller.moq} error={errors.moq}>{(id, inv) => <Input id={id} invalid={inv} inputMode="numeric" value={f.moq} onChange={(e) => set('moq', digits(e.target.value))} />}</Field>
          <Field label={S.seller.unit}>
            {(id) => <Select id={id} value={f.unit} onChange={(e) => set('unit', e.target.value as Unit)}>{UNITS.map((u) => <option key={u} value={u}>{S.units[u]}</option>)}</Select>}
          </Field>
          <Field label={S.seller.weight}>{(id) => <Input id={id} inputMode="numeric" value={f.weightPerUnitGrams} onChange={(e) => set('weightPerUnitGrams', digits(e.target.value))} />}</Field>
          <Field label={S.seller.dispatchDays}>{(id) => <Input id={id} inputMode="numeric" value={f.dispatchDays} onChange={(e) => set('dispatchDays', digits(e.target.value))} />}</Field>
        </div>
        <Field label={S.seller.returnPolicy}>{(id) => <Input id={id} value={f.returnPolicy} onChange={(e) => set('returnPolicy', e.target.value)} />}</Field>

        <hr className="stitch" />
        <SectionTitle>{S.seller.slabs}</SectionTitle>
        <p className="-mt-2 text-sm text-mute">{S.seller.slabsSub}</p>
        <div className="space-y-2">
          {slabs.map((r, i) => {
            const sortedIdx = slabCheck.sorted.indexOf(r)
            const invalid = showSlabErrors && slabCheck.badRows.has(sortedIdx)
            return (
              <Card key={i} className={cn('grid grid-cols-[1fr_1fr_1fr_auto] items-end gap-2 p-3', invalid && 'border-bad bg-bad-bg/40')}>
                <Field label={S.seller.minQty}>{(id) => <Input id={id} inputMode="numeric" value={r.minQty} onChange={(e) => patchSlab(i, 'minQty', e.target.value)} />}</Field>
                <Field label={S.seller.maxQty}>{(id) => <Input id={id} inputMode="numeric" placeholder="∞" value={r.maxQty} onChange={(e) => patchSlab(i, 'maxQty', e.target.value)} />}</Field>
                <Field label={S.seller.price}>{(id) => <Input id={id} inputMode="numeric" value={r.price} onChange={(e) => patchSlab(i, 'price', e.target.value)} />}</Field>
                <button type="button" aria-label={S.seller.remove} onClick={() => setSlabs((x) => x.filter((_, idx) => idx !== i))} className="mb-0.5 flex size-12 items-center justify-center rounded-xl text-bad hover:bg-bad-bg">
                  <Trash2 className="size-5" />
                </button>
              </Card>
            )
          })}
        </div>
        <p className="text-xs text-mute">{S.seller.maxQtyHint}</p>
        {slabCheck.errors.length > 0 && (showSlabErrors || slabs.length > 1) && (
          <ul role="alert" className="space-y-1 rounded-xl bg-bad-bg p-3 text-sm font-semibold text-bad">
            {slabCheck.errors.map((m) => <li key={m}>• {m}</li>)}
          </ul>
        )}
        <Button variant="outline" onClick={addSlab}><Plus className="size-4" />{S.seller.addSlab}</Button>

        <hr className="stitch" />
        <SectionTitle>{S.seller.variants}</SectionTitle>
        {errors.variants && <p role="alert" className="text-sm font-semibold text-bad">{errors.variants}</p>}
        <div className="space-y-2">
          {variants.map((v, i) => {
            const up = (k: keyof VariantRow, val: string | boolean) => setVariants((rows) => rows.map((r, idx) => (idx === i ? { ...r, [k]: val } : r)))
            return (
              <Card key={i} className="space-y-2 p-3">
                <div className="grid grid-cols-2 gap-2">
                  <Field label={S.seller.size}>{(id) => <Input id={id} value={v.size} onChange={(e) => up('size', e.target.value)} />}</Field>
                  <Field label={S.seller.colour}>{(id) => <Input id={id} value={v.colour} onChange={(e) => up('colour', e.target.value)} />}</Field>
                  <Field label={S.seller.stock}>{(id) => <Input id={id} inputMode="numeric" value={v.stockQty} onChange={(e) => up('stockQty', digits(e.target.value))} />}</Field>
                  <Field label={S.seller.sku}>{(id) => <Input id={id} value={v.sku} placeholder="auto" onChange={(e) => up('sku', e.target.value.toUpperCase())} />}</Field>
                </div>
                <div className="flex items-center justify-between">
                  <label className="flex min-h-11 items-center gap-2 text-sm font-semibold text-navy-900">
                    <input type="checkbox" checked={v.isActive} onChange={(e) => up('isActive', e.target.checked)} className="size-5 accent-leaf-600" />
                    {S.seller.statusActive}
                  </label>
                  <Button variant="dangerOutline" size="sm" onClick={() => setVariants((rows) => rows.filter((_, idx) => idx !== i))}><Trash2 className="size-4" />{S.seller.remove}</Button>
                </div>
              </Card>
            )
          })}
        </div>
        <Button variant="outline" onClick={() => setVariants((x) => [...x, { sku: '', size: '', colour: '', stockQty: '100', isActive: true }])}><Plus className="size-4" />{S.seller.addVariant}</Button>

        <hr className="stitch" />
        <SectionTitle>{S.seller.photos}</SectionTitle>
        {product ? (
          <div className="grid grid-cols-3 gap-2">
            {media.map((m) => (
              <div key={m.id} className="aspect-square overflow-hidden rounded-xl bg-navy-50"><img src={m.url} alt="" className="size-full object-cover" /></div>
            ))}
            <label className={cn('flex aspect-square cursor-pointer flex-col items-center justify-center gap-1 rounded-xl border-2 border-dashed border-leaf-600/50 text-sm font-bold text-leaf-700 focus-within:outline-2 focus-within:outline-leaf-600', addPhoto.isPending && 'opacity-50')}>
              <ImagePlus className="size-6" />
              {S.seller.addPhoto}
              <input type="file" accept="image/*" className="sr-only" disabled={addPhoto.isPending} onChange={(e) => { const file = e.target.files?.[0]; if (file) addPhoto.mutate(file); e.target.value = '' }} />
            </label>
          </div>
        ) : (
          <p className="rounded-xl bg-navy-50 p-3 text-sm text-mute">{S.seller.saveFirst}</p>
        )}

        <hr className="stitch" />
        <Field label={S.seller.status}>
          {(id) => (
            <Select id={id} value={f.status} onChange={(e) => set('status', e.target.value as ProductStatus)}>
              <option value="Draft">{S.seller.statusDraft}</option>
              <option value="Active">{S.seller.statusActive}</option>
              <option value="Inactive">{S.seller.statusInactive}</option>
              {f.status === 'PendingReview' && <option value="PendingReview">{S.seller.statusPendingReview}</option>}
            </Select>
          )}
        </Field>
        <Button block variant="accent" loading={save.isPending} onClick={onSave}>{S.app.save}</Button>
      </div>
    </div>
  )
}

export default function ProductEditorPage() {
  const { id } = useParams()
  const product = useQuery({ queryKey: ['seller-product', id], queryFn: () => sellerProductApi.get(Number(id)), enabled: !!id })
  if (!id) return <Editor />
  if (product.isLoading) return <PageLoading />
  if (product.error || !product.data) return <ErrorState error={product.error} onRetry={() => void product.refetch()} />
  return <Editor key={id} product={product.data} />
}
