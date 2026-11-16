import { useId, useState, type FormEvent } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Clock, Search, Tag } from 'lucide-react'
import { productApi } from '@/api/endpoints'
import { useCategories, useDebounced } from '@/hooks'
import { S } from '@/i18n'
import { cn } from '@/lib/utils'

const KEY = 'tc_recent_searches'
const readRecent = (): string[] => {
  try {
    const v = JSON.parse(localStorage.getItem(KEY) ?? '[]') as unknown
    return Array.isArray(v) ? v.filter((x): x is string => typeof x === 'string').slice(0, 6) : []
  } catch {
    return []
  }
}
const writeRecent = (list: string[]) => {
  try {
    localStorage.setItem(KEY, JSON.stringify(list.slice(0, 6)))
  } catch {
    /* storage blocked: recent searches just are not remembered */
  }
}

/**
 * Soft pill search field. On focus it offers recent searches; while typing it offers
 * matching cloth types and product names. Voice and photo search buttons are added here later.
 */
export function SearchBox({ initial = '', onSearch, placeholder, className }: { initial?: string; onSearch: (q: string) => void; placeholder?: string; className?: string }) {
  const [q, setQ] = useState(initial)
  const [open, setOpen] = useState(false)
  const [recent, setRecent] = useState<string[]>(readRecent)
  const listId = useId()
  const typed = useDebounced(q.trim(), 250)
  const cats = useCategories()

  const hits = useQuery({
    queryKey: ['search-suggest', typed],
    queryFn: () => productApi.list({ q: typed, pageSize: 4 }),
    enabled: open && typed.length >= 2,
    staleTime: 60_000,
  })

  const run = (term: string) => {
    const t = term.trim()
    setOpen(false)
    if (t) {
      const next = [t, ...recent.filter((r) => r.toLowerCase() !== t.toLowerCase())]
      setRecent(next)
      writeRecent(next)
    }
    setQ(t)
    onSearch(t)
  }
  const submit = (e: FormEvent) => {
    e.preventDefault()
    run(q)
  }

  const needle = q.trim().toLowerCase()
  const catHits = needle ? (cats.data ?? []).filter((c) => c.name.toLowerCase().includes(needle)).slice(0, 3) : []
  const prodHits = (hits.data?.items ?? []).map((p) => p.title)
  const recentShown = needle ? recent.filter((r) => r.toLowerCase().includes(needle)) : recent
  const showPanel = open && (recentShown.length > 0 || catHits.length > 0 || prodHits.length > 0)

  return (
    <div className={cn('relative', className)}>
      <form onSubmit={submit} role="search" className="flex min-h-[52px] items-center gap-2.5 rounded-[26px] bg-soft pl-4 pr-2">
        <Search className="size-[18px] shrink-0 text-ink" aria-hidden />
        <input
          type="search"
          value={q}
          onChange={(e) => {
            setQ(e.target.value)
            setOpen(true)
          }}
          onFocus={() => setOpen(true)}
          onBlur={() => setOpen(false)}
          onKeyDown={(e) => e.key === 'Escape' && setOpen(false)}
          placeholder={placeholder ?? S.shop.searchPlaceholder}
          aria-label={S.app.search}
          aria-controls={showPanel ? listId : undefined}
          aria-expanded={showPanel}
          enterKeyHint="search"
          autoComplete="off"
          className="min-h-12 min-w-0 flex-1 bg-transparent text-[15px] text-ink outline-none placeholder:text-mute [&::-webkit-search-cancel-button]:hidden"
        />
      </form>

      {showPanel && (
        // onMouseDown keeps the input focused, so a tap on a row is not lost to the blur.
        <div id={listId} onMouseDown={(e) => e.preventDefault()} className="absolute inset-x-0 top-full z-30 mt-2 overflow-hidden rounded-2xl border border-line bg-surface shadow-nav">
          {recentShown.length > 0 && (
            <div>
              <div className="flex items-center justify-between px-4 pt-3 text-xs text-mute">
                <span>{S.search.recent}</span>
                <button
                  type="button"
                  className="min-h-11 px-2 underline"
                  onClick={() => {
                    setRecent([])
                    writeRecent([])
                  }}
                >
                  {S.search.clearRecent}
                </button>
              </div>
              <ul>
                {recentShown.map((r) => (
                  <li key={r}>
                    <button type="button" onClick={() => run(r)} className="flex min-h-12 w-full items-center gap-3 px-4 text-left">
                      <Clock className="size-4 shrink-0 text-mute" aria-hidden />
                      <span className="min-w-0 flex-1 truncate">{r}</span>
                    </button>
                  </li>
                ))}
              </ul>
            </div>
          )}
          {(catHits.length > 0 || prodHits.length > 0) && (
            <div>
              <p className="px-4 pt-3 text-xs text-mute">{S.search.suggestions}</p>
              <ul>
                {catHits.map((c) => (
                  <li key={`c${c.id}`}>
                    <button type="button" onClick={() => run(c.name)} className="flex min-h-12 w-full items-center gap-3 px-4 text-left">
                      <Tag className="size-4 shrink-0 text-mute" aria-hidden />
                      <span className="min-w-0 flex-1 truncate">{c.name}</span>
                    </button>
                  </li>
                ))}
                {prodHits.map((t) => (
                  <li key={t}>
                    <button type="button" onClick={() => run(t)} className="flex min-h-12 w-full items-center gap-3 px-4 text-left">
                      <Search className="size-4 shrink-0 text-mute" aria-hidden />
                      <span className="min-w-0 flex-1 truncate">{t}</span>
                    </button>
                  </li>
                ))}
              </ul>
            </div>
          )}
        </div>
      )}
    </div>
  )
}
