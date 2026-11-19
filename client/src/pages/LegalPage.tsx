import { Link, useParams } from 'react-router-dom'
import { FileText } from 'lucide-react'
import { Badge, EmptyState, LinkButton, PageHeader } from '@/components/ui/misc'
import { S } from '@/i18n'

type DocKey = 'privacy' | 'terms' | 'seller-agreement' | 'refund'

/** What each page will cover. Placeholder outline only: the real text needs a lawyer (marked [LEGAL REVIEW NEEDED]). */
const DOCS: Record<DocKey, { title: () => string; sections: string[] }> = {
  privacy: {
    title: () => S.legal.privacy,
    sections: ['What we collect: mobile number, business and GST details, addresses', 'How we use it', 'Who we share it with: sellers, payment and delivery partners', 'How long we keep it', 'Your choices and how to contact us'],
  },
  terms: {
    title: () => S.legal.terms,
    sections: ['Who can use the app: verified businesses only', 'Orders, prices and GST', 'Payments and commission', 'Cancellations and disputes', 'Suspending an account', 'Changes to these terms'],
  },
  'seller-agreement': {
    title: () => S.legal.sellerAgreement,
    sections: ['Listing rules and verification', 'Commission and settlement', 'Dispatch time and the acceptance window', 'Returns and claims', 'Ending the agreement'],
  },
  refund: {
    title: () => S.legal.refund,
    sections: ['When you can return or claim', 'How to report a problem with photos', 'How refunds are paid', 'Time limits'],
  },
}
const ORDER: DocKey[] = ['privacy', 'terms', 'seller-agreement', 'refund']
const isDoc = (v: string | undefined): v is DocKey => !!v && v in DOCS

/** Public legal pages (no login needed). One screen for all four documents: /legal/privacy, /legal/terms, /legal/seller-agreement, /legal/refund. */
export default function LegalPage() {
  const { doc } = useParams()
  if (!isDoc(doc)) {
    return <EmptyState title={S.legal.notFound} action={<LinkButton to="/">{S.app.goHome}</LinkButton>} />
  }
  const d = DOCS[doc]

  return (
    <div className="mx-auto max-w-xl pb-10">
      <PageHeader title={d.title()} back />
      <div className="space-y-5 px-4 pt-4">
        <div className="rounded-card bg-peach-soft p-4">
          <Badge tone="warn">{S.legal.marker}</Badge>
          <p className="mt-2 text-[15px] text-saving">{S.legal.placeholder}</p>
        </div>

        <section>
          <h2 className="mb-2 text-sm font-semibold text-mute">{S.legal.sectionsTitle}</h2>
          <ol className="space-y-2">
            {d.sections.map((s, i) => (
              <li key={s} className="flex gap-3 rounded-card border border-line bg-surface px-4 py-3 text-[15px]">
                <span className="mt-0.5 text-mute tabular-nums">{i + 1}</span>
                <span className="min-w-0 break-words">{s}</span>
              </li>
            ))}
          </ol>
        </section>

        <nav aria-label={S.legal.title} className="flex flex-wrap gap-2 pt-2">
          {ORDER.filter((k) => k !== doc).map((k) => (
            <Link key={k} to={`/legal/${k}`} viewTransition className="press inline-flex min-h-11 items-center gap-1.5 rounded-chip border border-line bg-surface px-4 text-sm">
              <FileText className="size-4 text-mute" aria-hidden />
              {DOCS[k].title()}
            </Link>
          ))}
        </nav>
      </div>
    </div>
  )
}
