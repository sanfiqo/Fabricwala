import { useEffect, useMemo, useState } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router-dom'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { ArrowLeft, CircleAlert, IndianRupee, Package, Scissors, Shirt, Store, Truck } from 'lucide-react'
import { authApi } from '@/api/endpoints'
import { USE_MOCKS } from '@/api/client'
import { useMe, homeFor } from '@/auth'
import { Button } from '@/components/ui/button'
import { Field } from '@/components/ui/field'
import { OtpInput } from '@/components/ui/otp'
import { PhoneInput } from '@/components/ui/phone'
import { PageLoading } from '@/components/ui/misc'
import { fmt, S, setLang, useLang } from '@/i18n'
import { clearDraft, loadDraft, useSaveDraft } from '@/lib/useDraft'

const RESEND_SECONDS = 30
const DRAFT = 'login'
const tiles = [Shirt, Scissors, Package, Truck, Store, IndianRupee]

const demoNumbers = () => [
  ['9000000001', S.login.demoRetailer],
  ['9000000002', S.login.demoManufacturer],
  ['9000000003', S.login.demoAdmin],
  ['9000000004', S.login.demoNew],
  ['9000000005', S.login.demoPending],
]

const mmss = (s: number) => `${Math.floor(s / 60)}:${String(s % 60).padStart(2, '0')}`

