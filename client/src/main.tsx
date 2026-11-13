import { StrictMode, type ReactNode } from 'react'
import { createRoot } from 'react-dom/client'
import { createBrowserRouter, RouterProvider } from 'react-router-dom'
import { useIsRestoring } from '@tanstack/react-query'
import { PersistQueryClientProvider } from '@tanstack/react-query-persist-client'
import { queryClient, persistOptions } from '@/queryClient'
import { ToastProvider } from '@/components/ui/toast'
import { MotionRoot, initPerfProfile } from '@/motion'
import { useLang } from '@/i18n'
import App from './App'
import './index.css'

initPerfProfile()

// A data router (instead of <BrowserRouter>) is what lets links use the View Transitions API.
const router = createBrowserRouter([{ path: '*', element: <App /> }])

/** Re-mounts the app when the language changes, so every screen picks up the new text. */
function LangRoot() {
  const lang = useLang()
  return <RouterProvider key={lang} router={router} />
}

/** Saved data loads from the phone in one tick. Wait for it, or the login check would briefly see "no session" and bounce to /login. */
function WaitForSavedData({ children }: { children: ReactNode }) {
  return useIsRestoring() ? null : children
}

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <PersistQueryClientProvider client={queryClient} persistOptions={persistOptions}>
      <ToastProvider>
        <MotionRoot>
          <WaitForSavedData>
            <LangRoot />
          </WaitForSavedData>
        </MotionRoot>
      </ToastProvider>
    </PersistQueryClientProvider>
  </StrictMode>,
)
