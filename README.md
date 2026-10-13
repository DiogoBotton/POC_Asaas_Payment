# POC de pagamentos Asaas

Aplicação de demonstração com API ASP.NET Core .NET 10, PostgreSQL, React/Vite e integração server-side com Asaas. As cobranças são sempre à vista. O cartão usa o checkout hospedado da Asaas; PIX e boleto também podem ser criados diretamente.

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

A confirmação da cobrança avulsa cria uma tarefa persistida para agendar a NFS-e vinculada ao pagamento. Para habilitar o agendamento, configure pelo User Secrets os campos `Asaas:Invoice:ServiceDescription`, `Asaas:Invoice:Observations`, `Asaas:Invoice:MunicipalServiceId` ou `Asaas:Invoice:MunicipalServiceCode`, `Asaas:Invoice:MunicipalServiceName`, `Asaas:Invoice:RetainIss`, `Asaas:Invoice:Iss`, `Asaas:Invoice:Pis`, `Asaas:Invoice:Cofins`, `Asaas:Invoice:Csll`, `Asaas:Invoice:Inss` e `Asaas:Invoice:Ir`.

Use somente os dados de serviço e tributação reais da empresa, validados para o município e regime fiscal. Não configure alíquotas de exemplo. Sem esses valores, o sistema mantém a nota como `CONFIGURATION_REQUIRED`; após configuração, o worker tenta agendá-la. Agendamento não significa autorização municipal: acompanhe `INVOICE_AUTHORIZED` e `INVOICE_ERROR`.

## Rotas implementadas

- `POST /api/payments` cria cobrança hospedada ou cobrança direta PIX/boleto.
- `GET /api/payments` lista cobranças locais.
- `GET /api/payments/{id}` consulta uma cobrança, suas instruções e estado da NFS-e.
- `POST /api/webhooks/asaas` autentica e enfileira eventos Asaas.

Exemplos de chamadas estão em `API_Payment_Asaas/API_Payment_Asaas/API_Payment_Asaas.http`.

## Verificações

```powershell
dotnet build .\API_Payment_Asaas\API_Payment_Asaas.slnx
pnpm --dir .\Front_Payment_Asaas lint
pnpm --dir .\Front_Payment_Asaas build
```

Teste cobranças reais apenas no Sandbox. Esta POC não processa PAN/CVV no backend e não substitui uma revisão de segurança/PCI para produção.