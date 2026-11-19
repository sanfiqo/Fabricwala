import { useState, type ChangeEvent } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { CircleAlert, Download, FileSpreadsheet, Upload } from 'lucide-react'
import { BULK_TEMPLATE_URL, bulkApi } from '@/api/endpoints'
import { qk } from '@/api/keys'
import { Button } from '@/components/ui/button'
import { EmptyState, LinkButton, PageHeader, Spinner } from '@/components/ui/misc'
import { useToast } from '@/components/ui/toast'
import { fmt, S } from '@/i18n'

/** Add many products at once: download the CSV template, fill it, choose it, check the preview, import. Nothing is saved until Import. */
export default function BulkUploadPage() {
  const qc = useQueryClient()
  const toast = useToast()
  const [file, setFile] = useState<File | null>(null)
  const [done, setDone] = useState<number | null>(null)

  const preview = useMutation({ mutationFn: (f: File) => bulkApi.preview(f), onError: (e) => toast.error(e.message) })
  const run = useMutation({
    mutationFn: (f: File) => bulkApi.import(f),
    onSuccess: (r) => {
      setDone(r.imported)
      void qc.invalidateQueries({ queryKey: qk.sellerProducts })
    },
    onError: (e) => toast.error(e.message),
  })

  const onPick = (e: ChangeEvent<HTMLInputElement>) => {
    const f = e.target.files?.[0]
    e.target.value = '' // the same file can be chosen again after fixing it
    if (!f) return
    if (!/\.csv$/i.test(f.name)) return toast.error(S.bulk.csvOnly)
    setFile(f)
    preview.mutate(f)
  }

  if (done !== null) {
    return (
      <div>
        <PageHeader title={S.bulk.title} back="/seller/products" />
        <EmptyState icon={<FileSpreadsheet className="size-7" aria-hidden />} title={fmt(S.bulk.done, { n: done })} sub={S.bulk.doneSub} action={<LinkButton to="/seller/products">{S.bulk.toProducts}</LinkButton>} />
      </div>
    )
  }

  const p = preview.data
  const problems = p?.errors ?? []
  const rows = p?.rows ?? []
  const ready = !!file && !!p && problems.length === 0 && rows.length > 0

  return (
    <div className="pb-10">
      <PageHeader title={S.bulk.title} back="/seller/products" />
      <div className="space-y-5 px-4 pt-4">
        <p className="text-[15px] text-mute">{S.bulk.sub}</p>

        <section className="space-y-3 rounded-card border border-line bg-surface p-4">
          <h2 className="font-semibold">{S.bulk.step1}</h2>
          <p className="text-sm text-mute">{S.bulk.columns}</p>
          <a href={BULK_TEMPLATE_URL} download="products-template.csv" className="press inline-flex min-h-12 items-center gap-2 rounded-pill bg-surface px-5 font-medium shadow-[0_2px_10px_rgb(60_40_20/0.1)]">
            <Download className="size-5" aria-hidden />
            {S.bulk.template}
          </a>
        </section>

        <section className="space-y-3 rounded-card border border-line bg-surface p-4">
          <h2 className="font-semibold">{S.bulk.step2}</h2>
          <label className="press inline-flex min-h-12 cursor-pointer items-center gap-2 rounded-pill bg-brand px-6 font-medium text-white shadow-[0_8px_20px_rgb(14_90_67/0.28)] focus-within:outline-2 focus-within:outline-offset-2 focus-within:outline-brand">
            <Upload className="size-5" aria-hidden />
            {file ? S.bulk.change : S.bulk.choose}
            <input type="file" accept=".csv,text/csv" className="sr-only" onChange={onPick} />
          </label>
          {file && <p className="break-all text-sm text-mute">{file.name}</p>}

          {preview.isPending && (
            <p role="status" className="flex items-center gap-2 text-sm text-mute">
              <Spinner className="size-4" />
              {S.bulk.checking}
            </p>
          )}

          {problems.length > 0 && (
            <div role="alert" className="rounded-2xl bg-error-bg p-3 text-error">
              <p className="flex items-start gap-2 text-sm font-medium">
                <CircleAlert className="mt-0.5 size-4 shrink-0" aria-hidden />
                {fmt(S.bulk.problems, { n: problems.length })}
              </p>
              <ul className="mt-2 list-disc space-y-1 pl-9 text-sm">
                {problems.slice(0, 30).map((m, i) => (
                  <li key={i} className="break-words">
                    {m}
                  </li>
                ))}
              </ul>
            </div>
          )}

          {ready && <p className="text-sm font-medium text-success">{fmt(S.bulk.ready, { n: rows.length })}</p>}
        </section>

        {rows.length > 0 && (
          <section>
            <h2 className="mb-2 text-sm font-semibold text-mute">{S.bulk.rowsTitle}</h2>
            <ul className="divide-y divide-line rounded-card border border-line bg-surface">
              {rows.slice(0, 50).map((r, i) => (
                <li key={`${r.slug}-${i}`} className="px-4 py-3">
                  <p className="break-words text-[15px] font-medium">{r.title}</p>
                  <p className="text-[13px] text-mute">
                    {r.slug} · MOQ {r.moq} · GST {r.gstRate}% · {r.status}
                  </p>
                </li>
              ))}
            </ul>
          </section>
        )}

        <Button size="lg" block disabled={!ready} loading={run.isPending} onClick={() => file && run.mutate(file)}>
          {run.isPending ? S.bulk.importing : fmt(S.bulk.import, { n: rows.length })}
        </Button>
      </div>
    </div>
  )
}
