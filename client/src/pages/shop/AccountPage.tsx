import { useNavigate } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import { LogOut } from 'lucide-react'
import { authApi } from '@/api/endpoints'
import { setUseMocks, USE_MOCKS } from '@/api/client'
import { useMe } from '@/auth'
import { Button } from '@/components/ui/button'
import { Badge, Card, PageHeader, Segmented } from '@/components/ui/misc'
import { clearSavedData } from '@/queryClient'
import { label, LANGS, S, setLang, useLang } from '@/i18n'

/** Language, business details, demo-mode switch and log out. */
export default function AccountPage() {
  const { data: me } = useMe()
  const lang = useLang()
  const qc = useQueryClient()
  const nav = useNavigate()
  const status = me?.business?.verificationStatus

  const logout = async () => {
    try {
      await authApi.logout()
    } finally {
      qc.clear()
      clearSavedData()
      nav('/login', { replace: true })
    }
  }

  return (
    <div className="pb-4">
      <PageHeader title={S.account.title} />
      <div className="space-y-5 px-5 pt-4">
        <Card className="p-4">
          <p className="text-xs text-mute">{S.account.business}</p>
          <p className="mt-0.5 break-words text-lg font-semibold">{me?.business?.displayName ?? me?.fullName ?? '—'}</p>
          <div className="mt-3 flex flex-wrap items-center justify-between gap-2 text-sm">
            <span className="text-mute">{S.account.phone}</span>
            <span className="font-medium tabular-nums">{me?.phone}</span>
          </div>
          {status && (
            <div className="mt-2 flex flex-wrap items-center justify-between gap-2 text-sm">
              <span className="text-mute">{S.account.status}</span>
              <Badge tone={status === 'Approved' ? 'good' : status === 'Pending' ? 'warn' : 'bad'}>{label(S.status, status)}</Badge>
            </div>
          )}
        </Card>

        <section>
          <h2 className="font-semibold">{S.account.language}</h2>
          <p className="mb-2 text-sm text-mute">{S.account.languageSub}</p>
          <Segmented value={lang} onChange={setLang} options={LANGS.map((l) => ({ value: l, label: S.lang[l] }))} />
        </section>

        <section>
          <h2 className="font-semibold">{S.account.demo}</h2>
          <p className="mb-2 text-sm text-mute">{S.account.demoSub}</p>
          <Segmented
            value={USE_MOCKS ? 'on' : 'off'}
            onChange={(v) => {
              if ((v === 'on') !== USE_MOCKS) setUseMocks(v === 'on')
            }}
            options={[
              { value: 'on', label: S.account.demoOn },
              { value: 'off', label: S.account.demoOff },
            ]}
          />
        </section>

        <Button variant="soft" block onClick={() => void logout()}>
          <LogOut className="size-5" aria-hidden />
          {S.account.logout}
        </Button>
      </div>
    </div>
  )
}
