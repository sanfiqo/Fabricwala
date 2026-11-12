import { Navigate, Route, Routes } from 'react-router-dom'
import { useMe, homeFor, useUnauthorizedListener } from '@/auth'
import { PageLoading } from '@/components/ui/misc'
import { LinkButton, EmptyState } from '@/components/ui/misc'
import { S } from '@/i18n'

function RootRedirect() {
  const { data: me, isLoading } = useMe()
  if (isLoading) return <PageLoading />
  return <Navigate to={homeFor(me)} replace />
}

export default function App() {
  useUnauthorizedListener()
  return (
    <Routes>
      <Route path="/" element={<RootRedirect />} />
      <Route path="*" element={<EmptyState title={S.app.notFound} action={<LinkButton to="/">{S.app.goHome}</LinkButton>} />} />
    </Routes>
  )
}
