import { useMemo, useRef, useState } from 'react'
import { Navigate, useNavigate } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Check, FileText, Store, Factory } from 'lucide-react'
import { businessApi, masterApi } from '@/api/endpoints'
import type { BusinessType, DocType } from '@/api/types'
import { homeFor, useMe } from '@/auth'
import { Button } from '@/components/ui/button'
import { Field, Input, Select } from '@/components/ui/field'
import { PageHeader, PageLoading } from '@/components/ui/misc'
import { fmt, S } from '@/i18n'
import { cn } from '@/lib/utils'

const GSTIN = /^\d{2}[A-Z]{5}\d{4}[A-Z][1-9A-Z]Z[0-9A-Z]$/
const MAX_FILE = 5 * 1024 * 1024
const steps = [S.reg.stepType, S.reg.stepDetails, S.reg.stepAddress, S.reg.stepDocs]

interface Form {
  type: BusinessType
  legalName: string
  displayName: string
  gstin: string
  pan: string
  phone: string
  email: string
  line1: string
  line2: string
  stateId: string
  cityId: string
  pincode: string
}
type Errors = Partial<Record<keyof Form | DocType, string>>

export default function RegisterPage() {
  const { data: me, isLoading } = useMe()
  const qc = useQueryClient()
  const nav = useNavigate()
  const [step, setStep] = useState(0)
  const [form, setForm] = useState<Form>({
    type: 'Retailer', legalName: '', displayName: '', gstin: '', pan: '', phone: me?.phone ?? '', email: '',
    line1: '', line2: '', stateId: '1', cityId: '', pincode: '',
  })
  const [files, setFiles] = useState<Partial<Record<DocType, File>>>({})
  const [errors, setErrors] = useState<Errors>({})
  const [submitError, setSubmitError] = useState('')
  // Remember what already succeeded so a retry after a network failure does not create duplicates.
  const done = useRef({ business: false, address: false, docs: new Set<DocType>() })

  const states = useQuery({ queryKey: ['states'], queryFn: masterApi.states })
  const cities = useQuery({ queryKey: ['cities'], queryFn: () => masterApi.cities() })
  const cityOptions = useMemo(() => (cities.data ?? []).filter((c) => String(c.stateId) === form.stateId), [cities.data, form.stateId])

  const set = <K extends keyof Form>(k: K, v: Form[K]) => {
    setForm((f) => ({ ...f, [k]: v }))
    setErrors((e) => ({ ...e, [k]: undefined }))
  }

  const docTypes: Array<{ type: DocType; required: boolean }> = [
    { type: 'GST', required: true },
    { type: 'PAN', required: false },
    form.type === 'Retailer' ? { type: 'ShopLicence', required: false } : { type: 'Udyam', required: false },
  ]

  const submit = useMutation({
    mutationFn: async () => {
      if (!done.current.business) {
        await businessApi.create({
          type: form.type, legalName: form.legalName.trim(), displayName: form.displayName.trim(), gstin: form.gstin.trim().toUpperCase(),
          pan: form.pan.trim().toUpperCase() || undefined, phone: form.phone, email: form.email.trim() || undefined,
        })
        done.current.business = true
      }
      if (!done.current.address) {
        await businessApi.addAddress({
          addressType: form.type === 'Retailer' ? 'Delivery' : 'Pickup', line1: form.line1.trim(), line2: form.line2.trim() || undefined,
          cityId: Number(form.cityId), pincode: form.pincode, isDefault: true,
        })
        done.current.address = true
      }
      for (const [type, file] of Object.entries(files) as Array<[DocType, File]>) {
        if (done.current.docs.has(type)) continue
        await businessApi.addDocument(type, file)
        done.current.docs.add(type)
      }
    },
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['me'] })
      nav('/pending', { replace: true })
    },
    onError: (e) => setSubmitError(e.message),
  })

  if (isLoading) return <PageLoading />
  if (me?.business && !submit.isPending && !submit.isError) return <Navigate to={homeFor(me)} replace />
  if (me?.roles.includes('Admin')) return <Navigate to={homeFor(me)} replace />

  const validate = (s: number): Errors => {
    const e: Errors = {}
    if (s === 1) {
      if (!form.legalName.trim()) e.legalName = S.reg.required
      if (!form.displayName.trim()) e.displayName = S.reg.required
      if (!GSTIN.test(form.gstin.trim().toUpperCase())) e.gstin = S.reg.gstinInvalid
      if (!/^\d{10}$/.test(form.phone)) e.phone = S.login.invalidMobile
    }
    if (s === 2) {
      if (!form.line1.trim()) e.line1 = S.reg.required
      if (!form.cityId) e.cityId = S.reg.required
      if (!/^\d{6}$/.test(form.pincode)) e.pincode = S.reg.pincodeInvalid
    }
    if (s === 3 && !files.GST) e.GST = S.reg.required
    return e
  }
  const next = () => {
    const e = validate(step)
    setErrors(e)
    if (Object.keys(e).length) return
    if (step === 3) {
      setSubmitError('')
      submit.mutate()
    } else setStep(step + 1)
  }

  const pick = (type: DocType, file: File | undefined) => {
    if (!file) return
    if (file.size > MAX_FILE) return setErrors((e) => ({ ...e, [type]: '5 MB max' }))
    setFiles((f) => ({ ...f, [type]: file }))
    setErrors((e) => ({ ...e, [type]: undefined }))
  }

  return (
    <div>
      <PageHeader title={S.reg.title} />
      <div className="px-4 pb-6 pt-4">
        <p className="text-mute">{S.reg.sub}</p>
        <ol className="my-5 flex items-center gap-2" aria-label={fmt(S.reg.step, { n: step + 1, total: steps.length })}>
          {steps.map((label, i) => (
            <li key={label} className="flex flex-1 flex-col items-center gap-1">
              <span className={cn('flex size-8 items-center justify-center rounded-full text-sm font-bold', i < step ? 'bg-leaf-600 text-white' : i === step ? 'bg-navy-900 text-white' : 'bg-navy-50 text-mute')}>
                {i < step ? <Check className="size-4" /> : i + 1}
              </span>
              <span className={cn('text-xs font-semibold', i === step ? 'text-navy-900' : 'text-mute')}>{label}</span>
            </li>
          ))}
        </ol>

        <div className="space-y-5">
          {step === 0 && (
            <div className="space-y-3" role="radiogroup" aria-label={S.reg.type}>
              {([
                ['Retailer', S.reg.retailer, S.reg.retailerSub, Store],
                ['Manufacturer', S.reg.manufacturer, S.reg.manufacturerSub, Factory],
              ] as const).map(([v, t, sub, Icon]) => (
                <button
                  key={v}
                  type="button"
                  role="radio"
                  aria-checked={form.type === v}
                  onClick={() => set('type', v)}
                  className={cn('flex w-full items-center gap-4 rounded-2xl border-2 p-4 text-left transition', form.type === v ? 'border-leaf-600 bg-leaf-50' : 'border-line')}
                >
                  <span className={cn('flex size-14 items-center justify-center rounded-xl', form.type === v ? 'bg-navy-900 text-white' : 'bg-navy-50 text-navy-900')}>
                    <Icon className="size-7" />
                  </span>
                  <span>
                    <span className="block text-lg font-extrabold text-navy-900">{t}</span>
                    <span className="block text-mute">{sub}</span>
                  </span>
                </button>
              ))}
            </div>
          )}

          {step === 1 && (
            <>
              <Field label={S.reg.legalName} error={errors.legalName}>
                {(id, invalid) => <Input id={id} invalid={invalid} value={form.legalName} onChange={(e) => set('legalName', e.target.value)} autoComplete="organization" />}
              </Field>
              <Field label={S.reg.displayName} error={errors.displayName}>
                {(id, invalid) => <Input id={id} invalid={invalid} value={form.displayName} onChange={(e) => set('displayName', e.target.value)} />}
              </Field>
              <Field label={S.reg.gstin} hint={S.reg.gstinHint} error={errors.gstin}>
                {(id, invalid) => <Input id={id} invalid={invalid} value={form.gstin} onChange={(e) => set('gstin', e.target.value.toUpperCase().slice(0, 15))} autoCapitalize="characters" />}
              </Field>
              <Field label={S.reg.pan}>{(id) => <Input id={id} value={form.pan} onChange={(e) => set('pan', e.target.value.toUpperCase().slice(0, 10))} autoCapitalize="characters" />}</Field>
              <Field label={S.reg.phone} error={errors.phone}>
                {(id, invalid) => <Input id={id} invalid={invalid} value={form.phone} onChange={(e) => set('phone', e.target.value.replace(/\D/g, '').slice(0, 10))} inputMode="numeric" />}
              </Field>
              <Field label={S.reg.email}>{(id) => <Input id={id} type="email" value={form.email} onChange={(e) => set('email', e.target.value)} />}</Field>
            </>
          )}

          {step === 2 && (
            <>
              <Field label={S.reg.line1} error={errors.line1}>
                {(id, invalid) => <Input id={id} invalid={invalid} value={form.line1} onChange={(e) => set('line1', e.target.value)} autoComplete="address-line1" />}
              </Field>
              <Field label={S.reg.line2}>{(id) => <Input id={id} value={form.line2} onChange={(e) => set('line2', e.target.value)} />}</Field>
              <Field label={S.reg.state}>
                {(id) => (
                  <Select id={id} value={form.stateId} onChange={(e) => { set('stateId', e.target.value); set('cityId', '') }}>
                    {(states.data ?? []).map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
                  </Select>
                )}
              </Field>
              <Field label={S.reg.city} error={errors.cityId}>
                {(id, invalid) => (
                  <Select id={id} invalid={invalid} value={form.cityId} onChange={(e) => set('cityId', e.target.value)}>
                    <option value="">{S.reg.chooseCity}</option>
                    {cityOptions.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
                  </Select>
                )}
              </Field>
              <Field label={S.reg.pincode} error={errors.pincode}>
                {(id, invalid) => <Input id={id} invalid={invalid} value={form.pincode} onChange={(e) => set('pincode', e.target.value.replace(/\D/g, '').slice(0, 6))} inputMode="numeric" autoComplete="postal-code" />}
              </Field>
            </>
          )}

          {step === 3 && (
            <>
              <p className="text-mute">{S.reg.documentsSub}</p>
              {docTypes.map(({ type, required }) => (
                <div key={type} className="rounded-2xl border border-line p-4">
                  <div className="flex items-center gap-3">
                    <span className={cn('flex size-11 shrink-0 items-center justify-center rounded-xl', files[type] ? 'bg-leaf-100 text-leaf-700' : 'bg-navy-50 text-navy-800')}>
                      {files[type] ? <Check className="size-5" /> : <FileText className="size-5" />}
                    </span>
                    <div className="min-w-0 flex-1">
                      <p className="font-bold text-navy-900">{S.docType[type]}{required && ' *'}</p>
                      <p className="truncate text-sm text-mute">{files[type]?.name ?? ' '}</p>
                    </div>
                    <label className="inline-flex min-h-10 cursor-pointer items-center rounded-xl border border-navy-900/25 px-4 text-sm font-semibold text-navy-900 focus-within:outline-2 focus-within:outline-leaf-600">
                      {files[type] ? S.reg.changeFile : S.reg.chooseFile}
                      <input type="file" accept="image/*,application/pdf" className="sr-only" onChange={(e) => pick(type, e.target.files?.[0])} />
                    </label>
                  </div>
                  {errors[type] && <p role="alert" className="mt-2 text-sm font-medium text-bad">{errors[type]}</p>}
                </div>
              ))}
              {submitError && <p role="alert" className="rounded-xl bg-bad-bg p-3 text-sm font-semibold text-bad">{submitError}</p>}
            </>
          )}
        </div>

        <div className="mt-8 flex gap-3">
          {step > 0 && <Button variant="outline" onClick={() => setStep(step - 1)} disabled={submit.isPending}>{S.app.back}</Button>}
          <Button className="flex-1" onClick={next} loading={submit.isPending}>{step === 3 ? S.reg.submit : S.app.next}</Button>
        </div>
      </div>
    </div>
  )
}
