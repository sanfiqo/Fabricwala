import { useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { MapPin, MessageCircle, Plus } from 'lucide-react'
import { businessApi } from '@/api/endpoints'
import type { BusinessAddress } from '@/api/types'
import { Button } from '@/components/ui/button'
import { Field, Input } from '@/components/ui/field'
import { Badge, EmptyState, ErrorState, PageHeader, Sheet, Skeleton } from '@/components/ui/misc'
import { useToast } from '@/components/ui/toast'
import { useDebounced, useMyBusiness } from '@/hooks'
import { S } from '@/i18n'
import { waLink } from '@/lib/whatsapp'

/** The form in the bottom sheet. The city comes from the pincode, so the person never picks it. */
function AddForm({ type, onDone }: { type: BusinessAddress['addressType']; onDone: () => void }) {
  const qc = useQueryClient()
  const toast = useToast()
  const [v, setV] = useState({ pincode: '', line1: '', line2: '', isDefault: false })
  const [errors, setErrors] = useState<Record<string, string>>({})
  const set = (k: 'pincode' | 'line1' | 'line2') => (e: { target: { value: string } }) => setV((x) => ({ ...x, [k]: e.target.value }))

  const pin = useDebounced(v.pincode, 250)
  const valid = /^\d{6}$/.test(pin)
  const lookup = useQuery({ queryKey: ['pincode', pin], queryFn: () => businessApi.pincode(pin), enabled: valid, retry: false, staleTime: Infinity })

  const add = useMutation({
    mutationFn: () =>
      businessApi.addAddress({ addressType: type, line1: v.line1.trim(), line2: v.line2.trim() || undefined, cityId: lookup.data!.cityId, pincode: v.pincode, isDefault: v.isDefault }),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['business-me'] })
      toast.success(S.addr.added)
      onDone()
    },
    onError: (e) => toast.error(e.message),
  })

  const submit = (e: FormEvent) => {
    e.preventDefault()
    const next: Record<string, string> = {}
    if (!/^\d{6}$/.test(v.pincode)) next.pincode = S.addr.pincodeBad
    else if (!lookup.data) next.pincode = lookup.isError ? S.addr.pincodeUnknown : S.app.loading
    if (!v.line1.trim()) next.line1 = S.addr.line1Need
    setErrors(next)
    if (Object.keys(next).length === 0) add.mutate()
  }

  return (
    <form onSubmit={submit} noValidate className="space-y-4">
      <Field label={S.addr.pincode} hint={lookup.data ? `${lookup.data.cityName}, ${lookup.data.stateName}` : S.addr.pincodeHint} error={errors.pincode}>
        {(id, invalid) => <Input id={id} invalid={invalid} inputMode="numeric" maxLength={6} value={v.pincode} onChange={set('pincode')} />}
      </Field>
      <Field label={S.addr.line1} error={errors.line1}>
        {(id, invalid) => <Input id={id} invalid={invalid} value={v.line1} maxLength={200} onChange={set('line1')} />}
      </Field>
      <Field label={S.addr.line2}>{(id) => <Input id={id} value={v.line2} maxLength={200} onChange={set('line2')} />}</Field>
      <label className="flex min-h-12 cursor-pointer items-center gap-3 text-[15px]">
        <input type="checkbox" checked={v.isDefault} onChange={(e) => setV((x) => ({ ...x, isDefault: e.target.checked }))} className="size-5 accent-[var(--color-brand)]" />
        {S.addr.makeThisDefault}
      </label>
      <Button type="submit" size="lg" block loading={add.isPending}>
        {S.addr.save}
      </Button>
    </form>
  )
}

/** Address book for the shop (delivery addresses) and the seller (pickup addresses). Add and choose the default here; edit and delete need a backend route that does not exist yet. */
export default function AddressesPage() {
  const qc = useQueryClient()
  const toast = useToast()
  const biz = useMyBusiness()
  const [open, setOpen] = useState(false)
  const type: BusinessAddress['addressType'] = biz.data?.type === 'Manufacturer' ? 'Pickup' : 'Delivery'
  const isSeller = type === 'Pickup'
  const list = (biz.data?.addresses ?? []).filter((a) => a.addressType === type)

  const makeDefault = useMutation({
    mutationFn: (id: number) => businessApi.setDefaultAddress(id),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['business-me'] })
      toast.success(S.addr.defaultSet)
    },
    onError: (e) => toast.error(e.message),
  })

  return (
    <div className="pb-10">
      <PageHeader
        title={isSeller ? S.addr.pickupTitle : S.addr.title}
        back={isSeller ? '/seller/account' : '/shop/account'}
        action={
          <Button size="sm" onClick={() => setOpen(true)}>
            <Plus className="size-4" aria-hidden />
            {S.addr.add}
          </Button>
        }
      />
      <div className="space-y-3 px-4 pt-4">
        <p className="text-[15px] text-mute">{S.addr.sub}</p>
        {biz.isLoading ? (
          <div role="status" aria-label={S.app.loading} className="space-y-3">
            <Skeleton className="h-[92px] !rounded-card" />
            <Skeleton className="h-[92px] !rounded-card" />
          </div>
        ) : biz.error ? (
          <ErrorState error={biz.error} onRetry={() => void biz.refetch()} />
        ) : list.length === 0 ? (
          <EmptyState icon={<MapPin className="size-7" aria-hidden />} title={S.addr.none} />
        ) : (
          <ul className="space-y-3">
            {list.map((a) => (
              <li key={a.id} className="flex items-start gap-3 rounded-card border border-line bg-surface p-4">
                <span className="flex size-10 shrink-0 items-center justify-center rounded-full bg-soft" aria-hidden>
                  <MapPin className="size-5" />
                </span>
                <div className="min-w-0 flex-1">
                  <p className="font-medium">
                    {a.cityName} {a.pincode}
                  </p>
                  <p className="break-words text-sm text-mute">{[a.line1, a.line2].filter(Boolean).join(', ')}</p>
                  <p className="text-sm text-mute">{a.stateName}</p>
                  <div className="mt-2">
                    {a.isDefault ? (
                      <Badge tone="good">{S.addr.isDefault}</Badge>
                    ) : (
                      <Button size="sm" variant="soft" loading={makeDefault.isPending && makeDefault.variables === a.id} onClick={() => makeDefault.mutate(a.id)}>
                        {S.addr.makeDefault}
                      </Button>
                    )}
                  </div>
                </div>
              </li>
            ))}
          </ul>
        )}
        <a href={waLink(S.addr.waMsg)} target="_blank" rel="noreferrer" className="press flex min-h-[52px] items-center gap-3 rounded-card border border-line bg-surface px-4 text-sm">
          <MessageCircle className="size-5 shrink-0" aria-hidden />
          <span className="flex-1">{S.addr.changeHelp}</span>
        </a>
      </div>
      <Sheet open={open} onClose={() => setOpen(false)} title={S.addr.add}>
        <AddForm type={type} onDone={() => setOpen(false)} />
      </Sheet>
    </div>
  )
}
