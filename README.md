# DengueAlert API

API de alertas de **dengue, chikungunya e zika** para qualquer município do Brasil. Os dados vêm do
[InfoDengue](https://info.dengue.mat.br/) e ficam guardados em MySQL, de modo que o front não depende da fonte
externa a cada consulta.

Front-end: [dengue-alert-front-end](https://github.com/jhonvss1/dengue-alert-front-end).

## Como funciona (estratégia híbrida)

1. **Cadastro de cidades** – na primeira execução a API carrega os ~5.570 municípios do IBGE (`Cities`).
2. **Primeira consulta de uma cidade** – a API busca as 3 doenças na fonte (último ano), grava no banco e marca
   a cidade como *acompanhada*. As consultas seguintes saem direto do banco.
3. **Job semanal** – toda segunda às 03:00 (horário de Brasília, configurável) as cidades acompanhadas são
   atualizadas. Como a fonte revisa semanas recentes, a atualização reavalia uma janela das últimas semanas e faz
   *upsert*. Se a API estiver fora do ar no horário, a rodada perdida é recuperada ao subir.
4. **Resiliência** – se a fonte estiver fora do ar e a cidade já tiver dados, a API devolve o último dado salvo com
   `isStale: true`. Se a cidade ainda não tiver dados, responde `503`.

Consultas simultâneas à mesma cidade (ainda sem dados) disparam **uma única** busca na fonte.

## Endpoints

| Método | Rota | Descrição |
|---|---|---|
| GET | `/api/cities?search=&state=&page=&pageSize=` | Busca municípios (paginado, `pageSize` ≤ 100) |
| GET | `/api/cities/{geocode}` | Detalhe de um município (geocode IBGE de 7 dígitos) |
| GET | `/api/cities/{geocode}/alerts?disease=&weeks=` | Série semanal. `disease`: `dengue`, `chikungunya` ou `zika` (omitido = todas). `weeks`: 1–104 (padrão 12) |
| GET | `/api/cities/{geocode}/alerts/latest` | Semana mais recente de cada doença |
| GET | `/health` | Health check (inclui o banco) |
| POST | `/api/admin/sync` | Dispara sincronização. Body opcional `{"geocodes":[...]}` para pré-carregar cidades |
| GET | `/api/admin/sync/runs` | Últimas rodadas de sincronização |
| POST | `/api/admin/cities/refresh` | Recarrega o cadastro de municípios do IBGE |

Os endpoints `/api/admin/*` exigem o header `X-Api-Key` (configuração `Admin:ApiKey`); sem chave configurada eles
ficam desabilitados (`404`).

Exemplo de resposta de `GET /api/cities/3106200/alerts?weeks=2&disease=dengue`:

```json
{
  "city": { "geocode": 3106200, "name": "Belo Horizonte", "state": "MG", "isTracked": true, "lastSyncedAt": "2025-03-10T06:00:12Z" },
  "lastSyncedAt": "2025-03-10T06:00:12Z",
  "isStale": false,
  "alerts": [
    { "disease": "dengue", "year": 2025, "week": 10, "weekStart": "2025-03-02", "estimatedCases": 992, "reportedCases": 470,
      "incidencePer100k": 38.2, "alertLevel": 2, "updatedAt": "2025-03-10T06:00:12Z" }
  ]
}
```

`alertLevel`: 0 desconhecido, 1 verde, 2 amarelo, 3 laranja, 4 vermelho.

## Configuração

Tudo pode ser definido por variáveis de ambiente (`Secao__Chave`) ou `appsettings`. **Nunca** versione segredos.

| Chave | Padrão | Descrição |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | – | Connection string do MySQL (obrigatória) |
| `Database__ServerVersion` | `8.4.0` | Versão do MySQL (evita conectar para detectar) |
| `Cors__AllowedOrigins__0` | – | Origem do front (ex.: `https://dengue.brago.com.br`). Em Development, vazio libera tudo |
| `Admin__ApiKey` | – | Chave dos endpoints admin |
| `Sync__Enabled` | `true` | Liga/desliga o job semanal |
| `Sync__DayOfWeek` / `Sync__TimeOfDay` / `Sync__TimeZone` | `Monday` / `03:00` / `America/Sao_Paulo` | Agenda do job |
| `Sync__InitialHistoryWeeks` | `52` | Histórico carregado na primeira consulta |
| `Sync__RefreshWindowWeeks` | `8` | Janela reavaliada em cada atualização |
| `Sync__StaleAfterDays` | `14` | Dado mais velho que isso é atualizado na leitura |
| `Sync__MaxConcurrency` / `Sync__DelayBetweenCitiesMs` | `2` / `500` | Limites de carga sobre a fonte externa |

## Desenvolvimento local

Requisitos: .NET 9 SDK e Docker (para o MySQL).

```bash
cp .env.example .env            # preencha as senhas
docker compose up -d mysql      # MySQL em 127.0.0.1:3308

export ConnectionStrings__DefaultConnection="Server=127.0.0.1;Port=3308;Database=dengue_alert;User=dengue;Password=<sua-senha>;"
dotnet run                      # Swagger em http://localhost:5270
dotnet test                     # testes (SQLite em memória, sem rede)
```

As migrations são aplicadas automaticamente na subida. Para criar uma nova: `dotnet ef migrations add <Nome> -o Migrations`.
Para consultar o banco no navegador: `docker compose --profile tools up -d adminer` (http://127.0.0.1:8081).

## Deploy (servidor próprio + Cloudflare Tunnel)

```bash
cp .env.example .env   # preencha MYSQL_*, CORS_ORIGIN e ADMIN_API_KEY (openssl rand -hex 32)
docker compose up -d --build
```

Sobem o MySQL (volume `mysql_data`) e a API, publicada **somente em `127.0.0.1:8080`**. O MySQL não é exposto à
internet. Para publicar, adicione uma regra de ingress ao túnel que já existe (ou pelo painel *Zero Trust →
Networks → Tunnels → Public hostname*):

```yaml
ingress:
  - hostname: api-dengue.brago.com.br
    service: http://localhost:8080
  # ... suas outras regras ...
  - service: http_status:404
```

O front fica em `https://dengue.brago.com.br` (Cloudflare Pages). Pré-carregar cidades após o primeiro deploy
(opcional):

```bash
curl -X POST https://api-dengue.brago.com.br/api/admin/sync \
  -H "X-Api-Key: $ADMIN_API_KEY" -H "Content-Type: application/json" \
  -d '{"geocodes":[3106200,3550308,3304557]}'
```
