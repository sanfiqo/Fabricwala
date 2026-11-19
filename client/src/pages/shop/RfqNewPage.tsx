import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { rfqApi } from '@/api/endpoints'
import { qk } from '@/api/keys'
import type { RfqInput } from '@/api/types'
import { Button } from '@/components/ui/button'
import { Field, Input, Select, Textarea } from '@/components/ui/field'
import { PageHeader } from '@/components/ui/misc'
import { useToast } from '@/components/ui/toast'
import { useCategories } from '@/hooks'
import { fmt, S } from '@/i18n'

const UNITS = ['Piece', 'Set', 'Metre', 'Kg'] as const
const DAYS = [7, 14, 21, 30]

/** New request: say what you need, how many, and where to deliver. Makers who sell that category are told. */
export default function RfqNewPage() {
  const nav = useNavigate()
  const qc = useQueryClient()
  const toast = useToast()
  const cats = useCategories()
  const [v, setV] = useState({ title: '', description: '', categoryId: '', quantity: '', unit: 'Piece', target: '', pincode: '', neededBy: '', days: '14' })
  const [errors, setErrors] = useState<Record<string, string>>({})
  const set = (k: keyof typeof v) => (e: { target: { value: string } }) => setV((x) => ({ ...x, [k]: e.target.value }))

  const create = useMutation({
    mutationFn: (input: RfqInput) => rfqApi.create(input),
    onSuccess: (d) => {
      void qc.invalidateQueries({ queryKey: qk.rfqs() })
      toast.success(S.rfq.sent)
      nav(`/shop/rfq/${d.summary.id}`, { replace: true, viewTransition: true })
    },
    onError: (e) => toast.error(e.message),
  })

  const submit = (e: FormEvent) => {
    e.preventDefault()
    const next: Record<string, string> = {}
    if (!v.title.trim()) next.title = S.rfq.needTitle
    if (!(Number(v.quantity) > 0)) next.quantity = S.rfq.needQty
    if (!/^\d{6}$/.test(v.pincode)) next.pincode = S.rfq.needPincode
    setErrors(next)
    if (Object.keys(next).length) return
    create.mutate({
      title: v.title.trim(),
      description: v.description.trim() || undefined,
      categoryId: v.categoryId ? Number(v.categoryId) : undefined,
      quantity: Math.floor(Number(v.quantity)),
      unit: v.unit,
      targetPricePerUnit: Number(v.target) > 0 ? Number(v.target) : undefined,
      deliveryPincode: v.pincode,
      neededBy: v.neededBy || undefined,
      expiresInDays: Number(v.days),
    })
  }

  return (
    <div className="pb-10">
      <PageHeader title={S.rfq.newTitle} back="/shop/rfq" />
      <form onSubmit={submit} noValidate className="space-y-4 px-4 pt-4">
        <Field label={S.rfq.fTitle} hint={S.rfq.fTitleHint} error={errors.title}>
          {(id, invalid) => <Input id={id} invalid={invalid} value={v.title} maxLength={150} onChange={set('title')} />}
        </Field>
        <Field label={S.rfq.fDesc} hint={S.rfq.fDescHint}>
          {(id) => <Textarea id={id} value={v.description} maxLength={1000} onChange={set('description')} />}
        </Field>
        <Field label={S.rfq.fCategory}>
          {(id) => (
            <Select id={id} value={v.categoryId} onChange={set('categoryId')}>
              <option value="">{S.rfq.fNoCategory}</option>
              {(cats.data ?? []).map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </Select>
          )}
        </Field>
        <div className="grid grid-cols-2 gap-3">
          <Field label={S.rfq.fQty} error={errors.quantity}>
            {(id, invalid) => <Input id={id} invalid={invalid} inputMode="numeric" value={v.quantity} onChange={set('quantity')} />}
          </Field>
          <Field label={S.rfq.fUnit}>
            {(id) => (
              <Select id={id} value={v.unit} onChange={set('unit')}>
                {UNITS.map((u) => (
                  <option key={u} value={u}>
                    {S.units[u]}
                  </option>
                ))}
              </Select>
            )}
          </Field>
        </div>
        <Field label={S.rfq.fTarget}>
          {(id) => <Input id={id} inputMode="decimal" value={v.target} onChange={set('target')} />}
        </Field>
        <Field label={S.rfq.fPincode} error={errors.pincode}>
          {(id, invalid) => <Input id={id} invalid={invalid} inputMode="numeric" maxLength={6} value={v.pincode} onChange={set('pincode')} />}
        </Field>
        <Field label={S.rfq.fNeededBy}>
          {(id) => <Input id={id} type="date" value={v.neededBy} onChange={set('neededBy')} />}
        </Field>
        <Field label={S.rfq.fDays}>
          {(id) => (
            <Select id={id} value={v.days} onChange={set('days')}>
              {DAYS.map((d) => (
                <option key={d} value={d}>
                  {fmt(S.rfq.days, { n: d })}
                </option>
              ))}
            </Select>
          )}
        </Field>
        <Button type="submit" size="lg" block loading={create.isPending}>
          {S.rfq.submit}
        </Button>
      </form>
    </div>
  )
}
