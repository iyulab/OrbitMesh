import { createRoot } from 'react-dom/client'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { BrowserRouter } from 'react-router-dom'
import { ThemeProvider } from './contexts/ThemeContext'
import { AuthProvider } from './contexts/AuthContext'
import { Toaster } from './components/ui/sonner'
import App from './App'
import '@xyflow/react/dist/style.css'
import './index.css'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: Infinity, // SignalR handles real-time updates
      refetchOnWindowFocus: false, // No polling - SignalR only
      refetchOnReconnect: false,
    },
  },
})

createRoot(document.getElementById('root')!).render(
  <ThemeProvider>
    <AuthProvider>
      <QueryClientProvider client={queryClient}>
        <BrowserRouter>
          <App />
          <Toaster />
        </BrowserRouter>
      </QueryClientProvider>
    </AuthProvider>
  </ThemeProvider>,
)
