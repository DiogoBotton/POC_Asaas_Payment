export type PaymentMethod = 'undefined' | 'pix' | 'boleto' | 'creditCard'
export type DirectPaymentMethod = Exclude<PaymentMethod, 'undefined'>

export interface CreditCardDetails {
  holderName: string
  number: string
  expiryMonth: string
  expiryYear: string
  ccv: string
}

export interface CreditCardHolderDetails {
  name: string
  email: string
  cpfCnpj: string
  postalCode: string
  addressNumber: string
  phone: string
  address: string | null
  province: string | null
  addressComplement: string | null
  mobilePhone: string | null
}

export interface Customer {
  id: string
  name: string
  email: string
  cpfCnpj: string | null
  phone: string | null
  postalCode: string | null
  address: string | null
  addressNumber: string | null
  addressComplement: string | null
  province: string | null
  createdAt: string
}

export interface CreditCardDeviceInfo {
  colorDepth: number
  screenHeight: number
  screenWidth: number
  timeZoneOffset: number
  language: string
  userAgent: string
}

export interface CreatePaymentInput {
  name: string
  email: string
  cpfCnpj: string | null
  phone: string | null
  postalCode: string
  address: string
  addressNumber: string
  addressComplement: string | null
  province: string
  value: number
  dueDate: string
  description: string
  paymentMethod: DirectPaymentMethod
  saveCardForFutureUse: boolean
  creditCard?: CreditCardDetails
  creditCardHolderInfo?: CreditCardHolderDetails
  deviceInfo?: CreditCardDeviceInfo
}

export interface PaymentResponse {
  id: string
  value: number
  paymentMethod: PaymentMethod
  status: string
  dueDate: string
  bankSlipUrl: string | null
  bankSlipIdentificationField: string | null
  pixCopyPaste: string | null
  pixQrCodeUrl: string | null
  threeDSecureChallengeUrl: string | null
  invoiceStatus: string | null
  invoicePdfUrl: string | null
  invoiceXmlUrl: string | null
  createdAt: string
}

export type RecurringPaymentMethod = Exclude<PaymentMethod, 'undefined'>
export type BillingCycle = 'weekly' | 'biweekly' | 'monthly' | 'bimonthly' | 'quarterly' | 'semiannually' | 'yearly'

export interface CreateSubscriptionInput {
  name: string
  email: string
  cpfCnpj: string | null
  phone: string | null
  postalCode: string
  address: string
  addressNumber: string
  addressComplement: string | null
  province: string
  value: number
  paymentMethod: RecurringPaymentMethod
  cycle: BillingCycle
  nextDueDate: string
  description: string
  saveCardForFutureUse: boolean
  creditCard?: CreditCardDetails
  creditCardHolderInfo?: CreditCardHolderDetails
}

export interface SubscriptionCharge {
  id: string
  value: number
  paymentMethod: RecurringPaymentMethod
  status: string
  dueDate: string
  bankSlipIdentificationField: string | null
  bankSlipUrl: string | null
  pixCopyPaste: string | null
  pixQrCodeUrl: string | null
}

export interface Subscription {
  id: string
  customerName: string
  customerEmail: string
  value: number
  paymentMethod: RecurringPaymentMethod
  cycle: BillingCycle
  status: string
  nextDueDate: string
  createdAt: string
  payments: SubscriptionCharge[]
}

interface ProblemResponse { title?: string; detail?: string; errors?: Record<string, string[]> }

function problemMessage(problem: ProblemResponse | null, status: number) {
  const validationErrors = Object.entries(problem?.errors ?? {}).flatMap(([field, messages]) =>
    messages.map((message) => field === '$' || field === 'request' ? message : `${field}: ${message}`))
  return validationErrors.join(' ') || problem?.detail || problem?.title || `Falha na solicitação (${status}).`
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(path, { ...init, headers: { 'Content-Type': 'application/json', ...init?.headers } })
  if (!response.ok) {
    const problem = await response.json().catch(() => null) as ProblemResponse | null
    throw new Error(problemMessage(problem, response.status))
  }
  return response.json() as Promise<T>
}

export function createPayment(input: CreatePaymentInput) {
  return request<PaymentResponse>('/api/payments', { method: 'POST', body: JSON.stringify(input) })
}

export function getPayments() { return request<PaymentResponse[]>('/api/payments') }
export function getPayment(id: string) { return request<PaymentResponse>(`/api/payments/${encodeURIComponent(id)}`) }
export function getCustomers() { return request<Customer[]>('/api/customers') }
export async function findCustomerByCpfCnpj(cpfCnpj: string): Promise<Customer | null> {
  const response = await fetch(`/api/customers/lookup?cpfCnpj=${encodeURIComponent(cpfCnpj)}`)
  if (response.status === 404) return null
  if (!response.ok) {
    const problem = await response.json().catch(() => null) as ProblemResponse | null
    throw new Error(problemMessage(problem, response.status))
  }
  return response.json() as Promise<Customer>
}

export function getSubscriptions() { return request<Subscription[]>('/api/subscriptions') }
export function createSubscription(input: CreateSubscriptionInput) {
  return request<Subscription>('/api/subscriptions', { method: 'POST', body: JSON.stringify(input) })
}
export function updateSubscriptionValue(id: string, value: number) {
  return request<Subscription>(`/api/subscriptions/${encodeURIComponent(id)}/value`, { method: 'PUT', body: JSON.stringify({ value }) })
}
export function cancelSubscription(id: string) {
  return request<void>(`/api/subscriptions/${encodeURIComponent(id)}`, { method: 'DELETE' })
}