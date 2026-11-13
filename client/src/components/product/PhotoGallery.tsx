import { useRef, useState } from 'react'
import type { CSSProperties } from 'react'
import type { ProductDetail } from '@/api/types'
import { ProductImage } from '@/components/ProductImage'
import { fmt, S } from '@/i18n'
import { usePhoto } from '@/lib/photos'
import { cn } from '@/lib/utils'

function Thumb({ productId, n, url, lqip }: { productId: number; n: number; url: string; lqip?: string | null }) {
  const src = usePhoto(productId, n, url)
  return <ProductImage src={src} lqip={lqip} alt="" width={120} height={120} className="size-full rounded-lg" />
}

/** Large photo on a soft panel, thumbnails down the right. Swipe the photo to change. Fixed height, so nothing jumps while loading. */
export function PhotoGallery({ p }: { p: ProductDetail }) {
  const [i, setI] = useState(0)
  const touchX = useRef<number | null>(null)
  const media = p.media.filter((m) => m.mediaType === 'Image')
  const n = media.length
  const cur = media[Math.min(i, Math.max(0, n - 1))]
  const go = (d: number) => n > 1 && setI((x) => (x + d + n) % n)
  const shown = media.slice(0, 4)
  const mainPhoto = usePhoto(p.id, Math.min(i, Math.max(0, n - 1)) + 1, cur?.url)

  return (
    <div className={cn('mx-5 grid overflow-hidden rounded-[24px] bg-soft', n > 1 ? 'grid-cols-[minmax(0,1fr)_80px]' : 'grid-cols-1')}>
      <div
        className="photo-slot h-80 items-center"
        style={{ '--shadow-bottom': '30px', '--shadow-width': '55%', '--shadow-height': '14px' } as CSSProperties}
        onTouchStart={(e) => {
          touchX.current = e.touches[0].clientX
        }}
        onTouchEnd={(e) => {
          if (touchX.current === null) return
          const dx = e.changedTouches[0].clientX - touchX.current
          touchX.current = null
          if (Math.abs(dx) > 40) go(dx < 0 ? 1 : -1)
        }}
      >
        <ProductImage
          src={mainPhoto}
          lqip={cur?.lqip}
          alt={p.title}
          width={800}
          height={800}
          priority
          sizes="230px"
          style={{ viewTransitionName: 'product-photo' }}
          className="relative z-[1] size-[230px] rounded-[20px]"
        />
        {n > 1 && <span className="absolute bottom-3 left-3 z-[2] rounded-xl bg-surface px-2.5 py-1 text-xs tabular-nums">{fmt(S.product.photoCount, { i: i + 1, n })}</span>}
      </div>
      {n > 1 && (
        <div className="flex flex-col gap-2 p-2">
          {shown.map((m, idx) => (
            <button
              key={m.id}
              type="button"
              aria-label={fmt(S.product.photo, { i: idx + 1, n })}
              aria-current={idx === i}
              onClick={() => setI(idx)}
              className={cn('press flex h-[72px] items-stretch justify-stretch overflow-hidden rounded-xl border-2 bg-surface p-1', idx === i ? 'border-ink' : 'border-transparent')}
            >
              <Thumb productId={p.id} n={idx + 1} url={m.url} lqip={m.lqip} />
            </button>
          ))}
          {n > shown.length && (
            <button type="button" onClick={() => setI(shown.length)} className="press flex min-h-11 items-center justify-center rounded-xl text-xs text-mute">
              +{n - shown.length}
            </button>
          )}
        </div>
      )}
    </div>
  )
}
