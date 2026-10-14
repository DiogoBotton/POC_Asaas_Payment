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

### Atalho usado no Sandbox

Em `appsettings.Development.json`, `Asaas:Invoice:UseFirstMunicipalService` está habilitado para facilitar o teste. O recurso só é aplicado quando a URL base contém `sandbox`. Ele consulta `GET /v3/fiscalInfo/services?offset=0&limit=1` e usa do primeiro item o `id`, a `description` e a alíquota `issTax`. A aplicação envia o `municipalServiceId` (sem enviar `municipalServiceCode` junto), usa a descrição da assinatura como `serviceDescription` quando disponível e define `observations` vazia quando não configurada.

Nesse modo de teste, `retainIss` assume `false` e `pis`, `cofins`, `csll`, `inss` e `ir` assumem `0` quando não configurados. O `iss` vem do primeiro serviço; se ele não retornar essa alíquota, usa o valor configurado ou `0`. `deductions` é enviado como `0` e `effectiveDate` como a data UTC atual.

Esse atalho é somente para testar o fluxo técnico no Sandbox: o primeiro serviço pode não corresponder ao serviço realmente prestado, e alíquotas zero ou retenção desativada podem ser fiscalmente incorretas. Não use esses defaults em Produção. Para configuração normal, desative `UseFirstMunicipalService` e informe por User Secrets `Asaas:Invoice:ServiceDescription`, `Observations`, `MunicipalServiceId` ou `MunicipalServiceCode`, `MunicipalServiceName`, `RetainIss`, `Iss`, `Pis`, `Cofins`, `Csll`, `Inss` e `Ir`, com valores confirmados para a empresa e o município. Uma configuração incompleta fora do modo Sandbox mantém a nota como `CONFIGURATION_REQUIRED`.

Agendar não significa que a prefeitura autorizou a nota. Acompanhe `INVOICE_AUTHORIZED` e `INVOICE_ERROR` por webhook.

## Rotas implementadas

- `POST /api/payments` cria cobrança avulsa com método explícito (`pix`, `boleto` ou `creditCard`) e retorna instruções de pagamento.
- `GET /api/payments` lista cobranças locais.
- `GET /api/payments/{id}` consulta uma cobrança, suas instruções e estado da NFS-e.
- `POST /api/subscriptions` cria uma assinatura para cliente, valor, meio, ciclo e primeiro vencimento informados; método e ciclo são escolhidos na tela de criação, sem configuração global.
- `GET /api/subscriptions` e `GET /api/subscriptions/{id}` consultam assinaturas e cobranças por ciclo.
- `PUT /api/subscriptions/{id}/value` altera o valor dos próximos ciclos daquela assinatura, sem alterar cobranças já geradas.
- `DELETE /api/subscriptions/{id}` inativa a assinatura, preserva a cobrança do ciclo vigente e remove cobranças pendentes de ciclos posteriores que já tenham sido geradas.
- `POST /api/webhooks/asaas` autentica e enfileira eventos Asaas.

Na tela `Nova cobrança`, selecione cobrança avulsa ou assinatura recorrente. Para assinaturas, escolha o meio e o ciclo no próprio formulário: semanal, quinzenal, mensal, bimestral, trimestral, semestral ou anual. PIX e boleto exibem código/QR Code ou linha digitável localmente; o PDF do boleto abre em uma nova aba. O cartão envia os dados de pagamento diretamente à API Asaas. A POC não parcela assinaturas.

Os campos PAN/CVV são recebidos transitoriamente pela API e enviados à Asaas, mas nunca persistidos pela aplicação. A tokenização só é solicitada quando o cliente marca explicitamente a opção de salvar o cartão; nesse caso, apenas o token Asaas protegido com Data Protection, bandeira e últimos quatro dígitos são persistidos. O token não é PAN/CVV. O armazenamento de tokens não habilita sozinho uma interface para reutilizá-los. Confirme com a Asaas a disponibilidade de tokenização no ambiente de produção. O envio de dados de cartão pelo backend tem implicações de escopo PCI; esta POC não substitui avaliação PCI, revisão de segurança ou aprovação para produção.

Para cartão avulso, `Asaas:EnableThreeDSecure` habilita o fluxo 3DS e exige metadados de dispositivo enviados pela interface. O retorno do navegador não confirma pagamento: use o status persistido e os webhooks Asaas como fonte de verdade. Não repita automaticamente uma criação após timeout incerto, pois a cobrança pode ter sido criada no provedor.

As migrations `20261014181547_RemoveHostedPaymentFieldsAndAddStoredCards` e `20261014191623_RemoveSubscriptionConfiguration` removem dados antigos de URLs hospedadas e a tabela de configuração global. Revise os dados existentes antes de aplicá-las; as opções de configuração antigas serão descartadas.

Exemplos de chamadas estão em `API_Payment_Asaas/API_Payment_Asaas/API_Payment_Asaas.http`.

## Verificações

```powershell
dotnet build .\API_Payment_Asaas\API_Payment_Asaas.slnx
dotnet test .\API_Payment_Asaas\API_Payment_Asaas.slnx
pnpm --dir .\Front_Payment_Asaas lint
pnpm --dir .\Front_Payment_Asaas build
```

Teste cobranças apenas no Sandbox. Para a API, configure `Asaas:ApiKey` e `Asaas:WebhookToken` via User Secrets. A configuração fiscal de desenvolvimento contém placeholders vazios, sem alíquotas de exemplo. Nunca use dados reais de cartão em desenvolvimento ou nos exemplos HTTP.