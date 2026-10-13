import { useDeferredValue, useState, type FormEvent, type ReactNode } from 'react'
import { Link, NavLink, Outlet, useNavigate, useParams } from 'react-router'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft, ArrowRight, ArrowUpRight, Banknote, CalendarDays, Check, CircleAlert, Clock3, Copy, CreditCard, ExternalLink, FileText, LayoutDashboard, Plus, QrCode, ReceiptText, RefreshCw, Search, ShieldCheck, WalletCards } from 'lucide-react'
import { createPayment, getPayment, getPayments, type CreatePaymentInput, type PaymentResponse } from './api'

const currency = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' })
const finalStatuses = new Set(['CONFIRMED', 'RECEIVED', 'RECEIVED_IN_CASH', 'OVERDUE', 'REFUNDED', 'REFUND_REQUESTED', 'CHARGEBACK', 'DELETED'])

export default function PaymentsApp() {
    return <div className="app-shell">
        <aside className="sidebar">
            <Link className="brand" to="/" aria-label="Pagamentos, início"><span className="brand-mark"><ReceiptText size={19} /></span><span>pagamentos<span className="brand-period">.</span></span></Link>
            <div className="nav-caption">OPERAÇÃO</div>
            <nav className="primary-nav" aria-label="Navegação principal">
                <NavLink to="/" end className="nav-link"><LayoutDashboard size={18} /><span>Cobranças</span></NavLink>
                <NavLink to="/payments/new" className="nav-link"><Plus size={18} /><span>Nova cobrança</span></NavLink>
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

type PaymentChoice = 'hosted' | 'pix' | 'boleto'

export function NewPaymentPage() {
    const navigate = useNavigate()
    const queryClient = useQueryClient()
    const [choice, setChoice] = useState<PaymentChoice>('hosted')
    const [form, setForm] = useState({ name: '', email: '', cpfCnpj: '', phone: '', value: '', dueDate: defaultDueDate(), description: '' })
    const mutation = useMutation({ mutationFn: createPayment, onSuccess: async (payment) => { await queryClient.invalidateQueries({ queryKey: ['payments'] }); navigate(`/payments/${payment.id}`) } })
    function update(field: keyof typeof form, value: string) { setForm((current) => ({ ...current, [field]: value })) }
    function submit(event: FormEvent<HTMLFormElement>) {
        event.preventDefault()
        const hosted = choice === 'hosted'
        const request: CreatePaymentInput = { name: form.name.trim(), email: form.email.trim(), cpfCnpj: form.cpfCnpj.trim() || null, phone: form.phone.trim() || null, value: Number(form.value), dueDate: form.dueDate, description: form.description.trim() || 'Pagamento de serviço', paymentMethod: hosted ? 'undefined' : choice, checkoutMode: hosted ? 'hosted' : 'direct' }
        mutation.mutate(request)
    }

    return <section className="page-enter form-page">
        <Link className="back-link" to="/"><ArrowLeft size={16} /> Voltar para cobranças</Link>
        <div className="page-heading"><div className="eyebrow">NOVA OPERAÇÃO</div><h1>Criar cobrança</h1><p className="heading-copy">Cobrança única, à vista. A recorrência poderá ser ativada após a confirmação do primeiro pagamento.</p></div>
        <form className="payment-form" onSubmit={submit}>
            <div className="form-section"><div className="form-section-heading"><span className="section-number">01</span><div><h2>Dados do cliente</h2><p>Informações para identificar a cobrança.</p></div></div><div className="form-grid">
                <label className="field field-wide"><span>Nome completo</span><input required maxLength={200} autoComplete="name" value={form.name} onChange={(event) => update('name', event.target.value)} placeholder="Nome do cliente" /></label>
                <label className="field"><span>E-mail</span><input required type="email" maxLength={320} autoComplete="email" value={form.email} onChange={(event) => update('email', event.target.value)} placeholder="cliente@email.com" /></label>
                <label className="field"><span>CPF ou CNPJ <small>Opcional</small></span><input maxLength={20} value={form.cpfCnpj} onChange={(event) => update('cpfCnpj', event.target.value)} placeholder="000.000.000-00" /></label>
                <label className="field"><span>Telefone <small>Opcional</small></span><input type="tel" maxLength={30} autoComplete="tel" value={form.phone} onChange={(event) => update('phone', event.target.value)} placeholder="(11) 99999-9999" /></label>
            </div></div>
            <div className="form-section"><div className="form-section-heading"><span className="section-number">02</span><div><h2>Pagamento</h2><p>O valor será cobrado integralmente, sem parcelamento.</p></div></div><div className="form-grid">
                <label className="field"><span>Valor total</span><div className="amount-input"><span>R$</span><input required type="number" min="0.01" max="9999999.99" step="0.01" inputMode="decimal" value={form.value} onChange={(event) => update('value', event.target.value)} placeholder="0,00" /></div></label>
                <label className="field"><span>Vencimento</span><div className="input-with-icon"><CalendarDays size={16} /><input required type="date" value={form.dueDate} onChange={(event) => update('dueDate', event.target.value)} /></div></label>
                <label className="field field-wide"><span>Descrição <small>Opcional</small></span><input maxLength={200} value={form.description} onChange={(event) => update('description', event.target.value)} placeholder="Ex.: Consultoria mensal" /></label>
            </div><fieldset className="payment-choice-fieldset"><legend>Forma de pagamento</legend><div className="choice-list">
                <ChoiceButton selected={choice === 'hosted'} onClick={() => setChoice('hosted')} icon={<WalletCards size={19} />} title="Checkout Asaas" description="PIX, boleto ou cartão no ambiente seguro Asaas" />
                <ChoiceButton selected={choice === 'pix'} onClick={() => setChoice('pix')} icon={<QrCode size={19} />} title="PIX direto" description="QR Code e código copia e cola" />
                <ChoiceButton selected={choice === 'boleto'} onClick={() => setChoice('boleto')} icon={<Banknote size={19} />} title="Boleto bancário" description="Link do boleto para pagamento à vista" />
            </div></fieldset></div>
            {mutation.isError && <div className="form-error"><CircleAlert size={17} /> {mutation.error.message}</div>}
            <div className="form-footer"><span><ShieldCheck size={16} /> Dados enviados com segurança ao backend</span><button className="button button-primary" type="submit" disabled={mutation.isPending}>{mutation.isPending ? <><span className="button-spinner" /> Criando...</> : <>Criar cobrança <ArrowRight size={17} /></>}</button></div>
        </form>
    </section>
}

function ChoiceButton({ selected, onClick, icon, title, description }: { selected: boolean; onClick: () => void; icon: ReactNode; title: string; description: string }) {
    return <button className={`choice-option${selected ? ' selected' : ''}`} type="button" aria-pressed={selected} onClick={onClick}><span className="choice-icon">{icon}</span><span className="choice-copy"><strong>{title}</strong><small>{description}</small></span><span className="choice-check">{selected && <Check size={14} />}</span></button>
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
    const [copied, setCopied] = useState(false)
    async function copyPix() { if (!payment.pixCopyPaste) return; try { await navigator.clipboard.writeText(payment.pixCopyPaste); setCopied(true); window.setTimeout(() => setCopied(false), 1800) } catch { setCopied(false) } }
    if (payment.pixCopyPaste || payment.pixQrCodeUrl) return <section className="instruction-panel"><div className="instruction-heading"><QrCode size={18} /><div><h2>Pagamento via PIX</h2><p>Escaneie o QR Code ou copie o código.</p></div></div>{payment.pixQrCodeUrl && <img className="pix-qr" src={payment.pixQrCodeUrl} alt="QR Code PIX para pagamento" />}{payment.pixCopyPaste && <div className="copy-line"><code>{payment.pixCopyPaste}</code><button className="button button-secondary" onClick={copyPix} type="button">{copied ? <Check size={15} /> : <Copy size={15} />}{copied ? 'Copiado' : 'Copiar código'}</button></div>}</section>
    if (payment.bankSlipUrl) return <section className="instruction-panel"><div className="instruction-heading"><Banknote size={18} /><div><h2>Boleto bancário</h2><p>Abra o boleto para visualizar ou baixar.</p></div></div><a className="button button-secondary" href={payment.bankSlipUrl} target="_blank" rel="noreferrer">Abrir boleto <ArrowUpRight size={16} /></a></section>
    if (payment.invoiceUrl) return <section className="instruction-panel"><div className="instruction-heading"><ExternalLink size={18} /><div><h2>Checkout Asaas</h2><p>O cliente escolhe PIX, boleto ou cartão no ambiente seguro Asaas.</p></div></div><a className="button button-primary" href={payment.invoiceUrl} target="_blank" rel="noreferrer">Abrir checkout <ArrowUpRight size={16} /></a></section>
    return <section className="instruction-panel"><div className="instruction-heading"><Clock3 size={18} /><div><h2>Instruções indisponíveis</h2><p>Consulte o status atualizado ou verifique a configuração da integração.</p></div></div></section>
}

function StatusBadge({ status, large = false }: { status: string; large?: boolean }) {
    const tone = ['CONFIRMED', 'RECEIVED', 'RECEIVED_IN_CASH'].includes(status) ? 'success' : ['PENDING', 'AWAITING_RISK_ANALYSIS', 'PROCESSING', 'CREATING'].includes(status) ? 'pending' : ['OVERDUE', 'REFUNDED', 'CHARGEBACK', 'CREATION_FAILED'].includes(status) ? 'danger' : 'neutral'
    return <span className={`status-badge ${tone}${large ? ' status-large' : ''}`}><span />{statusLabel(status)}</span>
}

function methodIcon(method: string) { if (method === 'pix') return <QrCode size={15} />; if (method === 'boleto') return <Banknote size={15} />; if (method === 'creditCard') return <CreditCard size={15} />; return <WalletCards size={15} /> }
function methodLabel(method: string) { return ({ undefined: 'Checkout Asaas', pix: 'PIX', boleto: 'Boleto', creditCard: 'Cartão' } as Record<string, string>)[method] ?? method }
function statusLabel(status: string) { return ({ PENDING: 'Aguardando pagamento', CONFIRMED: 'Pagamento confirmado', RECEIVED: 'Pagamento recebido', RECEIVED_IN_CASH: 'Recebido em dinheiro', OVERDUE: 'Vencido', REFUNDED: 'Estornado', REFUND_REQUESTED: 'Estorno solicitado', CHARGEBACK: 'Contestado', DELETED: 'Removido', CREATING: 'Criando cobrança', CREATION_FAILED: 'Falha ao criar', AWAITING_RISK_ANALYSIS: 'Em análise', PROCESSING: 'Processando' } as Record<string, string>)[status] ?? status.replaceAll('_', ' ').toLocaleLowerCase('pt-BR') }
function invoiceLabel(status?: string | null) { return !status ? 'Será agendada após a confirmação do pagamento.' : ({ CONFIGURATION_REQUIRED: 'Aguardando configuração fiscal', SCHEDULING: 'Agendando emissão', SCHEDULE_FAILED: 'Falha no agendamento', SCHEDULED: 'Agendada, aguardando autorização municipal', AUTHORIZED: 'Autorizada', ERROR: 'Erro de autorização' } as Record<string, string>)[status] ?? status }
function formatDate(date: string) { return new Intl.DateTimeFormat('pt-BR', { dateStyle: 'medium', timeZone: 'UTC' }).format(new Date(`${date}T00:00:00Z`)) }
function formatDateTime(date: string) { return new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(date)) }
function defaultDueDate() { const date = new Date(); date.setDate(date.getDate() + 1); return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}` }