export default function LoginPage() {
  const { data: me, isLoading } = useMe()
  const qc = useQueryClient()
  const nav = useNavigate()
  const lang = useLang()
  const from = (useLocation().state as { from?: string } | null)?.from

  const saved = useMemo(() => loadDraft<{ phone: string; step: 'phone' | 'otp' }>(DRAFT), [])
  const [step, setStep] = useState<'phone' | 'otp'>(saved.step ?? 'phone')
  const [code, setCode] = useState('')
  const [codeError, setCodeError] = useState('')
  const [wait, setWait] = useState(saved.step === 'otp' ? RESEND_SECONDS : 0)

  // Validation text follows the language, so the schema is rebuilt when it changes.
  const schema = useMemo(
    () => z.object({ phone: z.string().regex(USE_MOCKS ? /^\d{10}$/ : /^[6-9]\d{9}$/, S.login.invalidMobile) }),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [lang],
  )
  const form = useForm<{ phone: string }>({ resolver: zodResolver(schema), defaultValues: { phone: saved.phone ?? '' } })
  const phone = form.watch('phone')
  useSaveDraft(DRAFT, { phone, step })

  useEffect(() => {
    if (wait <= 0) return
    const t = setTimeout(() => setWait((w) => w - 1), 1000)
    return () => clearTimeout(t)
  }, [wait])

  const request = useMutation({
    mutationFn: (p: string) => authApi.requestOtp(p),
    onSuccess: () => {
      setStep('otp')
      setCode('')
      setCodeError('')
      setWait(RESEND_SECONDS)
    },
    onError: (e) => form.setError('phone', { message: e.message }),
  })
  const verify = useMutation({
    mutationFn: (c: string) => authApi.verifyOtp(phone, c),
    onSuccess: async (user) => {
      qc.clear()
      clearDraft(DRAFT)
      // Re-read the session from the server so the cookie-based state is the source of truth.
      const fresh = await authApi.me().catch(() => user)
      qc.setQueryData(['me'], fresh)
      nav(from && from !== '/login' && homeFor(fresh) !== '/register' ? from : homeFor(fresh), { replace: true })
    },
    onError: (e) => setCodeError(e.message),
  })

  if (isLoading) return <PageLoading />
  if (me) return <Navigate to={homeFor(me)} replace />

  const otherLang = lang === 'en' ? 'hi' : 'en'

  if (step === 'otp') {
    return (
      <div className="mx-auto flex min-h-dvh max-w-md flex-col bg-background">
        <div className="px-4 pt-[18px]">
          <button
            type="button"
            aria-label={S.app.back}
            onClick={() => {
              setStep('phone')
              setCodeError('')
            }}
            className="press flex size-12 items-center justify-center rounded-full"
          >
            <ArrowLeft className="size-[22px]" aria-hidden />
          </button>
        </div>
        <div className="px-5 pt-2">
          <h1 className="text-[30px] font-semibold leading-[1.15] tracking-[-0.02em]">{S.login.otpTitle}</h1>
          <p className="mt-2 text-[15px] text-mute">
            {fmt(S.login.sentTo, { phone: `+91 ${phone.slice(0, 5)} ${phone.slice(5)}` })} ·{' '}
            <button type="button" onClick={() => setStep('phone')} className="min-h-11 font-medium text-ink underline">
              {S.app.change}
            </button>
          </p>
        </div>
        <form
          className="flex-1 px-5 pt-6"
          onSubmit={(e) => {
            e.preventDefault()
            if (code.length === 6) verify.mutate(code)
            else setCodeError(S.login.invalidOtp)
          }}
          noValidate
        >
          <OtpInput
            value={code}
            onChange={(v) => {
              setCode(v)
              setCodeError('')
            }}
            onComplete={(v) => verify.mutate(v)}
            invalid={!!codeError}
            autoFocus
          />
          {codeError && (
            <p role="alert" className="mt-3 flex items-start gap-2 text-sm font-medium text-error">
              <CircleAlert className="mt-0.5 size-4 shrink-0" aria-hidden />
              {codeError}
            </p>
          )}
          <div className="mt-4 flex items-center justify-between gap-3 text-sm text-mute">
            {'OTPCredential' in window ? (
              <span className="flex items-center gap-2">
                <span className="size-2 rounded-full bg-ink" aria-hidden />
                {S.login.smsAuto}
              </span>
            ) : (
              <span />
            )}
            <button
              type="button"
              disabled={wait > 0 || request.isPending}
              onClick={() => request.mutate(phone)}
              className="min-h-11 shrink-0 font-medium text-ink underline disabled:font-normal disabled:text-mute disabled:no-underline"
            >
              {wait > 0 ? fmt(S.login.resendTimer, { t: mmss(wait) }) : S.login.resend}
            </button>
          </div>
          <Button type="submit" size="lg" block className="mt-6" loading={verify.isPending}>
            {S.login.verify}
          </Button>
        </form>
      </div>
    )
  }

  return (
    <div className="mx-auto flex min-h-dvh max-w-md flex-col bg-background">
      <div className="flex items-center justify-between px-5 pt-[22px]">
        <span className="text-lg font-semibold tracking-[-0.01em]">{S.app.name}</span>
        <button
          type="button"
          onClick={() => setLang(otherLang)}
          className="press flex min-h-11 items-center rounded-full border border-line bg-surface px-3.5 text-sm"
        >
          {S.lang[otherLang]}
        </button>
      </div>

      <div aria-hidden className="mt-5 grid grid-cols-3 gap-px border-y border-line bg-line">
        {tiles.map((Icon, i) => (
          <span key={i} className="flex h-14 items-center justify-center bg-soft text-mute">
            <Icon className="size-6" strokeWidth={1.5} />
          </span>
        ))}
      </div>

      <div className="px-5 pt-6">
        <h1 className="text-balance text-[30px] font-semibold leading-[1.15] tracking-[-0.02em]">{S.login.heroTitle}</h1>
        <p className="mt-2 text-[15px] leading-relaxed text-mute">{S.login.heroSub}</p>
      </div>

      <form
        className="flex flex-1 flex-col px-5 pt-5"
        noValidate
        onSubmit={form.handleSubmit((v) => {
          request.mutate(v.phone)
        })}
      >
        <Field label={S.login.mobile} hint={S.login.smsHint} error={form.formState.errors.phone?.message}>
          {(id, invalid) => <PhoneInput id={id} invalid={invalid} autoFocus {...form.register('phone', { onChange: (e) => form.setValue('phone', e.target.value.replace(/\D/g, '').slice(0, 10)) })} />}
        </Field>

        {USE_MOCKS && (
          <div className="mt-4 rounded-2xl bg-soft p-3">
            <p className="mb-2 text-sm font-medium">{S.login.demoHelp}</p>
            <div className="flex flex-wrap gap-2">
              {demoNumbers().map(([n, l]) => (
                <button key={n} type="button" onClick={() => form.setValue('phone', n)} className="press min-h-11 rounded-full border border-line bg-surface px-3 text-sm">
                  {l} · {n.slice(-2)}
                </button>
              ))}
            </div>
          </div>
        )}

        <div className="mt-auto flex flex-col gap-3 pb-[max(22px,env(safe-area-inset-bottom))] pt-6">
          <Button type="submit" size="lg" block loading={request.isPending}>
            {S.login.sendOtp}
          </Button>
          <p className="text-center text-xs leading-relaxed text-mute">{S.login.terms}</p>
        </div>
      </form>
    </div>
  )
}
