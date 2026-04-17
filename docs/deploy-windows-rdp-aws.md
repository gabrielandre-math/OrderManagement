# OrderManagementApi — Guia Tecnico de Deploy: Windows Server via RDP (AWS EC2, Sem Docker)

| Componente | Tecnologia | Versao |
|---|---|---|
| API Principal | ASP.NET Core (Monolito Modular) | .NET 8 |
| Banco de Dados | PostgreSQL | 16.x |
| Message Broker | RabbitMQ | 3.x |
| Modulos | Catalog, Basket, Orders | — |
| Padroes | CQRS, MediatR, Carter, MassTransit, FluentValidation | — |
| Idiomas suportados | 43 culturas (en-US, pt-BR, es, fr, ja, zh-CN, etc.) | — |
| Repositorio | https://github.com/gabrielandre-math/OrderManagement | branch master |

---

## Sumario

1.  Visao Geral da Arquitetura
2.  Por que nao usar Docker no Windows EC2 (t3/t2)
3.  Pre-requisitos: Tipo de Instancia EC2 Recomendada
4.  Passo 1 — Conectar via RDP e Preparar o Ambiente
5.  Passo 2 — Instalar o .NET 8 SDK
6.  Passo 3 — Instalar o PostgreSQL 16
7.  Passo 4 — Instalar Erlang + RabbitMQ
8.  Passo 5 — Configurar Firewall do Windows e Security Groups AWS
9.  Passo 6 — Transferir o Projeto para a Instancia
10. Passo 7 — Configuracao (appsettings.json)
11. Passo 8 — Build e Publish da API
12. Passo 9 — Instalar como Windows Service (Producao)
13. Passo 10 — Verificar Todos os Servicos
14. Resolucao de Problemas Comuns
15. Referencia Rapida: Portas e Variaveis

---

## 1. Visao Geral da Arquitetura

O OrderManagementApi e um monolito modular em ASP.NET Core 8 composto por tres modulos de negocio independentes (Catalog, Basket e Orders), todos hospedados em um unico processo. Os modulos se comunicam internamente via MediatR (CQRS) e externamente via MassTransit sobre RabbitMQ (eventos de integracao como `BasketCheckoutIntegrationEvent` e `ProductPriceChangedIntegrationEvent`). O banco de dados e PostgreSQL 16 com Entity Framework Core / Npgsql.

```
Cliente HTTP / Swagger UI
        |
        | HTTP :5000
        v
API — ASP.NET Core 8
Carter . Swagger . FluentValidation
        |
        | MediatR (in-process)
        v
+-----------------+-----------------+-----------------+
| Catalog Module  | Basket Module   | Orders Module   |
+-----------------+-----------------+-----------------+
        |                                   |
        | EF Core / Npgsql                  | MassTransit / RabbitMQ
        v                                   v
PostgreSQL 16                         RabbitMQ 3.x
(porta 5432)                          (5672 / 15672)
```

Caracteristicas relevantes para o deploy:

- **Auto-migration**: o `Program.cs` executa `UseMigrationAsync<T>()` para os tres DbContexts (CatalogDbContext, BasketDbContext, OrdersDbContext) no startup. As migrations sao aplicadas automaticamente — nao e necessario rodar `dotnet ef database update` manualmente.
- **Windows Service**: o projeto ja inclui o pacote `Microsoft.Extensions.Hosting.WindowsServices` (8.0.0) e a chamada `builder.Host.UseWindowsService()` no `Program.cs`. Isso permite que o executavel responda corretamente aos sinais de start/stop do Service Control Manager (SCM) do Windows.
- **Swagger via configuracao**: o Swagger e habilitado em Development automaticamente OU quando a chave `"EnableSwagger": true` esta presente no appsettings.json. No ambiente Windows nativo em Production, o Swagger funcionara sem precisar mudar o environment.
- **i18n**: a API suporta 43 culturas via header `Accept-Language`. Nao requer configuracao adicional para o deploy.

No Linux + Docker, tudo funciona via `docker-compose.yml` (as variaveis de ambiente do compose sobrescrevem o appsettings.json). No Windows EC2, instalaremos cada servico nativamente — PostgreSQL e RabbitMQ como servicos Windows, e a API como Windows Service.

