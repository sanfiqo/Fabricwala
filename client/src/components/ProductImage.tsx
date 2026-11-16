import { useState, type CSSProperties } from 'react'
import { ImageOff } from 'lucide-react'
import { cn } from '@/lib/utils'

interface Props {
  src?: string | null
  alt: string
  /** Tiny (about 20px wide) blurred version, as a data URL. Shown until the real photo has loaded. TODO(BE): send `lqip` with every image. */
  lqip?: string | null
  /** Pass the real srcset/sizes when the backend serves several widths. */
  srcSet?: string
  sizes?: string
  /** First screen only. Everything else is lazy loaded. */
  priority?: boolean
  width: number
  height: number
  className?: string
  style?: CSSProperties
}

/**
 * The one image slot for product photos. Fixed size (no layout shift), rounded, cover.
 * Shows a blur-up placeholder (or a calm skeleton) until the photo loads, then fades the photo in.
 * Real photos drop in with no change to the layout around it.
 */
export function ProductImage({ src, alt, lqip, srcSet, sizes, priority, width, height, className, style }: Props) {
  const [loaded, setLoaded] = useState(false)
  const [failed, setFailed] = useState(false)
  const showImg = !!src && !failed

  return (
    <span
      className={cn('relative block overflow-hidden bg-line/60', !loaded && showImg && !lqip && 'skeleton', className)}
      style={style}
    >
      {lqip && !loaded && <span aria-hidden className="absolute inset-0 scale-110 bg-cover bg-center blur-xl" style={{ backgroundImage: `url(${lqip})` }} />}
      {showImg ? (
        <img
          ref={(el) => {
            // Cached photos can finish before React attaches onLoad.
            if (el?.complete && el.naturalWidth > 0) setLoaded(true)
          }}
          src={src}
          srcSet={srcSet}
          sizes={sizes}
          alt={alt}
          width={width}
          height={height}
          loading={priority ? 'eager' : 'lazy'}
          decoding="async"
          fetchPriority={priority ? 'high' : 'auto'}
          onLoad={() => setLoaded(true)}
          onError={() => setFailed(true)}
          className={cn('relative size-full object-cover transition-opacity duration-300', loaded ? 'opacity-100' : 'opacity-0')}
        />
      ) : (
        <span className="flex size-full items-center justify-center text-mute" role="img" aria-label={alt}>
          <ImageOff className="size-1/3" aria-hidden />
        </span>
      )}
    </span>
  )
}
