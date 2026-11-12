import { useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { ChevronRight, Search } from 'lucide-react'
import { productApi } from '@/api/endpoints'
import { ProductCard } from '@/components/ProductCard'
import { ErrorState, PageLoading, SectionTitle } from '@/components/ui/misc'
import { useCategories } from '@/hooks'
import { S } from '@/i18n'

export function SearchBar({ initial = '', onSearch, className }: { initial?: string; onSearch: (q: string) => void; className?: string }) {
  const [q, setQ] = useState(initial)
  const submit = (e: FormEvent) => {
    e.preventDefault()
    onSearch(q.trim())
  }
  return (
    <form onSubmit={submit} role="search" className={className}>
      <div className="relative">
        <Search className="pointer-events-none absolute left-4 top-1/2 size-5 -translate-y-1/2 text-mute" />
        <input
          type="search"
          value={q}
          onChange={(e) => setQ(e.target.value)}
          placeholder={S.shop.searchPlaceholder}
          aria-label={S.app.search}
          enterKeyHint="search"
          className="min-h-12 w-full rounded-2xl border border-line bg-navy-50 pl-12 pr-4 text-base placeholder:text-mute focus:border-leaf-600 focus:bg-white focus:outline-2 focus:outline-leaf-600/30"
        />
      </div>
    </form>
  )
}

export default function ShopHome() {
  const nav = useNavigate()
  const cats = useCategories()
  const fresh = useQuery({ queryKey: ['products', 'home'], queryFn: () => productApi.list({ sort: 'new', pageSize: 6 }) })

  return (
    <div className="px-4 pb-6 pt-4">
      <SearchBar onSearch={(q) => nav(`/shop/products${q ? `?q=${encodeURIComponent(q)}` : ''}`)} />

      <SectionTitle>{S.shop.categories}</SectionTitle>
      {cats.isLoading ? (
        <PageLoading />
      ) : cats.error ? (
        <ErrorState error={cats.error} onRetry={() => void cats.refetch()} />
      ) : (
        <div className="grid grid-cols-2 gap-3">
          {(cats.data ?? []).map((c) => (
            <Link key={c.id} to={`/shop/products?categoryId=${c.id}`} className="relative flex min-h-20 items-center overflow-hidden rounded-2xl bg-navy-50 py-3 pl-5 pr-3 active:bg-navy-100">
              <span className="absolute inset-y-0 left-0 w-1.5 bg-leaf-600" />
              <span className="flex-1 text-[15px] font-bold leading-5 text-navy-900">{c.name}</span>
              <ChevronRight className="size-5 shrink-0 text-mute" />
            </Link>
          ))}
        </div>
      )}

      <hr className="stitch my-6" />
      <SectionTitle action={<Link to="/shop/products" className="flex min-h-10 items-center text-sm font-bold text-leaf-700">{S.shop.all}</Link>}>{S.shop.featured}</SectionTitle>
      {fresh.isLoading ? (
        <PageLoading />
      ) : fresh.error ? (
        <ErrorState error={fresh.error} onRetry={() => void fresh.refetch()} />
      ) : (
        <div className="grid grid-cols-2 gap-3">{fresh.data?.items.map((p) => <ProductCard key={p.id} p={p} />)}</div>
      )}
    </div>
  )
}
