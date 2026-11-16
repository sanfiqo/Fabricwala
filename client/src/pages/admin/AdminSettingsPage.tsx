import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { adminApi } from '@/api/endpoints'
import type { PlatformSettings } from '@/api/types'
import { Button } from '@/components/ui/button'
import { Field, Input } from '@/components/ui/field'
import { ErrorState, PageHeader, PageLoading } from '@/components/ui/misc'
import { useToast } from '@/components/ui/toast'
import { S } from '@/i18n'

function Form({ initial }: { initial: PlatformSettings }) {
  const qc = useQueryClient()
  const toast = useToast()
  const [rate, setRate] = useState(String(initial.commissionDefaultRate))
  const [days, setDays] = useState(String(initial.settlementAutoReleaseDays))
  const [errors, setErrors] = useState<{ rate?: string; days?: string }>({})

  const save = useMutation({
    mutationFn: (s: PlatformSettings) => adminApi.saveSettings(s),
    onSuccess: (s) => {
      qc.setQueryData(['admin-settings'], s)
      toast.success(S.seller.saved)
    },
    onError: (e) => toast.error(e.message),
  })

  const submit = () => {
    const r = Number(rate)
    const d = Number(days)
    const e: typeof errors = {}
    if (rate.trim() === '' || Number.isNaN(r) || r < 0 || r > 50) e.rate = '0 – 50'
    if (!Number.isInteger(d) || d < 1 || d > 90) e.days = '1 – 90'
    setErrors(e)
    if (!e.rate && !e.days) save.mutate({ commissionDefaultRate: r, settlementAutoReleaseDays: d })
  }

  return (
    <div className="space-y-5 px-4 pb-8 pt-5">
      <Field label={S.admin.commission} hint={S.admin.commissionHint} error={errors.rate}>
        {(id, inv) => <Input id={id} invalid={inv} inputMode="decimal" value={rate} onChange={(e) => setRate(e.target.value.replace(/[^\d.]/g, ''))} />}
      </Field>
      <Field label={S.admin.settlementDays} hint={S.admin.settlementHint} error={errors.days}>
        {(id, inv) => <Input id={id} invalid={inv} inputMode="numeric" value={days} onChange={(e) => setDays(e.target.value.replace(/\D/g, ''))} />}
      </Field>
      <Button block variant="accent" loading={save.isPending} onClick={submit}>{S.app.save}</Button>
    </div>
  )
}

export default function AdminSettingsPage() {
  const q = useQuery({ queryKey: ['admin-settings'], queryFn: adminApi.settings })
  return (
    <div>
      <PageHeader title={S.admin.settingsTitle} />
      {q.isLoading ? <PageLoading /> : q.error || !q.data ? <ErrorState error={q.error} onRetry={() => void q.refetch()} /> : <Form initial={q.data} />}
    </div>
  )
}
