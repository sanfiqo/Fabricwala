import { forwardRef, useId, type InputHTMLAttributes, type ReactNode, type SelectHTMLAttributes, type TextareaHTMLAttributes } from 'react'
import { cn } from '@/lib/utils'

const control =
  'block w-full min-h-12 rounded-xl border bg-white px-4 text-base text-ink placeholder:text-mute/70 focus:border-leaf-600 focus:outline-2 focus:outline-leaf-600/30 disabled:bg-navy-50'

interface FieldProps {
  label: string
  hint?: string
  error?: string
  children: (id: string, invalid: boolean) => ReactNode
  className?: string
}

/** Label + control + hint/error. The control is a render prop so it gets the id. */
export function Field({ label, hint, error, children, className }: FieldProps) {
  const id = useId()
  return (
    <div className={cn('space-y-1.5', className)}>
      <label htmlFor={id} className="block text-sm font-semibold text-navy-900">
        {label}
      </label>
      {children(id, !!error)}
      {error ? (
        <p role="alert" className="text-sm font-medium text-bad">
          {error}
        </p>
      ) : hint ? (
        <p className="text-sm text-mute">{hint}</p>
      ) : null}
    </div>
  )
}

type InputProps = InputHTMLAttributes<HTMLInputElement> & { invalid?: boolean }
export const Input = forwardRef<HTMLInputElement, InputProps>(({ className, invalid, ...p }, ref) => (
  <input ref={ref} aria-invalid={invalid || undefined} className={cn(control, invalid ? 'border-bad' : 'border-line', className)} {...p} />
))
Input.displayName = 'Input'

type SelectProps = SelectHTMLAttributes<HTMLSelectElement> & { invalid?: boolean }
export const Select = forwardRef<HTMLSelectElement, SelectProps>(({ className, invalid, ...p }, ref) => (
  <select ref={ref} aria-invalid={invalid || undefined} className={cn(control, 'pr-8', invalid ? 'border-bad' : 'border-line', className)} {...p} />
))
Select.displayName = 'Select'

type AreaProps = TextareaHTMLAttributes<HTMLTextAreaElement> & { invalid?: boolean }
export const Textarea = forwardRef<HTMLTextAreaElement, AreaProps>(({ className, invalid, ...p }, ref) => (
  <textarea ref={ref} aria-invalid={invalid || undefined} className={cn(control, 'min-h-24 py-3', invalid ? 'border-bad' : 'border-line', className)} {...p} />
))
Textarea.displayName = 'Textarea'
