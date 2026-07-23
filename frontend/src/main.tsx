import { StrictMode, useState } from 'react'
import { createRoot } from 'react-dom/client'

// Intentional react/rules-of-hooks violation to validate the CI lint gate (ET020 task 7.4).
const _lintViolationProbe = useState(0)
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import './index.css'
import App from './App.tsx'

const queryClient = new QueryClient()

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <App />
    </QueryClientProvider>
  </StrictMode>,
)
