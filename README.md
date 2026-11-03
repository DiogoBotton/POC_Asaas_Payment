# POC de pagamentos Asaas

Aplicação de demonstração com API ASP.NET Core .NET 10, PostgreSQL, React/Vite e integração server-side com Asaas. A interface coleta o meio de pagamento e exibe localmente as instruções de PIX e boleto. Assinaturas geram cobranças recorrentes: cartão é automático; PIX e boleto geram uma cobrança por ciclo, paga manualmente pelo cliente.

## Requisitos

- .NET 10 SDK
- Node.js e pnpm
- Docker Desktop com Docker Compose
- Credenciais de Sandbox Asaas para criar cobranças

## Executar localmente

Na raiz do repositório, inicie o PostgreSQL:

```powershell
docker compose up -d postgres
```

O banco é publicado na porta `5433` para não conflitar com instalações locais de PostgreSQL na porta `5432`. A conexão local está em `appsettings.Development.json` e não contém credenciais Asaas.

Configure os segredos da API no User Secrets:

```powershell
dotnet user-secrets init --project .\API_Payment_Asaas\API_Payment_Asaas\API_Payment_Asaas.csproj
dotnet user-secrets set "Asaas:ApiKey" "<chave-de-sandbox>" --project .\API_Payment_Asaas\API_Payment_Asaas\API_Payment_Asaas.csproj
dotnet user-secrets set "Asaas:WebhookToken" "<token-aleatorio-com-ao-menos-32-caracteres>" --project .\API_Payment_Asaas\API_Payment_Asaas\API_Payment_Asaas.csproj
```

Aplique as migrações e inicie a API:

```powershell
dotnet ef database update --project .\API_Payment_Asaas\API_Payment_Asaas\API_Payment_Asaas.csproj
dotnet run --project .\API_Payment_Asaas\API_Payment_Asaas\API_Payment_Asaas.csproj --launch-profile http
```

A API e o documento OpenAPI ficam em `http://localhost:5020` e `http://localhost:5020/openapi/v1.json`.

Em outro terminal, inicie a interface:

```powershell
pnpm --dir .\Front_Payment_Asaas install
pnpm --dir .\Front_Payment_Asaas dev
```

A interface fica em `http://localhost:5173` e encaminha `/api` para a API local.

## Webhook Asaas

Cadastre no Sandbox uma URL pública que encaminhe para `POST /api/webhooks/asaas`, por exemplo com ngrok ou Cloudflare Tunnel. Configure o mesmo segredo de webhook em Asaas e no User Secrets `Asaas:WebhookToken`; ele deve ser diferente da chave da API. Os eventos são persistidos com ID único antes de responder 200 e processados em segundo plano.

## Emissão de NFS-e

A confirmação ou recebimento do pagamento cria uma tarefa persistida para agendar uma NFS-e vinculada à cobrança. O worker processa os webhooks em segundo plano e tenta agendar a nota após `PAYMENT_CONFIRMED` ou `PAYMENT_RECEIVED`.

### Campos exigidos pela Asaas

Para uma nota vinculada à cobrança, o `POST /v3/invoices` exige:

- Origem da nota: `payment` com o ID da cobrança Asaas. A API também aceita `installment` ou `customer` para outros fluxos.
- `serviceDescription`: descrição do serviço prestado.
- `observations`: observações da nota; pode ser uma string vazia quando não houver observações.
- `value`, `deductions` e `effectiveDate`.
- Serviço municipal: `municipalServiceId` ou `municipalServiceCode`, conforme a disponibilidade da prefeitura, e `municipalServiceName`.
- Objeto `taxes` com `retainIss`, `iss`, `pis`, `cofins`, `csll`, `inss` e `ir`.

Além do payload, a conta Asaas precisa ter suas informações fiscais configuradas conforme as exigências do município. Sem os dados obrigatórios, a Asaas responde 400. Para diagnóstico, a API registra o status HTTP do provedor e os códigos de erro retornados.

