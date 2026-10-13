export type PaymentMethod = 'undefined' | 'pix' | 'boleto' | 'creditCard'
export type CheckoutMode = 'hosted' | 'direct'

export interface CreatePaymentInput {
  name: string
  email: string
  cpfCnpj: string | null
  phone: string | null
  value: number
  dueDate: string
  description: string
  paymentMethod: PaymentMethod
  checkoutMode: CheckoutMode
}

export interface PaymentResponse {
  id: string
  value: number
  paymentMethod: PaymentMethod
  status: string
  dueDate: string
  invoiceUrl: string | null
  bankSlipUrl: string | null
  pixCopyPaste: string | null
  pixQrCodeUrl: string | null
  invoiceStatus: string | null
  invoicePdfUrl: string | null
  invoiceXmlUrl: string | null
  createdAt: string
}

interface ProblemResponse { title?: string; detail?: string }

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(path, { ...init, headers: { 'Content-Type': 'application/json', ...init?.headers } })
  if (!response.ok) {
    const problem = await response.json().catch(() => null) as ProblemResponse | null
    throw new Error(problem?.detail ?? problem?.title ?? `Falha na solicitação (${response.status}).`)
  }
  return response.json() as Promise<T>
}

export function createPayment(input: CreatePaymentInput) {
  return request<PaymentResponse>('/api/payments', { method: 'POST', body: JSON.stringify(input) })
}

export function getPayments() { return request<PaymentResponse[]>('/api/payments') }
export function getPayment(id: string) { return request<PaymentResponse>(`/api/payments/${encodeURIComponent(id)}`) }