---

## 2. Por que Nao Usar Docker no Windows EC2 (t3/t2)

| Problema | Causa Tecnica |
|---|---|
| Docker no Windows usa Hyper-V | O Docker Desktop / Docker Engine no Windows requer Hyper-V ou WSL2 para criar VMs Linux internas. |
| EC2 t3.small = Nitro Hypervisor | A AWS roda instancias t3 sobre o Nitro Hypervisor. Windows guest dentro do Nitro nao suporta nested virtualization (Hyper-V dentro de Hyper-V). |
| WSL2 tambem falha | WSL2 igualmente depende de virtualizacao aninhada para rodar o kernel Linux. O erro tipico e: `WSL 2 requires an update to its kernel component`. |

**Solucao 1 (recomendada):** Instalar os servicos nativamente no Windows, sem Docker — exatamente o que este guia ensina.

**Solucao 2 (alternativa):** Usar uma instancia metal (ex: c5.metal) ou mudar para Amazon Linux 2023 onde Docker funciona normalmente.

> Instancias t3, t2, m5, c5 (nao-metal) com Windows Server NAO suportam Hyper-V nested. Nao tente habilitar o recurso Hyper-V manualmente — a instancia pode se tornar inacessivel.

---

## 3. Pre-requisitos: Tipo de Instancia EC2 Recomendada

| Instancia | vCPU / RAM | Uso Ideal | Obs |
|---|---|---|---|
| t3.medium | 2 / 4 GB | Dev/Entrevista | Suficiente para tudo |
| t3.large | 2 / 8 GB | Staging/Demo | Confortavel |
| m5.large | 2 / 8 GB | Producao leve | Melhor CPU burst |
| t3.small | 2 / 2 GB | Minimo absoluto | Pode ter OOM com EF |
| t3.micro | 2 / 1 GB | Nao recomendado | RAM insuficiente |

### AMI Recomendada

Use **Windows Server 2022 Base** (AMI ID varia por regiao). Ao lancar a instancia, certifique-se de:

- Abrir a porta **3389/TCP** para RDP no Security Group
- Abrir a porta **5000/TCP** (API) e **15672/TCP** (RabbitMQ UI) para seu IP
- Criar um Key Pair ou anotar a senha do Administrator
- Alocar ao menos **30 GB** de armazenamento EBS (gp3)

---

## Passo 1 — Conectar via RDP e Preparar o Ambiente

### 1.1 — Obter a senha do Administrator

No console da AWS, va em **EC2 -> Instances** -> selecione sua instancia -> **Connect -> RDP Client**. Clique em **Get Password**, cole o conteudo do seu arquivo `.pem` e clique em **Decrypt Password**. Anote a senha gerada.

### 1.2 — Conectar via Microsoft Remote Desktop

Abra o Microsoft Remote Desktop (Windows) ou o Remote Desktop Connection e conecte ao IP publico da instancia:

```
Computer:  <IP_PUBLICO_EC2>
Username:  Administrator
Password:  <senha obtida no passo acima>
```

### 1.3 — Abrir o PowerShell como Administrador

Apos conectar, clique com o botao direito no menu Iniciar -> **Windows PowerShell (Admin)**. Todos os comandos deste guia sao executados neste terminal.

### 1.4 — Verificar conectividade basica

```powershell
nslookup google.com
[System.Environment]::OSVersion.VersionString
```

---

## Passo 2 — Instalar o .NET 8 SDK

### 2.1 — Download via PowerShell (metodo recomendado)

```powershell
$url = 'https://dot.net/v1/dotnet-install.ps1'
$script = Join-Path $env:TEMP 'dotnet-install.ps1'
Invoke-WebRequest -Uri $url -OutFile $script

# Instalar .NET 8 SDK (inclui runtime)
& $script -Channel 8.0
```

Alternativa via instalador grafico: acesse https://dotnet.microsoft.com/download/dotnet/8.0 pelo Edge dentro da instancia, baixe o **SDK 8.0.x — Windows x64 Installer** e execute.

### 2.2 — Verificar a instalacao

