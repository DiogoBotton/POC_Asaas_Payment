import { useDeferredValue, useRef, useState, type FormEvent } from 'react'
import { Link, Navigate, NavLink, Outlet, useNavigate, useParams, useSearchParams } from 'react-router'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, ArrowRight, ArrowUpRight, Banknote, Ban, CalendarDays, Check, CircleAlert, Clock3, Copy, CreditCard, ExternalLink, FileText, LayoutDashboard, Plus, QrCode, ReceiptText, RefreshCw, Repeat2, Save, Search, ShieldCheck, UsersRound, WalletCards } from 'lucide-react'
import { cancelSubscription, createPayment, createSubscription, findCustomerByCpfCnpj, getCustomers, getPayment, getPayments, getSubscriptions, updateSubscriptionValue, type BillingCycle, type CreatePaymentInput, type CreateSubscriptionInput, type CreditCardDetails, type CreditCardHolderDetails, type Customer, type DirectPaymentMethod, type PaymentResponse, type Subscription } from './api'

const currency = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' })
const finalStatuses = new Set(['CONFIRMED', 'RECEIVED', 'RECEIVED_IN_CASH', 'OVERDUE', 'REFUNDED', 'REFUND_REQUESTED', 'CHARGEBACK', 'DELETED'])

type CardFormValues = CreditCardDetails

const emptyCardForm: CardFormValues = { holderName: '', number: '', expiryMonth: '', expiryYear: '', ccv: '' }

function cardPayload(card: CardFormValues, customer: { name: string; email: string; cpfCnpj: string; phone: string; postalCode: string; addressNumber: string; address: string; province: string; addressComplement: string }) {
    const creditCard: CreditCardDetails = { holderName: card.holderName.trim(), number: card.number, expiryMonth: card.expiryMonth, expiryYear: card.expiryYear, ccv: card.ccv }
    const creditCardHolderInfo: CreditCardHolderDetails = {
        name: customer.name.trim(), email: customer.email.trim(), cpfCnpj: customer.cpfCnpj.trim(),
        postalCode: customer.postalCode, addressNumber: customer.addressNumber.trim(), phone: customer.phone.trim(),
        address: customer.address.trim() || null, province: customer.province.trim() || null,
        addressComplement: customer.addressComplement.trim() || null, mobilePhone: customer.phone.trim() || null,
    }
    return { creditCard, creditCardHolderInfo }
}

function parseInteger(value: string) {
    const normalized = value.trim()
    if (!/^[+-]?\d+$/.test(normalized)) return null
    const parsed = Number(normalized)
    return Number.isInteger(parsed) && parsed >= -2147483648 && parsed <= 2147483647 ? parsed : null
}

function validateCard(card: CardFormValues) {
    const number = digitsOnly(card.number)
    const ccv = digitsOnly(card.ccv)
    const month = parseInteger(card.expiryMonth)
    const year = parseInteger(card.expiryYear)
    if (number.length < 12 || number.length > 19 || ccv.length < 3 || ccv.length > 4 ||
        month === null || month < 1 || month > 12 || card.expiryYear.length !== 4 || year === null) {
        return 'Confira número, validade e código de segurança do cartão.'
    }
    return null
}

function collectDeviceInfo() {
    return {
        colorDepth: window.screen.colorDepth,
        screenHeight: window.screen.height,
        screenWidth: window.screen.width,
        timeZoneOffset: new Date().getTimezoneOffset() / 60,
        language: navigator.language,
        userAgent: navigator.userAgent,
    }
}

export default function PaymentsApp() {
    return <div className="app-shell">
        <aside className="sidebar">
            <Link className="brand" to="/" aria-label="Pagamentos, início"><span className="brand-mark"><ReceiptText size={19} /></span><span>pagamentos<span className="brand-period">.</span></span></Link>
            <div className="nav-caption">OPERAÇÃO</div>
            <nav className="primary-nav" aria-label="Navegação principal">
                <NavLink to="/" end className="nav-link"><LayoutDashboard size={18} /><span>Cobranças</span></NavLink>
                <NavLink to="/payments/new" className="nav-link"><Plus size={18} /><span>Nova cobrança</span></NavLink>
                <NavLink to="/customers" className="nav-link"><UsersRound size={18} /><span>Clientes</span></NavLink>
                <NavLink to="/subscriptions" end className="nav-link"><Repeat2 size={18} /><span>Assinaturas</span></NavLink>
            </nav>
            <div className="sidebar-bottom"><div className="environment"><span className="environment-dot" /> Sandbox Asaas</div><div className="sidebar-note"><ShieldCheck size={15} /> Integração protegida no backend</div></div>
        </aside>
        <main className="main-content"><header className="topbar"><span className="topbar-label">ASAAS <span>/</span> PAGAMENTOS</span><Link className="topbar-action" to="/payments/new"><Plus size={16} /> Nova cobrança</Link></header><div className="page-content"><Outlet /></div></main>
    </div>
}

