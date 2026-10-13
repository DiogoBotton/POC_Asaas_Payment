import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createBrowserRouter, RouterProvider } from 'react-router'
import './PaymentsApp.css'
import App from './App.tsx'
import { NewPaymentPage, PaymentDetailsPage, PaymentsPage } from './PaymentsApp.tsx'

const queryClient = new QueryClient({
  defaultOptions: { queries: { staleTime: 2_000, retry: 1 } },
})

const router = createBrowserRouter([{
  path: '/',
  element: <App />,
  children: [
    { index: true, element: <PaymentsPage /> },
    { path: 'payments/new', element: <NewPaymentPage /> },
    { path: 'payments/:id', element: <PaymentDetailsPage /> },
  ],
}])

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>
  </StrictMode>,
)
