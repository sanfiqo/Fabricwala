import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowRight, Plus, Trash2, Truck } from 'lucide-react'
import { adminApi, masterApi } from '@/api/endpoints'
import type { City, ShippingRate, ShippingRateInput } from '@/api/types'
import { Button } from '@/components/ui/button'
import { Field, Input, Select } from '@/components/ui/field'
import { Badge, Card, EmptyState, ErrorState, PageHeader, PageLoading, Sheet } from '@/components/ui/misc'
import { useToast } from '@/components/ui/toast'
import { S } from '@/i18n'
import { money } from '@/lib/utils'

interface Draft {
  id?: number
  fromCityId: string
  toCityId: string
  rateType: 'PerKg' | 'PerPiece'
  rate: string
  minCharge: string
  transitDays: string
  isActive: boolean
}
const empty: Draft = { fromCityId: '', toCityId: '', rateType: 'PerKg', rate: '', minCharge: '0', transitDays: '3', isActive: true }
const dec = (v: string) => v.replace(/[^\d.]/g, '')

export default function AdminShippingPage() {
  const qc = useQueryClient()
  const toast = useToast()
  const [draft, setDraft] = useState<Draft | null>(null)
  const [errors, setErrors] = useState<Record<string, string>>({})
  const rates = useQuery({ queryKey: ['admin-rates'], queryFn: adminApi.rates })
  const cities = useQuery({ queryKey: ['cities'], queryFn: () => masterApi.cities() })
  const name = (id: number) => cities.data?.find((c) => c.id === id)?.name ?? `#${id}`
  const sourcing = (cities.data ?? []).filter((c: City) => c.isSourcingHub)
  const delivery = (cities.data ?? []).filter((c: City) => c.isDeliveryZone)

  const refresh = () => qc.invalidateQueries({ queryKey: ['admin-rates'] })
  const save = useMutation({
    mutationFn: (d: Draft) => {
      const body: ShippingRateInput = {
        fromCityId: Number(d.fromCityId), toCityId: Number(d.toCityId), rateType: d.rateType, rate: Number(d.rate), minCharge: Number(d.minCharge) || 0,
        transitDays: Number(d.transitDays) || 0, isActive: d.isActive,
      }
      return d.id ? adminApi.updateRate(d.id, body) : adminApi.createRate(body)
    },
    onSuccess: async () => { await refresh(); setDraft(null); toast.success(S.seller.saved) },
    onError: (e) => toast.error(e.message),
  })
  const remove = useMutation({
    mutationFn: (id: number) => adminApi.deleteRate(id),
    onSuccess: async () => { await refresh(); setDraft(null) },
    onError: (e) => toast.error(e.message),
  })

  const open = (r?: ShippingRate) => {
    setErrors({})
    setDraft(r ? { id: r.id, fromCityId: String(r.fromCityId), toCityId: String(r.toCityId), rateType: r.rateType, rate: String(r.rate), minCharge: String(r.minCharge), transitDays: String(r.transitDays), isActive: r.isActive } : empty)
  }
  const set = <K extends keyof Draft>(k: K, v: Draft[K]) => setDraft((d) => (d ? { ...d, [k]: v } : d))
  const submit = () => {
    if (!draft) return
    const e: Record<string, string> = {}
    if (!draft.fromCityId || !draft.toCityId) e.cities = S.admin.needCities
    if (!(Number(draft.rate) > 0)) e.rate = S.admin.needRate
    setErrors(e)
    if (!Object.keys(e).length) save.mutate(draft)
  }

  const add = <Button size="sm" onClick={() => open()}><Plus className="size-4" />{S.admin.addRate}</Button>

  return (
    <div>
      <PageHeader title={S.admin.shippingTitle} action={add} />
      <div className="space-y-3 px-4 pt-4">
        {rates.isLoading || cities.isLoading ? (
          <PageLoading />
        ) : rates.error ? (
          <ErrorState error={rates.error} onRetry={() => void rates.refetch()} />
        ) : (rates.data ?? []).length === 0 ? (
          <EmptyState icon={<Truck className="size-8" />} title={S.admin.noRates} action={add} />
        ) : (
          rates.data!.map((r) => (
            <button key={r.id} type="button" className="block w-full text-left" onClick={() => open(r)}>
              <Card className="p-4 active:bg-navy-50">
                <div className="flex items-center gap-2 font-extrabold text-navy-900">
                  {name(r.fromCityId)}<ArrowRight className="size-4 text-leaf-600" />{name(r.toCityId)}
                  {!r.isActive && <Badge tone="warn" className="ml-auto">{S.admin.off}</Badge>}
                </div>
                <p className="mt-1 text-sm text-mute">
                  {money(r.rate)} / {r.rateType === 'PerKg' ? S.admin.perKg : S.admin.perPiece} · {S.admin.minShort} {money(r.minCharge)} · {r.transitDays} {S.admin.daysShort}
                </p>
              </Card>
            </button>
          ))
        )}
      </div>

      <Sheet open={!!draft} onClose={() => setDraft(null)} title={draft?.id ? S.admin.editRate : S.admin.addRate}>
        {draft && (
          <div className="space-y-4">
            <Field label={S.admin.from} error={errors.cities}>
              {(id, inv) => (
                <Select id={id} invalid={inv} value={draft.fromCityId} onChange={(e) => set('fromCityId', e.target.value)}>
                  <option value="">{S.reg.chooseCity}</option>
                  {sourcing.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
                </Select>
              )}
            </Field>
            <Field label={S.admin.to}>
              {(id) => (
                <Select id={id} value={draft.toCityId} onChange={(e) => set('toCityId', e.target.value)}>
                  <option value="">{S.reg.chooseCity}</option>
                  {delivery.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
                </Select>
              )}
            </Field>
            <Field label={S.admin.rateType}>
              {(id) => (
                <Select id={id} value={draft.rateType} onChange={(e) => set('rateType', e.target.value as Draft['rateType'])}>
                  <option value="PerKg">{S.admin.perKg}</option>
                  <option value="PerPiece">{S.admin.perPiece}</option>
                </Select>
              )}
            </Field>
            <div className="grid grid-cols-2 gap-3">
              <Field label={S.admin.rate} error={errors.rate}>{(id, inv) => <Input id={id} invalid={inv} inputMode="decimal" value={draft.rate} onChange={(e) => set('rate', dec(e.target.value))} />}</Field>
              <Field label={S.admin.minCharge}>{(id) => <Input id={id} inputMode="decimal" value={draft.minCharge} onChange={(e) => set('minCharge', dec(e.target.value))} />}</Field>
            </div>
            <Field label={S.admin.transit}>{(id) => <Input id={id} inputMode="numeric" value={draft.transitDays} onChange={(e) => set('transitDays', e.target.value.replace(/\D/g, ''))} />}</Field>
            <label className="flex min-h-12 items-center gap-3 font-semibold text-navy-900">
              <input type="checkbox" className="size-6 accent-leaf-600" checked={draft.isActive} onChange={(e) => set('isActive', e.target.checked)} />
              {S.admin.active}
            </label>
            <Button block variant="accent" loading={save.isPending} onClick={submit}>{S.app.save}</Button>
            {draft.id && (
              <Button block variant="dangerOutline" loading={remove.isPending} onClick={() => window.confirm(S.admin.confirmDelete) && remove.mutate(draft.id!)}>
                <Trash2 className="size-5" />{S.admin.delete}
              </Button>
            )}
          </div>
        )}
      </Sheet>
    </div>
  )
}
