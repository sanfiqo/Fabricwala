import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { createBrowserRouter, RouterProvider } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { ApiError } from '@/api/client'
import { ToastProvider } from '@/components/ui/toast'
import { MotionRoot, initPerfProfile } from '@/motion'
import { useLang } from '@/i18n'
import App from './App'
import './index.css'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      refetchOnWindowFocus: false,
      // Do not hammer the server on client errors (401, 403, 404, 422).
      retry: (count, err) => !(err instanceof ApiError && err.status < 500) && count < 2,
    },
  },
})

initPerfProfile()

// A data router (instead of <BrowserRouter>) is what lets links use the View Transitions API.
const router = createBrowserRouter([{ path: '*', element: <App /> }])

/** Re-mounts the app when the language changes, so every screen picks up the new text. */
function LangRoot() {
  const lang = useLang()
  return <RouterProvider key={lang} router={router} />
}

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <ToastProvider>
        <MotionRoot>
          <LangRoot />
        </MotionRoot>
      </ToastProvider>
    </QueryClientProvider>
  </StrictMode>,
)