```powershell
# Fechar e reabrir o PowerShell, entao executar:
dotnet --version
# Deve exibir: 8.0.xxx

dotnet --list-sdks
# Deve listar: 8.0.xxx [C:\Program Files\dotnet\sdk]

dotnet --list-runtimes
# Deve mostrar Microsoft.AspNetCore.App 8.0.x
```

---

## Passo 3 — Instalar o PostgreSQL 16

### 3.1 — Download do instalador EDB

```powershell
New-Item -ItemType Directory -Force -Path C:\Installers

$pg_url = 'https://get.enterprisedb.com/postgresql/postgresql-16.6-1-windows-x64.exe'
$pg_file = 'C:\Installers\postgresql-16-setup.exe'
Invoke-WebRequest -Uri $pg_url -OutFile $pg_file
```

### 3.2 — Instalacao silenciosa

```powershell
Start-Process -FilePath 'C:\Installers\postgresql-16-setup.exe' -Wait -ArgumentList '--mode', 'unattended', '--superpassword', 'postgres', '--serverport', '5432', '--prefix', 'C:\PostgreSQL\16', '--datadir', 'C:\PostgreSQL\16\data'

Write-Host 'PostgreSQL instalado com sucesso!'
```

### 3.3 — Verificar e configurar o servico

```powershell
Get-Service -Name 'postgresql*'
# Status deve ser: Running

# Adicionar psql ao PATH
$pg_bin = 'C:\PostgreSQL\16\bin'
[Environment]::SetEnvironmentVariable('Path', $env:Path + ';' + $pg_bin, 'Machine')

# Reabrir o PowerShell e testar conexao
psql -U postgres -c "SELECT version();"
# Senha: postgres
```

### 3.4 — Criar o banco de dados da aplicacao

```powershell
psql -U postgres -c "CREATE DATABASE ""OrderManagementDb"";"
psql -U postgres -c "\l"
# Deve listar: OrderManagementDb
```

> O PostgreSQL 16 esta instalado e rodando como Windows Service. Senha do superusuario: `postgres`. As tabelas serao criadas automaticamente pelo auto-migration da API no primeiro startup — nao e necessario rodar `dotnet ef database update`.

---

## Passo 4 — Instalar Erlang + RabbitMQ

> O RabbitMQ EXIGE que o Erlang/OTP esteja instalado ANTES. Instale sempre nesta ordem: Erlang primeiro, depois RabbitMQ.

### 4.1 — Instalar Erlang/OTP 26

```powershell
$erlang_url = 'https://github.com/erlang/otp/releases/download/OTP-26.2.5/otp_win64_26.2.5.exe'
$erlang_file = 'C:\Installers\erlang-setup.exe'
Invoke-WebRequest -Uri $erlang_url -OutFile $erlang_file

Start-Process -FilePath $erlang_file -Wait -ArgumentList '/S'
```

### 4.2 — Instalar RabbitMQ 3.13

```powershell
$rmq_url = 'https://github.com/rabbitmq/rabbitmq-server/releases/download/v3.13.7/rabbitmq-server-3.13.7.exe'
$rmq_file = 'C:\Installers\rabbitmq-setup.exe'
Invoke-WebRequest -Uri $rmq_url -OutFile $rmq_file

Start-Process -FilePath $rmq_file -Wait -ArgumentList '/S'

# Adicionar RabbitMQ sbin ao PATH
$rmq_bin = 'C:\Program Files\RabbitMQ Server\rabbitmq_server-3.13.7\sbin'
[Environment]::SetEnvironmentVariable('Path', $env:Path + ';' + $rmq_bin, 'Machine')

# Reabrir PowerShell e habilitar o plugin de gerenciamento web
rabbitmq-plugins enable rabbitmq_management
```

### 4.3 — Verificar o servico RabbitMQ

```powershell
Get-Service -Name RabbitMQ
# Status: Running

rabbitmqctl status
rabbitmqctl list_users
# Deve ter 'guest'
```

> Erlang + RabbitMQ instalados. Plugin de management habilitado na porta 15672. Usuario padrao: `guest` / `guest`. O codigo em `MassTransitExtensions.cs` le `RabbitMQ:Host` (para o host) e `MessageBroker:UserName` / `MessageBroker:Password` (para credenciais). Os valores padrao no appsettings.json ja apontam para `localhost` com `guest/guest`.

---