### Configuração fiscal

A tela `Fiscal` pesquisa o catálogo da Asaas por `GET /api/fiscal/municipal-services`; a seleção não é automática. Salve a configuração em `PUT /api/fiscal/configuration`, após confirmar serviço municipal, descrição, observações, retenção de ISS e alíquotas com o contador e as regras do município. O backend valida o serviço selecionado contra o catálogo Asaas. A configuração fica no PostgreSQL, não em `appsettings` ou User Secrets.

Cada NFS-e recebe um snapshot dos parâmetros fiscais usados no primeiro agendamento. Novas tentativas reutilizam esse snapshot mesmo se a configuração global mudar depois. Sem serviço municipal selecionado, a nota fica em `CONFIGURATION_REQUIRED`; configure os dados fiscais antes de operar e acompanhe `INVOICE_AUTHORIZED` e `INVOICE_ERROR` por webhook. Agendar não significa que a prefeitura autorizou a nota.

Agendar não significa que a prefeitura autorizou a nota. Acompanhe `INVOICE_AUTHORIZED` e `INVOICE_ERROR` por webhook.

## Rotas implementadas

- `POST /api/payments` cria cobrança avulsa com método explícito (`pix`, `boleto` ou `creditCard`) e retorna instruções de pagamento.
- `GET /api/payments` lista cobranças locais.
- `GET /api/payments/{id}` consulta uma cobrança, suas instruções e estado da NFS-e.
- `POST /api/subscriptions` cria uma assinatura mensal para cliente, valor, meio e dia de vencimento selecionado.
- `GET /api/subscriptions` e `GET /api/subscriptions/{id}` consultam assinaturas e cobranças por ciclo.
- `PUT /api/subscriptions/{id}/value` altera o valor dos próximos ciclos daquela assinatura, sem alterar cobranças já geradas.
- `DELETE /api/subscriptions/{id}` inativa a assinatura, preserva a cobrança do ciclo vigente e remove cobranças pendentes de ciclos posteriores que já tenham sido geradas.
- `POST /api/webhooks/asaas` autentica e enfileira eventos Asaas.

## Fluxo completo: assinatura até NFS-e

1. Inicie o PostgreSQL, a API e a interface seguindo **Executar localmente**. Configure `Asaas:ApiKey` e `Asaas:WebhookToken` no User Secrets.
2. Na tela `Fiscal`, consulte o catálogo de serviços municipais e salve a configuração antes de confirmar pagamentos. Sem ela, o webhook não consegue agendar a NFS-e.
3. Publique `POST /api/webhooks/asaas` por uma URL acessível pela Asaas e cadastre essa URL no Sandbox com o mesmo token. Habilite os eventos `PAYMENT_CONFIRMED` e `PAYMENT_RECEIVED` para iniciar o agendamento e os eventos de NFS-e que deseja acompanhar: `INVOICE_CREATED`, `INVOICE_UPDATED`, `INVOICE_SYNCHRONIZED`, `INVOICE_AUTHORIZED`, `INVOICE_PROCESSING_CANCELLATION`, `INVOICE_CANCELED`, `INVOICE_CANCELLATION_DENIED` e `INVOICE_ERROR`.
4. Crie a assinatura com `POST /api/subscriptions`. O corpo inclui os dados do cliente, o valor, o meio de pagamento e `billingDay` (6, 15 ou 25). A cobrança é mensal; a API calcula o próximo vencimento em São Paulo. Exemplo com PIX:

```http
POST http://localhost:5020/api/subscriptions
Content-Type: application/json

{
	"name": "Sandbox Customer",
	"email": "customer@example.com",
	"cpfCnpj": "12345678901",
	"postalCode": "01310-000",
	"address": "Av. Paulista",
	"addressNumber": "150",
	"province": "Bela Vista",
	"value": 89.90,
	"paymentMethod": "pix",
	"billingDay": 15,
	"description": "POC recurring subscription"
}
```

