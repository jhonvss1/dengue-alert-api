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
| `Cors__AllowedOrigins__0` | – | Origem do front (ex.: `https://dengue.braggo.com.br`). Em Development, vazio libera tudo |
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

## Deploy no seu servidor (API direto na VM + MySQL em Docker + Cloudflare Tunnel)

A API roda como serviço do `systemd`, ouvindo só em `127.0.0.1:8080`; o MySQL fica em Docker. Nenhum dos dois é
exposto à internet: só o túnel da Cloudflare chega na API.

**1. Portas.** Confira se `8080` e `3308` estão livres: `sudo ss -tulpn | grep -E ':8080|:3308'`.
(Para usar outra porta: `MYSQL_HOST_PORT` no `.env` e `ASPNETCORE_URLS` em `deploy/dengue-alert-api.service`.)

**2. MySQL em Docker.**

```bash
git clone -b dev https://github.com/jhonvss1/dengue-alert-api.git && cd dengue-alert-api
cp .env.example .env && chmod 600 .env    # preencha MYSQL_ROOT_PASSWORD, MYSQL_USER e MYSQL_PASSWORD
docker compose up -d mysql                # 127.0.0.1:3308
```

**3. .NET na VM** (SDK 9 ou superior; o `apt` nem sempre tem a versão 9):

```bash
curl -sSL https://dot.net/v1/dotnet-install.sh -o dotnet-install.sh
sudo bash dotnet-install.sh --channel 9.0 --install-dir /usr/share/dotnet
sudo ln -sf /usr/share/dotnet/dotnet /usr/local/bin/dotnet
```

**4. Instalar o serviço.**

```bash
sudo deploy/install.sh
sudo nano /etc/dengue-alert-api.env       # senha do MySQL, Cors__AllowedOrigins__0 e Admin__ApiKey (openssl rand -hex 32)
sudo systemctl start dengue-alert-api
journalctl -u dengue-alert-api -f         # espere "Catálogo de municípios atualizado"
curl http://127.0.0.1:8080/health         # Healthy
```

O `install.sh` cria o usuário `dengue-alert`, publica em `/opt/dengue-alert-api` e registra o serviço (inicia no boot e
reinicia se cair). Ele só deixa você iniciar depois de preencher `/etc/dengue-alert-api.env`.

**5. Túnel da Cloudflare.** Em *Zero Trust → Networks → Tunnels → seu túnel → Public hostname → Add*:
`api-dengue.braggo.com.br` → `http://localhost:8080`. Ou, no `config.yml` do túnel:

```yaml
ingress:
  - hostname: api-dengue.braggo.com.br
    service: http://localhost:8080
  # ... suas outras regras ...
  - service: http_status:404
```

Se o `cloudflared` rodar em Docker, `localhost` não alcança a API: use o IP da VM na rede do Docker
(ex.: `http://172.17.0.1:8080`) e ouça nessa interface em `ASPNETCORE_URLS`.

**6. Atualizar** depois de um `git pull`: `sudo deploy/update.sh`.

O front fica em `https://dengue.braggo.com.br` (Cloudflare Pages). Pré-carregar cidades após o primeiro deploy
(opcional):

```bash
curl -X POST https://api-dengue.braggo.com.br/api/admin/sync \
  -H "X-Api-Key: $ADMIN_API_KEY" -H "Content-Type: application/json" \
  -d '{"geocodes":[3106200,3550308,3304557]}'
```

### Alternativa: API também em Docker

```bash
docker compose -f docker-compose.yml -f docker-compose.api.yml up -d --build   # exige CORS_ORIGIN e ADMIN_API_KEY no .env
```

### Boas práticas

- **Backup:** o volume `mysql_data` guarda os dados. Agende um `mysqldump` para fora da VM
  (`docker exec dengue-alert-mysql mysqldump -uroot -p... dengue_alert`).
- **Rate limit:** crie uma regra de rate limit na Cloudflare para `api-dengue.braggo.com.br/api/*`.