## Passo 5 — Configurar Firewall do Windows e Security Groups AWS

### 5.1 — Abrir portas no Windows Defender Firewall

```powershell
New-NetFirewallRule -DisplayName 'OrderManagementApi' -Direction Inbound -Protocol TCP -LocalPort 5000 -Action Allow

New-NetFirewallRule -DisplayName 'RabbitMQ Management' -Direction Inbound -Protocol TCP -LocalPort 15672 -Action Allow

New-NetFirewallRule -DisplayName 'RabbitMQ AMQP' -Direction Inbound -Protocol TCP -LocalPort 5672 -Action Allow

Get-NetFirewallRule | Where-Object DisplayName -like 'Order*'
```

### 5.2 — Configurar Security Group na AWS Console

No console AWS: **EC2 -> Security Groups** -> selecione o grupo da sua instancia -> **Inbound Rules -> Edit**.

| Tipo | Protocolo | Porta | Origem | Finalidade |
|---|---|---|---|---|
| RDP | TCP | 3389 | Seu IP | Acesso RDP |
| Custom TCP | TCP | 5000 | 0.0.0.0/0 ou Seu IP | API HTTP |
| Custom TCP | TCP | 15672 | Seu IP | RabbitMQ UI |
| Custom TCP | TCP | 5672 | Bloqueado | AMQP (interno) |
| Custom TCP | TCP | 5432 | Bloqueado | PostgreSQL (interno) |

> NUNCA abra a porta 5432 (PostgreSQL) publicamente. O banco deve ser acessivel apenas pela propria instancia (localhost).

---

## Passo 6 — Transferir o Projeto para a Instancia

### Opcao A — Git clone (recomendado)

```powershell
winget install Git.Git --silent

# Reabrir o PowerShell
cd C:\
git clone https://github.com/gabrielandre-math/OrderManagement.git OrderManagementApi

# Resultado: C:\OrderManagementApi\
```

### Opcao B — Upload via RDP (Clipboard / File Transfer)

No cliente RDP (Local Resources -> More), habilite **Drives** para montar seu disco local. Dentro do RDP, abra o Explorer -> This PC -> drives mapeados. Copie a pasta para `C:\OrderManagementApi`.

### Opcao C — AWS S3

```powershell
# Na sua maquina local
aws s3 cp OrderManagementApi.tar s3://meu-bucket/

# Na instancia EC2
winget install Amazon.AWSCLI --silent
aws s3 cp s3://meu-bucket/OrderManagementApi.tar C:\
tar -xf C:\OrderManagementApi.tar -C C:\
```