5. Não existe `POST /api/customers`: ao criar uma cobrança ou assinatura, a API procura o cliente localmente por CPF/CNPJ ou e-mail; se não encontrar, cria o cliente na Asaas e no banco local. Se encontrar, sincroniza seus dados. `GET /api/customers` lista os clientes locais e `GET /api/customers/lookup?cpfCnpj=...` consulta um por documento.
6. Após o pagamento da cobrança do ciclo, a Asaas envia o webhook e o worker agenda a NFS-e. A resposta de criação é `201 Created` e contém o ID local da assinatura. Use `GET /api/subscriptions/{id}` para consultar as cobranças geradas; use o ID local de uma cobrança em `GET /api/payments/{id}` para acompanhar `invoiceStatus` (status bruto da Asaas), `invoiceWorkflowStatus` (estado local) e, quando disponíveis, as URLs do PDF e XML. A autorização final é informada pelo evento `INVOICE_AUTHORIZED`; agendamento não significa autorização.

Na tela `Nova cobrança`, selecione cobrança avulsa ou assinatura recorrente. Para assinaturas, escolha o meio e um dia mensal fixo: 6, 15 ou 25. O primeiro vencimento é a próxima ocorrência futura desse dia no calendário de São Paulo; se o dia escolhido for hoje, o vencimento será no mês seguinte. PIX e boleto exibem código/QR Code ou linha digitável localmente; o PDF do boleto abre em uma nova aba. O cartão envia os dados de pagamento diretamente à API Asaas. A POC não parcela assinaturas.

Os campos PAN/CVV são recebidos transitoriamente pela API e enviados à Asaas, mas nunca persistidos pela aplicação. A tokenização só é solicitada quando o cliente marca explicitamente a opção de salvar o cartão; nesse caso, apenas o token Asaas protegido com Data Protection, bandeira e últimos quatro dígitos são persistidos. O token não é PAN/CVV. O armazenamento de tokens não habilita sozinho uma interface para reutilizá-los. Confirme com a Asaas a disponibilidade de tokenização no ambiente de produção. O envio de dados de cartão pelo backend tem implicações de escopo PCI; esta POC não substitui avaliação PCI, revisão de segurança ou aprovação para produção.

Para cartão avulso, `Asaas:EnableThreeDSecure` habilita o fluxo 3DS e exige metadados de dispositivo enviados pela interface. O retorno do navegador não confirma pagamento: use o status persistido e os webhooks Asaas como fonte de verdade. Não repita automaticamente uma criação após timeout incerto, pois a cobrança pode ter sido criada no provedor.

As migrations `AddSubscriptionBillingDay` e `AddFiscalConfigurationAndInvoiceSnapshot` persistem o dia mensal escolhido e a configuração fiscal com snapshots por nota. Assinaturas existentes mantêm `BillingDay` nulo e continuam exibindo seu ciclo histórico. As migrations `20261014181547_RemoveHostedPaymentFieldsAndAddStoredCards` e `20261014191623_RemoveSubscriptionConfiguration` removem dados antigos de URLs hospedadas e a configuração recorrente global.

Exemplos de chamadas estão em `API_Payment_Asaas/API_Payment_Asaas/API_Payment_Asaas.http`.

## Verificações

```powershell
dotnet build .\API_Payment_Asaas\API_Payment_Asaas.slnx
dotnet test .\API_Payment_Asaas\API_Payment_Asaas.slnx
pnpm --dir .\Front_Payment_Asaas lint
pnpm --dir .\Front_Payment_Asaas build
```

Teste cobranças apenas no Sandbox. Para a API, configure `Asaas:ApiKey` e `Asaas:WebhookToken` via User Secrets. Configure os dados fiscais pela tela `Fiscal`; eles ficam no PostgreSQL e não são definidos em `appsettings`. Nunca use dados reais de cartão em desenvolvimento ou nos exemplos HTTP.