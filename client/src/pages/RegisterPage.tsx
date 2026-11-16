import { useEffect, useMemo, useRef, useState } from 'react'
import { Navigate, useNavigate } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Controller, useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { ArrowLeft, Check, FileText, Factory, Store } from 'lucide-react'
import { businessApi, masterApi } from '@/api/endpoints'
import type { BusinessType, DocType } from '@/api/types'
import { homeFor, useMe } from '@/auth'
import { Button } from '@/components/ui/button'
import { Field, Input, Select } from '@/components/ui/field'
import { PageLoading } from '@/components/ui/misc'
import { useDebounced } from '@/hooks'
import { fmt, S, useLang } from '@/i18n'
import { cn } from '@/lib/utils'
import { clearDraft, loadDraft, useSaveDraft } from '@/lib/useDraft'

const GSTIN = /^\d{2}[A-Z]{5}\d{4}[A-Z][1-9A-Z]Z[0-9A-Z]$/
const MAX_FILE = 5 * 1024 * 1024
const DRAFT = 'register'

interface Values {
  type: BusinessType
  displayName: string
  legalName: string
  pincode: string
  stateId: string
  cityId: string
  line1: string
  gstin: string
}

export default function RegisterPage() {
  const { data: me, isLoading } = useMe()
  const qc = useQueryClient()
  const nav = useNavigate()
  const lang = useLang()
  const [step, setStep] = useState<1 | 2>(() => (loadDraft<{ step: 1 | 2 }>(DRAFT).step ?? 1))
  const [files, setFiles] = useState<Partial<Record<DocType, File>>>({})
  const [fileErrors, setFileErrors] = useState<Partial<Record<DocType, string>>>({})
  const [submitError, setSubmitError] = useState('')
  // Remember what already succeeded so a retry after a network failure does not create duplicates.
  const done = useRef({ business: false, address: false, docs: new Set<DocType>() })

  const schema = useMemo(
    () =>
      z.object({
        type: z.enum(['Retailer', 'Manufacturer']),
        displayName: z.string().trim().min(1, S.reg.required),
        legalName: z.string().trim(),
        pincode: z.string().regex(/^\d{6}$/, S.reg.pincodeInvalid),
        stateId: z.string(),
        cityId: z.string().min(1, S.reg.required),
        line1: z.string().trim().min(1, S.reg.required),
        gstin: z.string().refine((v) => v === '' || GSTIN.test(v.toUpperCase()), S.reg.gstWrong),
      }),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [lang],
  )
  const draft = useMemo(() => loadDraft<Values>(DRAFT), [])
  const form = useForm<Values>({
    resolver: zodResolver(schema),
    mode: 'onBlur',
    defaultValues: { type: 'Retailer', displayName: '', legalName: '', pincode: '', stateId: '', cityId: '', line1: '', gstin: '', ...draft },
  })
  const v = form.watch()
  useSaveDraft(DRAFT, { ...v, step })

  // Pincode fills city and state by itself. If the lookup does not know it, the person picks them.
  const pin = useDebounced(v.pincode, 250)
  const lookup = useQuery({ queryKey: ['pincode', pin], queryFn: () => businessApi.pincode(pin), enabled: /^\d{6}$/.test(pin), retry: false, staleTime: Infinity })
  const states = useQuery({ queryKey: ['states'], queryFn: masterApi.states })
  const cities = useQuery({ queryKey: ['cities'], queryFn: () => masterApi.cities() })
  const found = lookup.data
  const manual = lookup.isError || (/^\d{6}$/.test(pin) && !lookup.isFetching && !found)
  // Set once per pincode (the lookup result is the source of truth until the person edits).
  const lastApplied = useRef('')
  const setValue = form.setValue
  useEffect(() => {
    if (found && lastApplied.current !== found.pincode) {
      lastApplied.current = found.pincode
      setValue('cityId', String(found.cityId))
      setValue('stateId', String(found.stateId))
    }
  }, [found, setValue])
  const cityOptions = (cities.data ?? []).filter((c) => String(c.stateId) === v.stateId)

  const docTypes: Array<{ type: DocType }> = [{ type: 'GST' }, { type: 'PAN' }, { type: v.type === 'Retailer' ? 'ShopLicence' : 'Udyam' }]

  const submit = useMutation({
    mutationFn: async (values: Values) => {
      if (!done.current.business) {
        await businessApi.create({
          type: values.type,
          legalName: values.legalName.trim() || values.displayName.trim(),
          displayName: values.displayName.trim(),
          gstin: values.gstin.trim().toUpperCase(),
          phone: me?.phone ?? '',
          pincode: values.pincode,
        })
        done.current.business = true
      }
      if (!done.current.address) {
        await businessApi.addAddress({ addressType: values.type === 'Retailer' ? 'Delivery' : 'Pickup', line1: values.line1.trim(), cityId: Number(values.cityId), pincode: values.pincode, isDefault: true })
        done.current.address = true
      }
      for (const [type, file] of Object.entries(files) as Array<[DocType, File]>) {
        if (done.current.docs.has(type)) continue
        await businessApi.addDocument(type, file)
        done.current.docs.add(type)
      }
    },
    onSuccess: async () => {
      clearDraft(DRAFT)
      await qc.invalidateQueries({ queryKey: ['me'] })
      nav('/pending', { replace: true })
    },
    onError: (e) => setSubmitError(e.message),
  })

  if (isLoading) return <PageLoading />
  if (me?.business && !submit.isPending && !submit.isError) return <Navigate to={homeFor(me)} replace />
  if (me?.roles.includes('Admin')) return <Navigate to={homeFor(me)} replace />

  const pick = (type: DocType, file: File | undefined) => {
    if (!file) return
    if (file.size > MAX_FILE) return setFileErrors((e) => ({ ...e, [type]: S.reg.tooBig }))
    setFiles((f) => ({ ...f, [type]: file }))
    setFileErrors((e) => ({ ...e, [type]: undefined }))
  }

  const goDocs = form.handleSubmit(() => setStep(2))
  const send = form.handleSubmit((values) => {
    setSubmitError('')
    submit.mutate(values)
  })

  return (
    <div className="pb-8">
      <div className="flex items-center justify-between px-4 pt-[18px]">
        {step === 2 ? (
          <button type="button" aria-label={S.app.back} onClick={() => setStep(1)} className="press flex size-12 items-center justify-center rounded-full bg-soft">
            <ArrowLeft className="size-[22px]" aria-hidden />
          </button>
        ) : (
          <span className="size-12" />
        )}
        <span className="pr-1.5 text-sm text-mute">{fmt(S.reg.stepOf, { n: step, total: 2 })}</span>
      </div>
      <div role="img" aria-label={fmt(S.reg.stepOf, { n: step, total: 2 })} className="mx-5 mt-1 grid grid-cols-2 gap-1">
        <span className="h-1.5 rounded-full bg-ink" />
        <span className={cn('h-1.5 rounded-full', step === 2 ? 'bg-ink' : 'bg-line')} />
      </div>

      {step === 1 ? (
        <form onSubmit={(e) => void goDocs(e)} noValidate className="space-y-[18px] px-5 pt-5">
          <h1 className="text-[30px] font-semibold leading-[1.15] tracking-[-0.02em]">{S.reg.tellUs}</h1>

          <div>
            <span id="type-label" className="mb-2 block text-sm font-medium">{S.reg.iWant}</span>
            <Controller
              control={form.control}
              name="type"
              render={({ field }) => (
                <div role="radiogroup" aria-labelledby="type-label" className="flex gap-2.5">
                  {([
                    ['Retailer', S.reg.buyForShop, S.reg.retailer, Store],
                    ['Manufacturer', S.reg.sellMine, S.reg.manufacturer, Factory],
                  ] as const).map(([val, title, sub, Icon]) => {
                    const on = field.value === val
                    return (
                      <button
                        key={val}
                        type="button"
                        role="radio"
                        aria-checked={on}
                        onClick={() => field.onChange(val)}
                        className={cn('press relative flex min-h-[112px] flex-1 flex-col items-start justify-end gap-0.5 rounded-card border bg-surface p-3.5 text-left', on ? 'border-2 border-ink' : 'border-line')}
                      >
                        {on && (
                          <span className="absolute right-3 top-3 flex size-[22px] items-center justify-center rounded-full bg-ink text-background" aria-hidden>
                            <Check className="size-3.5" />
                          </span>
                        )}
                        <Icon className="mb-2 size-6" aria-hidden />
                        <span className="text-[15px] font-semibold">{title}</span>
                        <span className="text-[13px] text-mute">{sub}</span>
                      </button>
                    )
                  })}
                </div>
              )}
            />
          </div>

          <Field label={S.reg.shopName} error={form.formState.errors.displayName?.message}>
            {(id, invalid) => <Input id={id} invalid={invalid} autoComplete="organization" {...form.register('displayName')} />}
          </Field>
          <Field label={S.reg.legalOptional} hint={S.reg.legalHint}>
            {(id) => <Input id={id} {...form.register('legalName')} />}
          </Field>

          <Field label={S.reg.pincodeShop} error={form.formState.errors.pincode?.message}>
            {(id, invalid) => (
              <div className="relative">
                <Input
                  id={id}
                  invalid={invalid}
                  inputMode="numeric"
                  autoComplete="postal-code"
                  maxLength={6}
                  className="pr-12 tracking-[0.05em] tabular-nums"
                  {...form.register('pincode', { onChange: (e) => form.setValue('pincode', e.target.value.replace(/\D/g, '').slice(0, 6)) })}
                />
                {found && (
                  <span className="absolute right-3.5 top-1/2 flex size-6 -translate-y-1/2 items-center justify-center rounded-full bg-success text-white" aria-hidden>
                    <Check className="size-4" />
                  </span>
                )}
              </div>
            )}
          </Field>
          {found && <p className="-mt-3 text-sm" aria-live="polite">{found.cityName}, {found.stateName}</p>}
          {manual && (
            <div className="-mt-1 space-y-3" aria-live="polite">
              <p className="text-sm text-mute">{S.reg.pincodeNotFound}</p>
              <Field label={S.reg.state}>
                {(id) => (
                  <Select id={id} {...form.register('stateId', { onChange: () => form.setValue('cityId', '') })}>
                    <option value="">{S.reg.chooseState}</option>
                    {(states.data ?? []).map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
                  </Select>
                )}
              </Field>
              <Field label={S.reg.city} error={form.formState.errors.cityId?.message}>
                {(id, invalid) => (
                  <Select id={id} invalid={invalid} {...form.register('cityId')}>
                    <option value="">{S.reg.chooseCity}</option>
                    {cityOptions.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
                  </Select>
                )}
              </Field>
            </div>
          )}

          <Field label={S.reg.shopAddress} error={form.formState.errors.line1?.message}>
            {(id, invalid) => <Input id={id} invalid={invalid} autoComplete="address-line1" {...form.register('line1')} />}
          </Field>

          <Field label={S.reg.gstOptional} hint={S.reg.gstHelp} error={form.formState.errors.gstin?.message}>
            {(id, invalid) => (
              <Input
                id={id}
                invalid={invalid}
                autoCapitalize="characters"
                maxLength={15}
                className="tracking-[0.04em]"
                {...form.register('gstin', { onChange: (e) => form.setValue('gstin', e.target.value.toUpperCase().slice(0, 15)) })}
              />
            )}
          </Field>

          <Button type="submit" size="lg" block>
            {S.app.next}
          </Button>
        </form>
      ) : (
        <div className="space-y-4 px-5 pt-5">
          <h1 className="text-[30px] font-semibold leading-[1.15] tracking-[-0.02em]">{S.reg.docsTitle}</h1>
          <p className="text-mute">{S.reg.docsOptional}</p>
          {docTypes.map(({ type }) => (
            <div key={type} className="rounded-card border border-line bg-surface p-4">
              <div className="flex items-center gap-3">
                <span className={cn('flex size-11 shrink-0 items-center justify-center rounded-full', files[type] ? 'bg-success text-white' : 'bg-soft')}>
                  {files[type] ? <Check className="size-5" aria-hidden /> : <FileText className="size-5" aria-hidden />}
                </span>
                <div className="min-w-0 flex-1">
                  <p className="font-medium">{S.docType[type]}</p>
                  <p className="truncate text-sm text-mute">{files[type]?.name ?? ' '}</p>
                </div>
                <label className="press inline-flex min-h-11 cursor-pointer items-center rounded-full bg-soft px-4 text-sm font-medium focus-within:outline-2 focus-within:outline-ink">
                  {files[type] ? S.reg.changeFile : S.reg.chooseFile}
                  <input type="file" accept="image/*,application/pdf" className="sr-only" onChange={(e) => pick(type, e.target.files?.[0])} />
                </label>
              </div>
              {fileErrors[type] && <p role="alert" className="mt-2 text-sm font-medium text-error">{fileErrors[type]}</p>}
            </div>
          ))}
          {submitError && <p role="alert" className="rounded-2xl bg-error-bg p-3 text-sm font-medium text-error">{submitError}</p>}
          <Button size="lg" block onClick={() => void send()} loading={submit.isPending}>
            {Object.keys(files).length > 0 ? S.reg.submit : S.reg.skipDocs}
          </Button>
        </div>
      )}
    </div>
  )
}
