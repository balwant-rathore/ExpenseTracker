import { useSessionBootstrap } from '@/features/auth/session/useSessionBootstrap'
import { AppRouter } from '@/routes/AppRouter'

function App() {
  useSessionBootstrap()

  return <AppRouter />
}

export default App