export function PaymentsPage() {
    const [filter, setFilter] = useState('')
    const query = useQuery({ queryKey: ['payments'], queryFn: getPayments, refetchInterval: 30_000 })
    const deferredFilter = useDeferredValue(filter.trim().toLocaleLowerCase('pt-BR'))
    const payments = query.data ?? []
    const filtered = payments.filter((item) => `${item.id} ${item.status} ${item.paymentMethod}`.toLocaleLowerCase('pt-BR').includes(deferredFilter))
    const pending = payments.filter((item) => !finalStatuses.has(item.status)).length
    const received = payments.filter((item) => ['CONFIRMED', 'RECEIVED', 'RECEIVED_IN_CASH'].includes(item.status)).reduce((sum, item) => sum + item.value, 0)

    return <section className="page-enter">
        <div className="page-heading heading-row"><div><div className="eyebrow">VISÃO GERAL</div><h1>Cobranças</h1><p className="heading-copy">Acompanhe pagamentos e emissão fiscal em um só lugar.</p></div><Link className="button button-primary" to="/payments/new"><Plus size={17} /> Nova cobrança</Link></div>
        <div className="metrics-row" aria-label="Resumo de cobranças"><div className="metric"><span className="metric-label">TOTAL DE COBRANÇAS</span><strong>{payments.length}</strong></div><div className="metric"><span className="metric-label">AGUARDANDO PAGAMENTO</span><strong>{pending}</strong></div><div className="metric"><span className="metric-label">VALOR CONFIRMADO</span><strong>{currency.format(received)}</strong></div></div>
        <div className="table-toolbar"><div><h2>Movimentações</h2><span className="subtle-text">Atualização automática a cada 30 segundos</span></div><div className="table-actions"><label className="search-field"><Search size={16} /><input value={filter} onChange={(event) => setFilter(event.target.value)} placeholder="Buscar por ID ou status" aria-label="Buscar cobranças" /></label><button className="icon-button" type="button" title="Atualizar lista" aria-label="Atualizar lista" onClick={() => query.refetch()}><RefreshCw size={16} /></button></div></div>
        {query.isPending ? <div className="state-message"><span className="loading-mark" /> Carregando cobranças</div> : query.isError ? <div className="state-message error-message"><CircleAlert size={18} /> {query.error.message}</div> : filtered.length === 0 ? <div className="empty-state"><div className="empty-icon"><ReceiptText size={23} /></div><h3>{payments.length ? 'Nenhum resultado' : 'Nenhuma cobrança ainda'}</h3><p>{payments.length ? 'Tente outro termo de busca.' : 'Crie uma cobrança para iniciar sua operação.'}</p>{!payments.length && <Link className="button button-dark" to="/payments/new"><Plus size={16} /> Criar cobrança</Link>}</div> :
            <div className="table-wrap"><table className="payments-table"><thead><tr><th>COBRANÇA</th><th>FORMA</th><th>VENCIMENTO</th><th>STATUS</th><th className="align-right">VALOR</th><th aria-label="Abrir detalhe" /></tr></thead><tbody>{filtered.map((payment) => <tr key={payment.id}><td><Link className="payment-id" to={`/payments/${payment.id}`}>#{payment.id.slice(0, 8).toUpperCase()}</Link><span className="table-subtext">Criada em {formatDate(payment.createdAt.slice(0, 10))}</span></td><td><span className="method-cell">{methodIcon(payment.paymentMethod)}{methodLabel(payment.paymentMethod)}</span></td><td>{formatDate(payment.dueDate)}</td><td><StatusBadge status={payment.status} /></td><td className="align-right amount-cell">{currency.format(payment.value)}</td><td><Link className="row-open" to={`/payments/${payment.id}`} aria-label={`Abrir cobrança ${payment.id}`}><ArrowRight size={17} /></Link></td></tr>)}</tbody></table></div>}
    </section>
}

export function CustomersPage() {
    const [filter, setFilter] = useState('')
    const query = useQuery({ queryKey: ['customers'], queryFn: getCustomers, refetchInterval: 30_000 })
    const deferredFilter = useDeferredValue(filter.trim().toLocaleLowerCase('pt-BR'))
    const customers = query.data ?? []
    const filtered = customers.filter((customer) =>
        `${customer.name} ${customer.email} ${customer.cpfCnpj ?? ''} ${customer.phone ?? ''}`
            .toLocaleLowerCase('pt-BR')
            .includes(deferredFilter))

    return <section className="page-enter">
        <div className="page-heading heading-row"><div><div className="eyebrow">CADASTROS</div><h1>Clientes</h1><p className="heading-copy">Dados de contato e endereços usados nas cobranças.</p></div><button className="button button-secondary" type="button" onClick={() => query.refetch()} disabled={query.isFetching}><RefreshCw size={16} /> Atualizar</button></div>
        <div className="table-toolbar"><div><h2>Clientes cadastrados</h2><span className="subtle-text">{customers.length} registros</span></div><div className="table-actions"><label className="search-field"><Search size={16} /><input value={filter} onChange={(event) => setFilter(event.target.value)} placeholder="Buscar nome, documento ou e-mail" aria-label="Buscar clientes" /></label></div></div>
        {query.isPending ? <div className="state-message"><span className="loading-mark" /> Carregando clientes</div> : query.isError ? <div className="state-message error-message"><CircleAlert size={18} /> {query.error.message}</div> : filtered.length === 0 ? <div className="empty-state"><div className="empty-icon"><UsersRound size={23} /></div><h3>{customers.length ? 'Nenhum resultado' : 'Nenhum cliente cadastrado'}</h3><p>{customers.length ? 'Tente outro nome, documento ou e-mail.' : 'Os clientes serão listados após o primeiro cadastro em uma cobrança.'}</p></div> :
            <div className="table-wrap"><table className="payments-table customers-table"><thead><tr><th>CLIENTE</th><th>CPF / CNPJ</th><th>TELEFONE</th><th>ENDEREÇO</th></tr></thead><tbody>{filtered.map((customer) => <tr key={customer.id}><td><strong className="customer-name">{customer.name}</strong><span className="table-subtext">{customer.email}</span></td><td>{customer.cpfCnpj || 'Não informado'}</td><td>{customer.phone || 'Não informado'}</td><td className="customer-address">{customerAddress(customer)}</td></tr>)}</tbody></table></div>}
    </section>
}

export function NewPaymentPage() {
    const navigate = useNavigate()
    const queryClient = useQueryClient()
    const [searchParams] = useSearchParams()
    const [mode, setMode] = useState<'oneOff' | 'subscription'>(() => searchParams.get('mode') === 'subscription' ? 'subscription' : 'oneOff')
    const [form, setForm] = useState({ name: '', email: '', cpfCnpj: '', phone: '', postalCode: '', address: '', addressNumber: '', addressComplement: '', province: '', value: '', dueDate: defaultDueDate(), description: '', paymentMethod: 'pix' as DirectPaymentMethod, cycle: 'monthly' as BillingCycle, saveCardForFutureUse: false })
    const [card, setCard] = useState<CardFormValues>(emptyCardForm)
    const [cardValidationError, setCardValidationError] = useState<string | null>(null)
    const [lookupDocument, setLookupDocument] = useState('')
    const documentRef = useRef('')
    const lastDocumentLookup = useRef('')
    const profileDocumentRef = useRef('')
    const customerLookup = useMutation({
        mutationFn: findCustomerByCpfCnpj,
        onSuccess: (customer, document) => {
            if (!customer || documentRef.current !== document || digitsOnly(customer.cpfCnpj ?? '') !== document) return
            profileDocumentRef.current = document
            setForm((current) => ({
                ...current,
                name: customer.name,
                email: customer.email,
                phone: customer.phone ?? '',
                postalCode: formatPostalCode(customer.postalCode ?? ''),
                address: customer.address ?? '',
                addressNumber: customer.addressNumber ?? '',
                addressComplement: customer.addressComplement ?? '',
                province: customer.province ?? '',
            }))
        },
    })
    const pendingOperation = useRef<
        | { kind: 'payment'; input: CreatePaymentInput }
        | { kind: 'subscription'; input: CreateSubscriptionInput }
        | null
    >(null)
    const mutation = useMutation({
        mutationFn: async () => {
            const operation = pendingOperation.current
            pendingOperation.current = null
            if (!operation) throw new Error('Dados da cobrança indisponíveis.')
            return operation.kind === 'payment'
                ? { kind: operation.kind, result: await createPayment(operation.input) }
                : { kind: operation.kind, result: await createSubscription(operation.input) }
        },
        onSuccess: async (operation) => {
            await queryClient.invalidateQueries({ queryKey: ['payments'] })
            await queryClient.invalidateQueries({ queryKey: ['subscriptions'] })
            if (operation.kind === 'subscription') {
                navigate('/subscriptions')
            } else if (operation.result.threeDSecureChallengeUrl) {
                window.location.assign(operation.result.threeDSecureChallengeUrl)
            } else {
                navigate(`/payments/${operation.result.id}`)
            }
        },
    })
    function update(field: keyof typeof form, value: string) { setForm((current) => ({ ...current, [field]: value })) }
    function updateCard(field: keyof CardFormValues, value: string) {
        setCard((current) => ({ ...current, [field]: value }))
        setCardValidationError(null)
    }
    function updateDocument(value: string) {
        const document = digitsOnly(value)
        documentRef.current = document
        if (profileDocumentRef.current && profileDocumentRef.current !== document) {
            profileDocumentRef.current = ''
            setForm((current) => ({ ...current, name: '', email: '', phone: '', postalCode: '', address: '', addressNumber: '', addressComplement: '', province: '' }))
        }
        update('cpfCnpj', value)
        if (document.length !== 11 && document.length !== 14) {
            lastDocumentLookup.current = ''
            setLookupDocument('')
        } else if (lastDocumentLookup.current !== document) {
            lastDocumentLookup.current = document
            setLookupDocument(document)
            customerLookup.mutate(document)
        }
    }
    function submit(event: FormEvent<HTMLFormElement>) {
        event.preventDefault()
        mutation.reset()
        const cardError = form.paymentMethod === 'creditCard' ? validateCard(card) : null
        setCardValidationError(cardError)
        if (cardError) return

        const customer = { name: form.name.trim(), email: form.email.trim(), cpfCnpj: form.cpfCnpj.trim(), phone: form.phone.trim(), postalCode: digitsOnly(form.postalCode), address: form.address.trim(), addressNumber: form.addressNumber.trim(), addressComplement: form.addressComplement.trim(), province: form.province.trim() }
        const cardDetails = form.paymentMethod === 'creditCard' ? cardPayload(card, customer) : null
        const customerDetails = { ...customer, cpfCnpj: customer.cpfCnpj || null, phone: customer.phone || null, addressComplement: customer.addressComplement || null }
        if (mode === 'subscription') {
            pendingOperation.current = {
                kind: 'subscription',
                input: {
                    ...customerDetails,
                    value: Number(form.value),
                    paymentMethod: form.paymentMethod,
                    cycle: form.cycle,
                    nextDueDate: form.dueDate,
                    description: form.description.trim() || 'Assinatura da plataforma',
                    saveCardForFutureUse: form.saveCardForFutureUse,
                    ...(cardDetails ?? {}),
                },
            }
        } else {
            pendingOperation.current = {
                kind: 'payment',
                input: {
                    ...customerDetails,
                    value: Number(form.value),
                    dueDate: form.dueDate,
                    description: form.description.trim() || 'Pagamento de serviço',
                    paymentMethod: form.paymentMethod,
                    saveCardForFutureUse: form.saveCardForFutureUse,
                    ...(cardDetails ? { ...cardDetails, deviceInfo: collectDeviceInfo() } : {}),
                },
            }
        }
        mutation.mutate()
    }

    return <section className="page-enter form-page">
        <Link className="back-link" to={mode === 'subscription' ? '/subscriptions' : '/'}><ArrowLeft size={16} /> Voltar para {mode === 'subscription' ? 'assinaturas' : 'cobranças'}</Link>
        <div className="page-heading"><div className="eyebrow">NOVA OPERAÇÃO</div><h1>Criar cobrança</h1><p className="heading-copy">Escolha o formato, meio de pagamento e condições.</p></div>
        <div className="operation-mode" role="group" aria-label="Formato da cobrança">
            <button type="button" aria-pressed={mode === 'oneOff'} onClick={() => setMode('oneOff')}>Avulsa</button>
            <button type="button" aria-pressed={mode === 'subscription'} onClick={() => setMode('subscription')}>Assinatura recorrente</button>
        </div>
        <form className="payment-form" onSubmit={submit}>
            <div className="form-section"><div className="form-section-heading"><span className="section-number">01</span><div><h2>Dados do cliente</h2><p>Informações para identificar a cobrança.</p></div></div><div className="form-grid">
                <label className="field field-wide"><span>Nome completo</span><input required maxLength={200} autoComplete="name" value={form.name} onChange={(event) => update('name', event.target.value)} placeholder="Nome do cliente" /></label>
                <label className="field"><span>E-mail</span><input required type="email" maxLength={320} autoComplete="email" value={form.email} onChange={(event) => update('email', event.target.value)} placeholder="cliente@email.com" /></label>
                <label className="field"><span>CPF ou CNPJ {form.paymentMethod === 'creditCard' ? <small>Obrigatório para cartão</small> : <small>Opcional</small>}</span><input required={form.paymentMethod === 'creditCard'} maxLength={20} value={form.cpfCnpj} onChange={(event) => updateDocument(event.target.value)} placeholder="000.000.000-00" /></label>
                <label className="field"><span>Telefone {form.paymentMethod === 'creditCard' ? <small>Obrigatório para cartão</small> : <small>Opcional</small>}</span><input required={form.paymentMethod === 'creditCard'} type="tel" maxLength={30} autoComplete="tel" value={form.phone} onChange={(event) => update('phone', event.target.value)} placeholder="(11) 99999-9999" /></label>
                {customerLookup.isPending && customerLookup.variables === lookupDocument && <p className="customer-lookup-note" role="status">Consultando cadastro...</p>}
                {customerLookup.data && customerLookup.variables === lookupDocument && <p className="customer-lookup-note" role="status"><Check size={14} /> Cliente localizado; dados preenchidos.</p>}
                {customerLookup.isError && customerLookup.variables === lookupDocument && <p className="customer-lookup-note lookup-error" role="status">Não foi possível consultar o cadastro salvo.</p>}
            </div></div>
            <div className="form-section"><div className="form-section-heading"><span className="section-number">02</span><div><h2>Endereço do cliente</h2><p>Informe o endereço utilizado no cadastro fiscal do tomador.</p></div></div><div className="form-grid">
                <label className="field"><span>CEP</span><input required inputMode="numeric" autoComplete="postal-code" maxLength={9} value={form.postalCode} onChange={(event) => update('postalCode', formatPostalCode(event.target.value))} placeholder="00000-000" /></label>
                <label className="field field-wide"><span>Logradouro</span><input required maxLength={255} autoComplete="address-line1" value={form.address} onChange={(event) => update('address', event.target.value)} placeholder="Rua ou avenida" /></label>
                <label className="field"><span>Número</span><input required maxLength={20} autoComplete="address-line2" value={form.addressNumber} onChange={(event) => update('addressNumber', event.target.value)} placeholder="Número" /></label>
                <label className="field"><span>Bairro</span><input required maxLength={100} value={form.province} onChange={(event) => update('province', event.target.value)} placeholder="Bairro" /></label>
                <label className="field field-wide"><span>Complemento <small>Opcional</small></span><input maxLength={255} autoComplete="address-line3" value={form.addressComplement} onChange={(event) => update('addressComplement', event.target.value)} placeholder="Apartamento, sala..." /></label>
            </div></div>
            <div className="form-section"><div className="form-section-heading"><span className="section-number">03</span><div><h2>{mode === 'subscription' ? 'Condições da assinatura' : 'Pagamento'}</h2><p>{mode === 'subscription' ? 'Defina o valor e a frequência das próximas cobranças.' : 'Pagamento integral, sem parcelamento.'}</p></div></div><div className="form-grid">
                <label className="field"><span>{mode === 'subscription' ? 'Valor por ciclo' : 'Valor total'}</span><div className="amount-input"><span>R$</span><input required type="number" min="0.01" max="9999999.99" step="0.01" inputMode="decimal" value={form.value} onChange={(event) => update('value', event.target.value)} placeholder="0,00" /></div></label>
                <label className="field"><span>{mode === 'subscription' ? 'Primeiro vencimento' : 'Vencimento'}</span><div className="input-with-icon"><CalendarDays size={16} /><input required type="date" min={mode === 'subscription' ? defaultDueDate() : undefined} value={form.dueDate} onChange={(event) => update('dueDate', event.target.value)} /></div></label>
                <label className="field"><span>Meio de pagamento</span><select value={form.paymentMethod} onChange={(event) => update('paymentMethod', event.target.value)}><option value="pix">PIX</option><option value="boleto">Boleto</option><option value="creditCard">Cartão de crédito</option></select></label>
                {mode === 'subscription' && <label className="field"><span>Ciclo de recorrência</span><select required value={form.cycle} onChange={(event) => update('cycle', event.target.value)}>{billingCycles.map((cycle) => <option key={cycle.value} value={cycle.value}>{cycle.label}</option>)}</select></label>}
                <label className="field field-wide"><span>Descrição <small>Opcional</small></span><input maxLength={mode === 'subscription' ? 150 : 200} value={form.description} onChange={(event) => update('description', event.target.value)} placeholder="Ex.: Consultoria mensal" /></label>
            </div>{form.paymentMethod === 'creditCard' && <>
                <CreditCardFields card={card} update={updateCard} />
                {cardValidationError && <div className="form-error" role="alert"><CircleAlert size={17} /> {cardValidationError}</div>}
                <label className="setting-option save-card-option"><input type="checkbox" checked={form.saveCardForFutureUse} onChange={(event) => setForm((current) => ({ ...current, saveCardForFutureUse: event.target.checked }))} /><span>Salvar o token Asaas para futuras cobranças. PAN e CVV não serão armazenados.</span></label>
            </>}
                <div className="payment-security-note"><WalletCards size={18} /><span>{form.paymentMethod === 'creditCard' ? 'Os dados do cartão são enviados ao Asaas pela API e descartados após o envio.' : mode === 'subscription' ? 'As instruções de cada ciclo serão exibidas nesta plataforma.' : 'As instruções de pagamento serão exibidas nesta plataforma.'}</span></div></div>
            {mutation.isError && <div className="form-error"><CircleAlert size={17} /> {mutation.error.message}</div>}
            <div className="form-footer"><span><ShieldCheck size={16} /> Dados enviados com segurança ao backend</span><button className="button button-primary" type="submit" disabled={mutation.isPending}>{mutation.isPending ? <><span className="button-spinner" /> Criando...</> : <>{mode === 'subscription' ? 'Criar assinatura' : 'Criar cobrança'} <ArrowRight size={17} /></>}</button></div>
        </form>
    </section>
}

const billingCycles: { value: BillingCycle; label: string }[] = [
    { value: 'weekly', label: 'Semanal' },
    { value: 'biweekly', label: 'Quinzenal' },
    { value: 'monthly', label: 'Mensal' },
    { value: 'bimonthly', label: 'Bimestral' },
    { value: 'quarterly', label: 'Trimestral' },
    { value: 'semiannually', label: 'Semestral' },
    { value: 'yearly', label: 'Anual' },
]

function CreditCardFields({ card, update }: { card: CardFormValues; update: (field: keyof CardFormValues, value: string) => void }) {
    return <div className="form-section card-entry-section">
        <div className="form-section-heading"><span className="section-number">04</span><div><h2>Dados do cartão</h2><p>Informações encaminhadas à Asaas para processar o pagamento.</p></div></div>
        <div className="form-grid">
            <label className="field field-wide"><span>Nome impresso no cartão</span><input required autoComplete="cc-name" maxLength={200} value={card.holderName} onChange={(event) => update('holderName', event.target.value)} /></label>
            <label className="field field-wide"><span>Número do cartão</span><input required inputMode="numeric" autoComplete="cc-number" maxLength={23} value={card.number} onChange={(event) => update('number', event.target.value)} /></label>
            <label className="field"><span>Mês de validade</span><input required inputMode="numeric" autoComplete="cc-exp-month" maxLength={2} value={card.expiryMonth} onChange={(event) => update('expiryMonth', event.target.value)} placeholder="MM" /></label>
            <label className="field"><span>Ano de validade</span><input required inputMode="numeric" autoComplete="cc-exp-year" maxLength={4} value={card.expiryYear} onChange={(event) => update('expiryYear', event.target.value)} placeholder="AAAA" /></label>
            <label className="field"><span>Código de segurança</span><input required type="password" inputMode="numeric" autoComplete="cc-csc" maxLength={4} value={card.ccv} onChange={(event) => update('ccv', event.target.value)} /></label>
        </div>
    </div>
}

export function NewSubscriptionPage() {
    return <Navigate to="/payments/new?mode=subscription" replace />
}

export function SubscriptionsPage() {
    const query = useQuery({ queryKey: ['subscriptions'], queryFn: getSubscriptions, refetchInterval: 10_000 })
    const subscriptions = query.data ?? []
    const active = subscriptions.filter((subscription) => subscription.status === 'ACTIVE').length
    const totalPerCycle = subscriptions.filter((subscription) => subscription.status === 'ACTIVE').reduce((sum, subscription) => sum + subscription.value, 0)

    return <section className="page-enter">
        <div className="page-heading heading-row"><div><div className="eyebrow">RECORRÊNCIA</div><h1>Assinaturas</h1><p className="heading-copy">Clientes, valores e cobranças geradas por ciclo.</p></div><Link className="button button-primary" to="/subscriptions/new"><Plus size={17} /> Nova assinatura</Link></div>
        <div className="metrics-row"><div className="metric"><span className="metric-label">ASSINATURAS</span><strong>{subscriptions.length}</strong></div><div className="metric"><span className="metric-label">ATIVAS</span><strong>{active}</strong></div><div className="metric"><span className="metric-label">VALOR POR CICLO ATIVO</span><strong>{currency.format(totalPerCycle)}</strong></div></div>
        <div className="table-toolbar"><div><h2>Clientes e ciclos</h2><span className="subtle-text">Cada cliente mantém suas próprias condições</span></div><button className="icon-button" type="button" title="Atualizar lista" aria-label="Atualizar lista" onClick={() => query.refetch()}><RefreshCw size={16} /></button></div>
        {query.isPending ? <div className="state-message"><span className="loading-mark" /> Carregando assinaturas</div> : query.isError ? <div className="state-message error-message"><CircleAlert size={18} /> {query.error.message}</div> : subscriptions.length === 0 ? <div className="empty-state"><div className="empty-icon"><Repeat2 size={23} /></div><h3>Nenhuma assinatura ainda</h3><p>Crie uma adesão para iniciar a recorrência.</p><Link className="button button-dark" to="/subscriptions/new"><Plus size={16} /> Criar assinatura</Link></div> :
            <div className="table-wrap"><table className="payments-table subscriptions-table"><thead><tr><th>CLIENTE</th><th>CICLO / MEIO</th><th>PRÓXIMO VENCIMENTO</th><th>STATUS</th><th>VALOR POR CICLO</th><th>AÇÕES</th></tr></thead><tbody>{subscriptions.map((subscription) => <SubscriptionRow key={`${subscription.id}:${subscription.value}`} subscription={subscription} />)}</tbody></table></div>}
    </section>
}

function SubscriptionRow({ subscription }: { subscription: Subscription }) {
    const queryClient = useQueryClient()
    const [value, setValue] = useState(String(subscription.value))
    const updateMutation = useMutation({ mutationFn: () => updateSubscriptionValue(subscription.id, Number(value)), onSuccess: async () => { await queryClient.invalidateQueries({ queryKey: ['subscriptions'] }) } })
    const cancelMutation = useMutation({ mutationFn: () => cancelSubscription(subscription.id), onSuccess: async () => { await queryClient.invalidateQueries({ queryKey: ['subscriptions'] }) } })
    const canEdit = subscription.status === 'ACTIVE'
    function cancel() {
        if (window.confirm(`Cancelar a assinatura de ${subscription.customerName}? O ciclo vigente será preservado.`)) cancelMutation.mutate()
    }

    return <>
        <tr><td><strong className="customer-name">{subscription.customerName}</strong><span className="table-subtext">{subscription.customerEmail}</span></td><td>{cycleLabel(subscription.cycle)}<span className="table-subtext method-cell">{methodIcon(subscription.paymentMethod)}{methodLabel(subscription.paymentMethod)}</span></td><td>{formatDate(subscription.nextDueDate)}</td><td><StatusBadge status={subscription.status} /></td><td><div className="inline-value"><span>R$</span><input aria-label={`Novo valor para ${subscription.customerName}`} type="number" min="0.01" max="9999999.99" step="0.01" value={value} disabled={!canEdit} onChange={(event) => setValue(event.target.value)} /><button className="icon-button" type="button" title="Salvar valor para os próximos ciclos" aria-label={`Salvar valor para ${subscription.customerName}`} disabled={!canEdit || updateMutation.isPending || Number(value) === subscription.value} onClick={() => updateMutation.mutate()}><Save size={15} /></button></div>{updateMutation.isError && <span className="inline-error">{updateMutation.error.message}</span>}</td><td><div className="subscription-actions"><button className="icon-button cancel-button" type="button" title="Cancelar assinatura" aria-label={`Cancelar assinatura de ${subscription.customerName}`} disabled={['CREATING', 'CREATION_FAILED', 'INACTIVE', 'DELETED', 'EXPIRED'].includes(subscription.status) || cancelMutation.isPending} onClick={cancel}><Ban size={16} /></button></div></td></tr>
        <tr className="cycle-row"><td colSpan={6}><details className="cycle-details"><summary>Cobranças geradas: {subscription.payments.length}</summary>{subscription.payments.length === 0 ? <p>Nenhuma cobrança de ciclo recebida ainda.</p> : <div className="cycle-list">{subscription.payments.map((payment) => <div className="cycle-item" key={payment.id}><span>{formatDate(payment.dueDate)}</span><StatusBadge status={payment.status} /><strong>{currency.format(payment.value)}</strong>{payment.pixQrCodeUrl && <img className="cycle-qr" src={payment.pixQrCodeUrl} alt="QR Code PIX" />}{payment.pixCopyPaste && <CopyButton value={payment.pixCopyPaste} label="Copiar PIX" />}{payment.bankSlipIdentificationField && <><code className="cycle-digitable-line">{payment.bankSlipIdentificationField}</code><CopyButton value={payment.bankSlipIdentificationField} label="Copiar linha digitável" /></>}{payment.bankSlipUrl && <a className="icon-button" href={payment.bankSlipUrl} target="_blank" rel="noreferrer" aria-label="Abrir PDF do boleto" title="Abrir PDF do boleto"><FileText size={15} /></a>}</div>)}</div>}</details></td></tr>
    </>
}

export function PaymentDetailsPage() {
    const { id } = useParams()
    const query = useQuery({ queryKey: ['payment', id], queryFn: () => getPayment(id!), enabled: Boolean(id), refetchInterval: (current) => !current.state.data || !finalStatuses.has(current.state.data.status) ? 5_000 : false, refetchIntervalInBackground: false })
    const payment = query.data
    if (query.isPending) return <div className="state-message"><span className="loading-mark" /> Carregando cobrança</div>
    if (query.isError || !payment) return <div className="state-message error-message"><CircleAlert size={18} /> {query.error?.message ?? 'Cobrança não encontrada.'}</div>
    return <section className="page-enter details-page">
        <Link className="back-link" to="/"><ArrowLeft size={16} /> Todas as cobranças</Link>
        <div className="page-heading heading-row details-heading"><div><div className="eyebrow">DETALHE DA COBRANÇA</div><h1>#{payment.id.slice(0, 8).toUpperCase()}</h1><p className="heading-copy">Criada em {formatDate(payment.createdAt.slice(0, 10))}</p></div><StatusBadge status={payment.status} large /></div>
        <div className="details-layout"><div className="details-main">
            <div className="amount-panel"><div><span className="metric-label">VALOR TOTAL</span><strong>{currency.format(payment.value)}</strong></div><div className="amount-panel-meta"><span>{methodIcon(payment.paymentMethod)} {methodLabel(payment.paymentMethod)}</span><span><CalendarDays size={15} /> Vence em {formatDate(payment.dueDate)}</span></div></div>
            <PaymentInstructions payment={payment} />
            <div className="invoice-row"><div className="invoice-icon"><FileText size={19} /></div><div className="invoice-copy"><strong>Nota fiscal de serviço</strong><span>{invoiceLabel(payment.invoiceStatus)}</span></div>{payment.invoicePdfUrl && <a className="icon-button" href={payment.invoicePdfUrl} target="_blank" rel="noreferrer" title="Abrir PDF da NFS-e" aria-label="Abrir PDF da NFS-e"><ExternalLink size={16} /></a>}</div>
        </div><aside className="details-side"><div className="side-heading"><h2>Atividade</h2><button className="icon-button" type="button" aria-label="Atualizar status" title="Atualizar status" onClick={() => query.refetch()}><RefreshCw size={15} /></button></div>
                <div className="timeline"><div className="timeline-item complete"><span className="timeline-dot"><Check size={11} /></span><div><strong>Cobrança criada</strong><small>{formatDateTime(payment.createdAt)}</small></div></div><div className={`timeline-item${finalStatuses.has(payment.status) ? ' complete' : ' current'}`}><span className="timeline-dot">{finalStatuses.has(payment.status) ? <Check size={11} /> : <Clock3 size={11} />}</span><div><strong>{statusLabel(payment.status)}</strong><small>{finalStatuses.has(payment.status) ? 'Atualizado pelo webhook Asaas' : 'Aguardando confirmação do pagamento'}</small></div></div>{payment.invoiceStatus && <div className={`timeline-item${payment.invoiceStatus === 'AUTHORIZED' ? ' complete' : ''}`}><span className="timeline-dot"><FileText size={11} /></span><div><strong>NFS-e: {invoiceLabel(payment.invoiceStatus)}</strong><small>Atualização recebida da Asaas</small></div></div>}</div>
                {!finalStatuses.has(payment.status) && <p className="polling-note"><span className="pulse-dot" /> Atualização automática a cada 5 segundos</p>}<div className="provider-note"><ShieldCheck size={16} /><span>O status é sincronizado por webhook e salvo nesta aplicação.</span></div>
            </aside></div>
    </section>
}

function PaymentInstructions({ payment }: { payment: PaymentResponse }) {
    if (payment.paymentMethod === 'pix') return <section className="instruction-panel"><div className="instruction-heading"><QrCode size={18} /><div><h2>Pagamento via PIX</h2><p>Escaneie o QR Code ou copie o código.</p></div></div>{payment.pixQrCodeUrl && <img className="pix-qr" src={payment.pixQrCodeUrl} alt="QR Code PIX para pagamento" />}{payment.pixCopyPaste && <div className="copy-line"><code>{payment.pixCopyPaste}</code><CopyButton value={payment.pixCopyPaste} label="Copiar código PIX" /></div>}</section>
    if (payment.paymentMethod === 'boleto') return <section className="instruction-panel"><div className="instruction-heading"><Banknote size={18} /><div><h2>Boleto bancário</h2><p>Use a linha digitável ou abra o PDF.</p></div></div>{payment.bankSlipIdentificationField && <div className="copy-line"><code>{payment.bankSlipIdentificationField}</code><CopyButton value={payment.bankSlipIdentificationField} label="Copiar linha digitável" /></div>}{payment.bankSlipUrl && <a className="button button-secondary" href={payment.bankSlipUrl} target="_blank" rel="noreferrer">Abrir PDF do boleto <ArrowUpRight size={16} /></a>}</section>
    if (payment.paymentMethod === 'creditCard') return <section className="instruction-panel"><div className="instruction-heading"><CreditCard size={18} /><div><h2>Pagamento via cartão</h2><p>{payment.status === 'AWAITING_RISK_ANALYSIS' ? 'Pagamento em análise pela operadora.' : 'Aguardando atualização do pagamento pela Asaas.'}</p></div></div></section>
    return <section className="instruction-panel"><div className="instruction-heading"><Clock3 size={18} /><div><h2>Instruções indisponíveis</h2><p>Consulte o status atualizado ou verifique a configuração da integração.</p></div></div></section>
}

function CopyButton({ value, label }: { value: string; label: string }) {
    const [copied, setCopied] = useState(false)
    async function copy() {
        try {
            await navigator.clipboard.writeText(value)
            setCopied(true)
            window.setTimeout(() => setCopied(false), 1800)
        } catch {
            setCopied(false)
        }
    }
    return <button className="icon-button" type="button" title={copied ? 'Copiado' : label} aria-label={copied ? 'Copiado' : label} onClick={copy}>{copied ? <Check size={15} /> : <Copy size={15} />}</button>
}

function StatusBadge({ status, large = false }: { status: string; large?: boolean }) {
    const tone = ['CONFIRMED', 'RECEIVED', 'RECEIVED_IN_CASH', 'ACTIVE'].includes(status) ? 'success' : ['PENDING', 'AWAITING_RISK_ANALYSIS', 'PROCESSING', 'CREATING'].includes(status) ? 'pending' : ['OVERDUE', 'REFUNDED', 'CHARGEBACK', 'CREATION_FAILED', 'INACTIVE', 'DELETED'].includes(status) ? 'danger' : 'neutral'
    return <span className={`status-badge ${tone}${large ? ' status-large' : ''}`}><span />{statusLabel(status)}</span>
}

function methodIcon(method: string) { if (method === 'pix') return <QrCode size={15} />; if (method === 'boleto') return <Banknote size={15} />; if (method === 'creditCard') return <CreditCard size={15} />; return <WalletCards size={15} /> }
function methodLabel(method: string) { return ({ undefined: 'Método legado', pix: 'PIX', boleto: 'Boleto', creditCard: 'Cartão de crédito' } as Record<string, string>)[method] ?? method }
function cycleLabel(cycle: string) { return ({ weekly: 'Semanal', biweekly: 'Quinzenal', monthly: 'Mensal', bimonthly: 'Bimestral', quarterly: 'Trimestral', semiannually: 'Semestral', yearly: 'Anual' } as Record<string, string>)[cycle] ?? cycle }
function statusLabel(status: string) { return ({ PENDING: 'Aguardando pagamento', CONFIRMED: 'Pagamento confirmado', RECEIVED: 'Pagamento recebido', RECEIVED_IN_CASH: 'Recebido em dinheiro', OVERDUE: 'Vencido', REFUNDED: 'Estornado', REFUND_REQUESTED: 'Estorno solicitado', CHARGEBACK: 'Contestado', DELETED: 'Removido', CREATING: 'Criando cobrança', CREATION_FAILED: 'Falha ao criar', ACTIVE: 'Ativa', INACTIVE: 'Cancelada', EXPIRED: 'Expirada', AWAITING_RISK_ANALYSIS: 'Em análise', PROCESSING: 'Processando' } as Record<string, string>)[status] ?? status.replaceAll('_', ' ').toLocaleLowerCase('pt-BR') }
function invoiceLabel(status?: string | null) { return !status ? 'Será agendada após a confirmação do pagamento.' : ({ CONFIGURATION_REQUIRED: 'Aguardando configuração fiscal', SCHEDULING: 'Agendando emissão', SCHEDULE_FAILED: 'Falha no agendamento', SCHEDULED: 'Agendada, aguardando autorização municipal', AUTHORIZED: 'Autorizada', ERROR: 'Erro de autorização' } as Record<string, string>)[status] ?? status }
function digitsOnly(value: string) { return value.replace(/\D/g, '') }
function formatPostalCode(value: string) { const digits = digitsOnly(value).slice(0, 8); return digits.length > 5 ? `${digits.slice(0, 5)}-${digits.slice(5)}` : digits }
function customerAddress(customer: Customer) {
    const street = [customer.address, customer.addressNumber].filter(Boolean).join(', ')
    const details = [street, customer.addressComplement, customer.province, customer.postalCode ? `CEP ${formatPostalCode(customer.postalCode)}` : null].filter(Boolean)
    return details.length ? details.join(' · ') : 'Endereço não informado'
}
function formatDate(date: string) { return new Intl.DateTimeFormat('pt-BR', { dateStyle: 'medium', timeZone: 'UTC' }).format(new Date(`${date}T00:00:00Z`)) }
function formatDateTime(date: string) { return new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(date)) }
function defaultDueDate() { const date = new Date(); date.setDate(date.getDate() + 1); return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}` }