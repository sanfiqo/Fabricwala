import { useMutation } from '@tanstack/react-query'
import { Download, Loader2 } from 'lucide-react'
import { useToast } from '@/components/ui/toast'
import { downloadInvoice, type InvoiceType } from '@/lib/invoice'

/** A row for an order card: tap to download one invoice as a PDF. Same look as the other rows (help, report). */
export function InvoiceRow({ sellerOrderId, type, label }: { sellerOrderId: number; type?: InvoiceType; label: string }) {
  const toast = useToast()
  const get = useMutation({ mutationFn: () => downloadInvoice(sellerOrderId, type), onError: (e) => toast.error(e.message) })
  return (
    <button type="button" disabled={get.isPending} onClick={() => get.mutate()} className="press flex min-h-[52px] w-full items-center gap-3 border-t border-line px-4 text-left text-sm">
      {get.isPending ? <Loader2 className="size-5 shrink-0 animate-spin" aria-hidden /> : <Download className="size-5 shrink-0" aria-hidden />}
      <span className="flex-1">{label}</span>
    </button>
  )
}