> Ao final, a estrutura deve estar em `C:\OrderManagementApi\` com as subpastas `src\` e o arquivo `OrderManagementApi.slnx`.

---

## Passo 7 — Configuracao (appsettings.json)

O appsettings.json base ja vem configurado corretamente para o ambiente Windows nativo. Nao e necessario edita-lo se o PostgreSQL e o RabbitMQ estao no localhost com as credenciais padrao.

Conteudo atual do `src\Bootstrapper\Api\appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultSchema": "Host=localhost;Port=5432;Database=OrderManagementDb;Username=postgres;Password=postgres;Include Error Detail=true"
  },
  "RabbitMQ": {
    "Host": "localhost",
    "Port": 5672
  },
  "MessageBroker": {
    "UserName": "guest",
    "Password": "guest"
  },
  "EnableSwagger": true
}
```

Explicacao das chaves:

| Chave | Valor padrao | Lida por | Finalidade |
|---|---|---|---|
| `ConnectionStrings:DefaultSchema` | `Host=localhost;...` | EF Core / Npgsql (via `AddCatalogModule`, `AddBasketModule`, `AddOrdersModule`) | Connection string do PostgreSQL |
| `RabbitMQ:Host` | `localhost` | `MassTransitExtensions.cs` -> `configurator.Host(...)` | Hostname do broker RabbitMQ |
| `RabbitMQ:Port` | `5672` | Informativo (MassTransit usa a porta padrao 5672) | Porta AMQP |
| `MessageBroker:UserName` | `guest` | `MassTransitExtensions.cs` -> `host.Username(...)` | Credencial RabbitMQ |
| `MessageBroker:Password` | `guest` | `MassTransitExtensions.cs` -> `host.Password(...)` | Credencial RabbitMQ |
| `EnableSwagger` | `true` | `Program.cs` -> `builder.Configuration.GetValue<bool>("EnableSwagger")` | Habilita Swagger em qualquer environment |

> Se precisar de credenciais diferentes (ex: producao com senha forte), edite este arquivo ou crie um `appsettings.Production.json` que sobrescreva apenas as chaves necessarias. Tambem pode usar variaveis de ambiente com `__` como separador (ex: `ConnectionStrings__DefaultSchema`).

### Alternativa — Variaveis de Ambiente no PowerShell

```powershell
$env:ConnectionStrings__DefaultSchema = 'Host=localhost;Port=5432;Database=OrderManagementDb;Username=postgres;Password=postgres;Include Error Detail=true'
$env:RabbitMQ__Host = 'localhost'
$env:MessageBroker__UserName = 'guest'
$env:MessageBroker__Password = 'guest'
$env:ASPNETCORE_ENVIRONMENT = 'Production'
$env:ASPNETCORE_URLS = 'http://0.0.0.0:5000'
```

> O ASP.NET Core usa `__` (duplo underscore) como separador de hierarquia em variaveis de ambiente — equivalente ao `:` no appsettings.json.

---

## Passo 8 — Build e Publish da API

### 8.1 — Restaurar pacotes NuGet

```powershell
cd C:\OrderManagementApi
dotnet restore OrderManagementApi.slnx
```

### 8.2 — Build (verificacao)

```powershell
dotnet build OrderManagementApi.slnx -c Release
# Output esperado: 'Build succeeded.'
```

### 8.3 — Publish (geracao dos artefatos de producao)

```powershell
dotnet publish src/Bootstrapper/Api/Api.csproj -c Release -o C:\ApiPublish --no-restore
```

```powershell
ls C:\ApiPublish
# Deve conter: Api.exe, Api.dll, appsettings.json, etc.
```

> Os artefatos de producao estao em `C:\ApiPublish\` prontos para execucao. O `Api.exe` ja inclui o suporte a Windows Service via `Microsoft.Extensions.Hosting.WindowsServices` (8.0.0) — o pacote esta declarado no `Api.csproj` e o `Program.cs` chama `builder.Host.UseWindowsService()`.

---

## Passo 9 — Instalar como Windows Service (Producao)

O projeto ja esta preparado para rodar como Windows Service. O `Program.cs` contem `builder.Host.UseWindowsService()` que faz o executavel responder corretamente aos sinais de start/stop/pause do SCM (Service Control Manager) do Windows. Em Linux, essa chamada e ignorada (no-op).

### Metodo A — Usando sc.exe (nativo do Windows)

```powershell
sc.exe create OrderManagementApi binPath= "C:\ApiPublish\Api.exe --urls http://0.0.0.0:5000" start= auto DisplayName= "Order Management API"
```

```powershell
# Definir acao de recuperacao em falha (reiniciar automaticamente)
sc.exe failure OrderManagementApi reset= 60 actions= restart/5000/restart/10000/restart/30000
```

```powershell
sc.exe start OrderManagementApi
```

```powershell
sc.exe query OrderManagementApi
# STATE deve ser: 4  RUNNING
```

> Na primeira execucao, a API aplicara automaticamente todas as migrations pendentes nos tres DbContexts (CatalogDbContext, BasketDbContext, OrdersDbContext) via `UseMigrationAsync<T>()`. Nao e necessario rodar `dotnet ef database update` manualmente. As tabelas serao criadas automaticamente no banco `OrderManagementDb`.

### Metodo B — Usando NSSM (Non-Sucking Service Manager)

```powershell
winget install NSSM.NSSM

nssm install OrderManagementApi C:\ApiPublish\Api.exe
nssm set OrderManagementApi AppParameters "--urls http://0.0.0.0:5000"
nssm set OrderManagementApi AppEnvironmentExtra "ASPNETCORE_ENVIRONMENT=Production"
nssm set OrderManagementApi Start SERVICE_AUTO_START
nssm set OrderManagementApi ObjectName LocalSystem ""

