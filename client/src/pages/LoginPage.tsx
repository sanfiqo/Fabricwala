import { useEffect, useState, type FormEvent } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router-dom'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { authApi } from '@/api/endpoints'
import { USE_MOCKS } from '@/api/client'
import { useMe, homeFor } from '@/auth'
import { BrandMark } from '@/components/AppShell'
import { Button } from '@/components/ui/button'
import { Field, Input } from '@/components/ui/field'
import { PageLoading } from '@/components/ui/misc'
import { fmt, S } from '@/i18n'

const RESEND_SECONDS = 30

const demoNumbers = [
  ['9000000001', S.login.demoRetailer],
  ['9000000002', S.login.demoManufacturer],
  ['9000000003', S.login.demoAdmin],
  ['9000000004', S.login.demoNew],
  ['9000000005', S.login.demoPending],
]

export default function LoginPage() {
  const { data: me, isLoading } = useMe()
  const qc = useQueryClient()
  const nav = useNavigate()
  const from = (useLocation().state as { from?: string } | null)?.from

  const [phone, setPhone] = useState('')
  const [code, setCode] = useState('')
  const [step, setStep] = useState<'phone' | 'otp'>('phone')
  const [error, setError] = useState('')
  const [wait, setWait] = useState(0)

  useEffect(() => {
    if (wait <= 0) return
    const t = setTimeout(() => setWait((w) => w - 1), 1000)
    return () => clearTimeout(t)
  }, [wait])

  const request = useMutation({
    mutationFn: () => authApi.requestOtp(phone),
    onSuccess: () => {
      setStep('otp')
      setCode('')
      setWait(RESEND_SECONDS)
      setError('')
    },
    onError: (e) => setError(e.message),
  })
  const verify = useMutation({
    mutationFn: () => authApi.verifyOtp(phone, code),
    onSuccess: async (user) => {
      qc.clear()
      // Re-read the session from the server so the cookie-based state is the source of truth.
      const fresh = await authApi.me().catch(() => user)
      qc.setQueryData(['me'], fresh)
      nav(from && from !== '/login' && homeFor(fresh) !== '/register' ? from : homeFor(fresh), { replace: true })
    },
    onError: (e) => setError(e.message),
  })

  if (isLoading) return <PageLoading />
  if (me) return <Navigate to={homeFor(me)} replace />

  const onPhone = (e: FormEvent) => {
    e.preventDefault()
    if (!/^[6-9]\d{9}$/.test(phone) && !(USE_MOCKS && /^\d{10}$/.test(phone))) return setError(S.login.invalidMobile)
    setError('')
    request.mutate()
  }
  const onOtp = (e: FormEvent) => {
    e.preventDefault()
    if (!/^\d{6}$/.test(code)) return setError(S.login.invalidOtp)
    setError('')
    verify.mutate()
  }

  return (
    <div className="mx-auto flex min-h-dvh max-w-md flex-col bg-white">
      <div className="relative overflow-hidden bg-navy-900 px-6 pb-14 pt-12 text-white">
        {/* woven-cloth texture */}
        <svg className="absolute inset-0 size-full opacity-[.07]" aria-hidden>
          <defs>
            <pattern id="weave" width="14" height="14" patternUnits="userSpaceOnUse" patternTransform="rotate(45)">
              <rect width="7" height="14" fill="#fff" />
            </pattern>
          </defs>
          <rect width="100%" height="100%" fill="url(#weave)" />
        </svg>
        <div className="relative">
          <BrandMark className="size-12 bg-white/10" />
          <h1 className="mt-6 text-3xl font-extrabold leading-tight">{S.app.name}</h1>
          <p className="mt-2 text-lg text-white/80">{S.app.tagline}</p>
        </div>
        <div className="absolute inset-x-0 bottom-0 h-1.5 bg-[repeating-linear-gradient(90deg,#2f9e6b_0_14px,transparent_14px_24px)]" />
      </div>

      <div className="flex-1 px-6 py-8">
        {step === 'phone' ? (
          <form onSubmit={onPhone} noValidate className="space-y-5">
            <div>
              <h2 className="text-2xl font-extrabold text-navy-900">{S.login.title}</h2>
              <p className="mt-1 text-mute">{S.login.sub}</p>
            </div>
            <Field label={S.login.mobile} hint={S.login.mobileHint} error={error}>
              {(id, invalid) => (
                <div className="flex gap-2">
                  <span className="flex min-h-12 items-center rounded-xl bg-navy-50 px-4 font-bold text-navy-900">+91</span>
                  <Input
                    id={id}
                    invalid={invalid}
                    value={phone}
                    onChange={(e) => setPhone(e.target.value.replace(/\D/g, '').slice(0, 10))}
                    inputMode="numeric"
                    autoComplete="tel-national"
                    placeholder="98765 43210"
                    autoFocus
                  />
                </div>
              )}
            </Field>
            <Button type="submit" block loading={request.isPending}>
              {S.login.sendOtp}
            </Button>
            {USE_MOCKS && (
              <div className="rounded-2xl border border-dashed border-leaf-600/50 bg-leaf-50 p-4">
                <p className="mb-2 text-sm font-bold text-leaf-700">{S.login.demoHelp}</p>
                <div className="flex flex-wrap gap-2">
                  {demoNumbers.map(([n, l]) => (
                    <button key={n} type="button" onClick={() => setPhone(n)} className="min-h-10 rounded-full border border-leaf-600/30 bg-white px-3 text-sm font-semibold text-navy-900">
                      {l} · {n.slice(-2)}
                    </button>
                  ))}
                </div>
              </div>
            )}
          </form>
        ) : (
          <form onSubmit={onOtp} noValidate className="space-y-5">
            <div>
              <h2 className="text-2xl font-extrabold text-navy-900">{S.login.otpTitle}</h2>
              <p className="mt-1 text-mute">{fmt(S.login.otpSub, { phone: `+91 ${phone}` })}</p>
            </div>
            <Field label={S.login.otp} error={error}>
              {(id, invalid) => (
                <Input
                  id={id}
                  invalid={invalid}
                  value={code}
                  onChange={(e) => setCode(e.target.value.replace(/\D/g, '').slice(0, 6))}
                  inputMode="numeric"
                  autoComplete="one-time-code"
                  placeholder="••••••"
                  className="text-center text-2xl font-bold tracking-[.5em]"
                  autoFocus
                />
              )}
            </Field>
            <Button type="submit" block loading={verify.isPending}>
              {S.login.verify}
            </Button>
            <div className="flex items-center justify-between">
              <Button type="button" variant="ghost" size="sm" onClick={() => { setStep('phone'); setError('') }}>
                {S.login.change}
              </Button>
              <Button type="button" variant="ghost" size="sm" disabled={wait > 0} loading={request.isPending} onClick={() => request.mutate()}>
                {wait > 0 ? fmt(S.login.resendIn, { s: wait }) : S.login.resend}
              </Button>
            </div>
          </form>
        )}
      </div>
    </div>
  )
}
