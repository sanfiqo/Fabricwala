import { useState, type FormEvent } from 'react'
import { Search } from 'lucide-react'
import { S } from '@/i18n'
import { cn } from '@/lib/utils'

/** Soft pill search field. Voice and photo search buttons are added here later. */
export function SearchBox({ initial = '', onSearch, placeholder, className }: { initial?: string; onSearch: (q: string) => void; placeholder?: string; className?: string }) {
  const [q, setQ] = useState(initial)
  const submit = (e: FormEvent) => {
    e.preventDefault()
    onSearch(q.trim())
  }
  return (
    <form onSubmit={submit} role="search" className={cn('flex min-h-[52px] items-center gap-2.5 rounded-[26px] bg-soft pl-4 pr-2', className)}>
      <Search className="size-[18px] shrink-0 text-ink" aria-hidden />
      <input
        type="search"
        value={q}
        onChange={(e) => setQ(e.target.value)}
        placeholder={placeholder ?? S.shop.searchPlaceholder}
        aria-label={S.app.search}
        enterKeyHint="search"
        className="min-h-12 min-w-0 flex-1 bg-transparent text-[15px] text-ink outline-none placeholder:text-mute [&::-webkit-search-cancel-button]:hidden"
      />
    </form>
  )
}
