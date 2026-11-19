import { ChevronDown, MessageCircle } from 'lucide-react'
import { PageHeader } from '@/components/ui/misc'
import { S } from '@/i18n'
import { waLink } from '@/lib/whatsapp'

const FAQ = [1, 2, 3, 4, 5, 6, 7, 8] as const

/** Help Center: common questions (opens and closes) and a WhatsApp button. Route: /help (no login needed). */
export default function HelpPage() {
  const h = S.help as Record<string, string>
  return (
    <div className="mx-auto max-w-xl pb-10">
      <PageHeader title={S.help.title} back />
      <p className="mx-5 mt-1 text-[15px] text-mute">{S.help.sub}</p>

      <h2 className="mx-5 mt-6 text-[13px] font-medium uppercase tracking-wide text-mute">{S.help.faq}</h2>
      <div className="mx-5 mt-2 divide-y divide-line overflow-hidden rounded-card border border-line bg-surface">
        {FAQ.map((n) => (
          <details key={n} className="group">
            <summary className="press flex min-h-[56px] cursor-pointer list-none items-center gap-3 px-4 py-3 text-[15px] font-medium [&::-webkit-details-marker]:hidden">
              <span className="min-w-0 flex-1">{h[`q${n}`]}</span>
              <ChevronDown className="size-4 shrink-0 text-mute transition-transform group-open:rotate-180" aria-hidden />
            </summary>
            <p className="px-4 pb-4 text-[14px] leading-relaxed text-mute">{h[`a${n}`]}</p>
          </details>
        ))}
      </div>

      <h2 className="mx-5 mt-6 text-[13px] font-medium uppercase tracking-wide text-mute">{S.help.contact}</h2>
      <a
        href={waLink(S.help.waMsg)}
        target="_blank"
        rel="noreferrer"
        className="press mx-5 mt-2 flex min-h-12 items-center justify-center gap-2 rounded-pill bg-brand px-5 font-medium text-white shadow-raised"
      >
        <MessageCircle className="size-5" aria-hidden />
        {S.help.whatsapp}
      </a>
    </div>
  )
}
