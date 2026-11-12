import { cva, type VariantProps } from 'class-variance-authority'
import { forwardRef, type ButtonHTMLAttributes } from 'react'
import { Loader2 } from 'lucide-react'
import { cn } from '@/lib/utils'

const buttonVariants = cva(
  'inline-flex items-center justify-center gap-2 rounded-xl font-semibold transition active:scale-[.98] disabled:pointer-events-none disabled:opacity-50 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-leaf-600',
  {
    variants: {
      variant: {
        primary: 'bg-navy-900 text-white hover:bg-navy-800',
        accent: 'bg-leaf-600 text-white hover:bg-leaf-700',
        outline: 'border border-navy-900/25 bg-white text-navy-900 hover:bg-navy-50',
        ghost: 'text-navy-900 hover:bg-navy-50',
        danger: 'bg-bad text-white hover:opacity-90',
        dangerOutline: 'border border-bad/40 bg-white text-bad hover:bg-bad-bg',
      },
      size: {
        md: 'min-h-12 px-5 text-base',
        sm: 'min-h-10 px-4 text-sm',
        icon: 'size-12',
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
