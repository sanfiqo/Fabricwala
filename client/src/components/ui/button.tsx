import { cva, type VariantProps } from 'class-variance-authority'
import { forwardRef, type ButtonHTMLAttributes } from 'react'
import { Loader2 } from 'lucide-react'
import { cn } from '@/lib/utils'

/** One charcoal pill per screen (primary). Everything else is soft, outline or ghost. */
export const buttonVariants = cva(
  'press inline-flex select-none items-center justify-center gap-2 rounded-pill font-medium disabled:pointer-events-none disabled:opacity-45',
  {
    variants: {
      variant: {
        primary: 'bg-ink text-background hover:bg-ink/90',
        /** Same as primary. Kept so screens that are not migrated yet keep working. */
        accent: 'bg-ink text-background hover:bg-ink/90',
        soft: 'bg-soft text-ink hover:bg-line',
        outline: 'border border-line bg-surface text-ink hover:bg-soft',
        ghost: 'text-ink hover:bg-soft',
        danger: 'bg-error text-white hover:opacity-90',
        dangerOutline: 'border border-error/40 bg-surface text-error hover:bg-error-bg',
      },
      size: {
        md: 'min-h-12 px-6 text-base',
        lg: 'min-h-14 px-6 text-base',
        sm: 'min-h-11 px-4 text-sm',
        icon: 'size-12',
        'icon-sm': 'size-11',
      },
      block: { true: 'w-full', false: '' },
    },
    defaultVariants: { variant: 'primary', size: 'md', block: false },
  },
)

interface Props extends ButtonHTMLAttributes<HTMLButtonElement>, VariantProps<typeof buttonVariants> {
  loading?: boolean
}

export const Button = forwardRef<HTMLButtonElement, Props>(({ className, variant, size, block, loading, children, disabled, ...rest }, ref) => (
  <button ref={ref} className={cn(buttonVariants({ variant, size, block }), className)} disabled={disabled || loading} {...rest}>
    {loading && <Loader2 className="size-4 animate-spin" aria-hidden />}
    {children}
  </button>
))
Button.displayName = 'Button'