nssm start OrderManagementApi
```

### Teste rapido (sem instalar como servico)

```powershell
cd C:\ApiPublish
.\Api.exe --urls "http://0.0.0.0:5000"
```

---

## Passo 10 — Verificar Todos os Servicos

### 10.1 — Verificar servicos Windows

```powershell
Get-Service postgresql* | Select Name, Status
# postgresql-x64-16: Running

Get-Service RabbitMQ | Select Name, Status
# RabbitMQ: Running

Get-Service OrderManagementApi | Select Name, Status
# OrderManagementApi: Running

netstat -an | findstr "5000 5432 5672 15672"
```

### 10.2 — Testar a API via PowerShell

```powershell
# Testar o Swagger UI (deve retornar 200)
Invoke-WebRequest -Uri 'http://localhost:5000/swagger' -UseBasicParsing

# Testar endpoint de produtos do Catalog
Invoke-WebRequest -Uri 'http://localhost:5000/products' -UseBasicParsing | Select Content

# Testar i18n (resposta em portugues)
Invoke-WebRequest -Uri 'http://localhost:5000/products' -UseBasicParsing -Headers @{'Accept-Language'='pt-BR'}
```

### 10.3 — Acessar do seu computador local

Substitua `IP_PUBLICO_EC2` pelo IP publico da sua instancia:

| Servico | URL de Acesso |
|---|---|
| Swagger UI (API Docs) | `http://IP_PUBLICO_EC2:5000/swagger` |
| API — Listar Produtos | `http://IP_PUBLICO_EC2:5000/products` |
| RabbitMQ Management | `http://IP_PUBLICO_EC2:15672` |
| RabbitMQ Login | `guest` / `guest` |

> Se o Swagger UI carregar com os endpoints do Catalog, Basket e Orders visiveis, e o RabbitMQ Management mostrar conexoes ativas, o deploy foi concluido com sucesso. O Swagger funciona em qualquer environment porque `EnableSwagger: true` esta no appsettings.json.

---

## 14. Resolucao de Problemas Comuns

### `dotnet` nao e reconhecido como comando

Feche e reabra o PowerShell apos instalar o .NET 8. Verifique se o PATH inclui `C:\Program Files\dotnet\`:

```powershell
[System.Environment]::GetEnvironmentVariable('Path','Machine')
```

### connection to server on socket failed (PostgreSQL)

```powershell
Get-Service postgresql*
# Se parado:
Start-Service postgresql-x64-16

psql -U postgres -h localhost
# Senha: postgres
```

Verifique `pg_hba.conf` em `C:\PostgreSQL\16\data\pg_hba.conf` — deve conter: `host all all 127.0.0.1/32 md5`

### RabbitMQ.Client.Exceptions.BrokerUnreachableException

```powershell
Get-Service RabbitMQ
# Se parado:
Start-Service RabbitMQ

Test-NetConnection -ComputerName localhost -Port 5672
# TcpTestSucceeded deve ser True
```

Verifique os logs: `C:\Users\%USERNAME%\AppData\Roaming\RabbitMQ\log\`

### `relation does not exist` ou tabelas nao encontradas

As migrations sao aplicadas automaticamente pelo `UseMigrationAsync<T>()` no startup da API. Se o erro ocorrer, significa que a API nao conseguiu iniciar corretamente. Verifique:

1. O banco `OrderManagementDb` existe: `psql -U postgres -c "\l"`
2. A connection string no appsettings.json esta correta
3. Os logs do Event Viewer: `eventvwr` -> Windows Logs -> Application

Se precisar aplicar manualmente (situacao atipica):

```powershell
cd C:\OrderManagementApi
dotnet ef database update --project src/Modules/Catalog/Catalog/Catalog.csproj --startup-project src/Bootstrapper/Api/Api.csproj --context CatalogDbContext
```

### Swagger nao carrega (404 em /swagger)

O Swagger e controlado pela chave `EnableSwagger` no appsettings.json, NAO pelo `ASPNETCORE_ENVIRONMENT`. Verifique:

```powershell
# No arquivo C:\ApiPublish\appsettings.json deve conter:
# "EnableSwagger": true

