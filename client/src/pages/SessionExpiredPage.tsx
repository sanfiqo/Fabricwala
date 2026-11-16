import { LogIn } from 'lucide-react'
import { LinkButton } from '@/components/ui/misc'
import { S } from '@/i18n'

/** Shown when the server says the session is over (401) for someone who was logged in. */
export default function SessionExpiredPage() {
  return (
    <main className="mx-auto flex min-h-dvh max-w-xl flex-col items-center justify-center px-6 text-center">
      <span className="mb-4 flex size-16 items-center justify-center rounded-full bg-soft" aria-hidden>
        <LogIn className="size-8" />
      </span>
      <h1 className="text-2xl font-semibold">{S.session.title}</h1>
      <p className="mt-2 max-w-sm text-mute">{S.session.body}</p>
      <LinkButton to="/login" className="mt-6 min-w-48">
        {S.session.action}
      </LinkButton>
    </main>
  )
}
