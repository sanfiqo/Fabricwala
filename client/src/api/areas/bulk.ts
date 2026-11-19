import { upload } from '../client'
import type { BulkImportResult, BulkPreview } from '../types'

/** Template download is a plain link (`/api/v1/seller/products/bulk/template`, CSV). */
export const BULK_TEMPLATE_URL = '/api/v1/seller/products/bulk/template'

const asForm = (file: File) => {
  const form = new FormData()
  form.append('file', file)
  return form
}

/** Bulk product upload (CSV only). Preview shows what would be created; import creates it and fails if any row has a problem. */
export const bulkApi = {
  preview: (file: File) => upload<BulkPreview>('seller/products/bulk/preview', asForm(file)),
  import: (file: File) => upload<BulkImportResult>('seller/products/bulk/import', asForm(file)),
}