# Se estiver faltando, adicione a chave e reinicie:
Restart-Service OrderManagementApi
```

Com `"EnableSwagger": true`, o Swagger funciona independentemente do environment (Development, Production, etc.).

### Windows Service falha ao iniciar (The service did not respond)

```powershell
# Teste primeiro executando manualmente:
cd C:\ApiPublish
.\Api.exe --urls "http://0.0.0.0:5000"
```

Observe a saida do console por mensagens de erro. Causas comuns:
- PostgreSQL ou RabbitMQ nao estao rodando
- Connection string incorreta
- Porta 5000 ja em uso

Verifique logs do Event Viewer: `eventvwr` -> Windows Logs -> Application. O projeto usa `UseWindowsService()` que registra logs no Event Log automaticamente.

### Erro de globalizacao / ICU

O projeto suporta 43 culturas e depende das ICU libraries. No Windows Server 2022, as ICU libraries ja estao incluidas no SO. Se por algum motivo ocorrer erro:

```powershell
winget install ICU.icu
```

---

## 15. Referencia Rapida: Portas e Variaveis

### Portas dos Servicos

| Servico | Porta | Protocolo | Expor externamente? |
|---|---|---|---|
| API (HTTP) | 5000 | TCP/HTTP | Sim |
| Swagger UI | 5000/swagger | HTTP | Sim |
| PostgreSQL | 5432 | TCP | Nao (local) |
| RabbitMQ AMQP | 5672 | TCP | Nao (local) |
| RabbitMQ Mgmt UI | 15672 | HTTP | So seu IP |

### Variaveis de Ambiente da API

| Variavel | Valor (Windows Nativo) |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ASPNETCORE_URLS` | `http://0.0.0.0:5000` |
| `ConnectionStrings__DefaultSchema` | `Host=localhost;Port=5432;Database=OrderManagementDb;Username=postgres;Password=postgres` |
| `RabbitMQ__Host` | `localhost` |
| `MessageBroker__UserName` | `guest` |
| `MessageBroker__Password` | `guest` |
| `EnableSwagger` | `true` (ja no appsettings.json — nao precisa de env var) |

### Comandos Essenciais de Gerenciamento

```powershell
# Status de todos os servicos
Get-Service postgresql*, RabbitMQ, OrderManagementApi

# Reiniciar tudo
Restart-Service postgresql-x64-16
Restart-Service RabbitMQ
Restart-Service OrderManagementApi

# Parar a API para atualizacao
Stop-Service OrderManagementApi
# ... atualizar arquivos em C:\ApiPublish\
Start-Service OrderManagementApi

# Ver logs da API no Event Viewer
Get-EventLog -LogName Application -Source 'OrderManagementApi' -Newest 20

# Ver logs do RabbitMQ
Get-Content "$env:APPDATA\RabbitMQ\log\rabbit@$env:COMPUTERNAME.log" -Tail 50

# Remover o servico (se necessario recriar)
sc.exe delete OrderManagementApi
```

---

### Arquivos-chave do projeto relevantes para o deploy

| Arquivo | Finalidade |
|---|---|
| `src/Bootstrapper/Api/Program.cs` | Entry point. Contem `UseWindowsService()`, Swagger via config, auto-migration dos 3 DbContexts |
| `src/Bootstrapper/Api/Api.csproj` | Declara `Microsoft.Extensions.Hosting.WindowsServices` 8.0.0 |
| `src/Bootstrapper/Api/appsettings.json` | Config base: ConnectionString, RabbitMQ, MessageBroker, EnableSwagger |
| `src/Shared/Shared.Messaging/Extensions/MassTransitExtensions.cs` | Le `RabbitMQ:Host`, `MessageBroker:UserName`, `MessageBroker:Password` |
| `src/Shared/Shared/Extensions/DatabaseExtensions.cs` | `UseMigrationAsync<T>()` — aplica migrations + seed no startup |
| `docker-compose.yml` | Deploy Linux (Docker) — NAO usado no Windows nativo |

---

> Este guia foi gerado com base no codigo-fonte do repositorio https://github.com/gabrielandre-math/OrderManagement (branch master). ASP.NET Core 8, .NET 8, PostgreSQL 16, RabbitMQ 3.x, MassTransit, MediatR, Carter. Deploy em instancia AWS EC2 Windows Server 2022 sem Docker.
