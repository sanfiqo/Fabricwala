import { useQuery } from '@tanstack/react-query'
import { featureApi } from '@/api/endpoints'
import { qk } from '@/api/keys'

/** True when an optional part of the platform is switched on by the admin (for example `Rfq`). False while loading or if the check fails. */
export function useFeature(name: string): boolean {
  const q = useQuery({ queryKey: qk.features, queryFn: featureApi.all, staleTime: 5 * 60_000, retry: false })
  return q.data?.[name] === true
}
