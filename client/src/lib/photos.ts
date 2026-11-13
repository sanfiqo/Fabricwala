import { useQuery } from '@tanstack/react-query'

// Real photos can be dropped into client/public/photos as <productId>-<n>.jpg (n = 1 for the cover, 2, 3, ...).
// Run `npm run photos` once after adding files: it writes photos/manifest.json, so the app only asks for files that exist
// (no failed requests for the rest). Where there is no file, the photo from the server or the placeholder is used.
// The image slot has a fixed size, so nothing moves when photos arrive.
type Manifest = Record<string, number[]>

async function loadManifest(): Promise<Manifest> {
  try {
    const res = await fetch('/photos/manifest.json', { cache: 'no-cache' })
    if (!res.ok || !(res.headers.get('content-type') ?? '').includes('json')) return {}
    return (await res.json()) as Manifest
  } catch {
    return {}
  }
}

export const photoUrl = (productId: number, n: number) => `/photos/${productId}-${n}.jpg`

/** The local photo for this product if there is one, else `fallback` (the server photo). `n` starts at 1. */
export function usePhoto(productId: number | undefined, n: number, fallback: string | null | undefined): string | null | undefined {
  const m = useQuery({ queryKey: ['photo-manifest'], queryFn: loadManifest, staleTime: Infinity, gcTime: Infinity, retry: false })
  if (productId !== undefined && m.data?.[String(productId)]?.includes(n)) return photoUrl(productId, n)
  return fallback
}
