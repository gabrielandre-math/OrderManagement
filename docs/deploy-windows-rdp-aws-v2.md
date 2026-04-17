# OrderManagementApi — Guia Tecnico de Deploy (Versao Corrigida)

## Windows Server via RDP (AWS EC2, Sem Docker)

> Versao Corrigida — comandos validados manualmente

| Componente | Tecnologia | Versao |
|---|---|---|
| API Principal | ASP.NET Core (Monolito Modular) | .NET 8 |
| Banco de Dados | PostgreSQL | 16.x |
| Message Broker | RabbitMQ | 3.x |
| Modulos | Catalog, Basket, Orders | — |
| Padroes | CQRS, MediatR, Carter, MassTransit, FluentValidation | — |
| Repositorio | https://github.com/gabrielandre-math/OrderManagement | branch master |

> Este documento e a versao corrigida do guia original, incorporando os ajustes identificados durante o deploy real: fix de TLS para download do Erlang, correcao do cookie do RabbitMQ, build via Api.csproj (contornando o .slnx) e resolucao de conflito de pacotes Microsoft.CodeAnalysis.

---

## Sumario

1. [Visao Geral da Arquitetura](#1-visao-geral-da-arquitetura)
2. [Por que Nao Usar Docker no Windows EC2 (t3/t2)](#2-por-que-nao-usar-docker-no-windows-ec2-t3t2)
3. [Pre-requisitos: Tipo de Instancia EC2 Recomendada](#3-pre-requisitos-tipo-de-instancia-ec2-recomendada)
4. [Passo 1 — Conectar via RDP e Preparar o Ambiente](#4-passo-1--conectar-via-rdp-e-preparar-o-ambiente)
5. [Passo 2 — Instalar o .NET 8 SDK](#5-passo-2--instalar-o-net-8-sdk)
6. [Passo 3 — Instalar o PostgreSQL 16](#6-passo-3--instalar-o-postgresql-16) *(Corrigido)*
7. [Passo 4 — Instalar Erlang + RabbitMQ](#7-passo-4--instalar-erlang--rabbitmq) *(Corrigido)*
8. [Passo 5 — Configurar Firewall e Security Groups AWS](#8-passo-5--configurar-firewall-do-windows-e-security-groups-aws)
9. [Passo 6 — Transferir o Projeto para a Instancia](#9-passo-6--transferir-o-projeto-para-a-instancia)
10. [Passo 7 — Configuracao (appsettings.json)](#10-passo-7--configuracao-appsettingsjson)
11. [Passo 8 — Build e Publish da API](#11-passo-8--build-e-publish-da-api) *(Corrigido)*
12. [Passo 9 — Instalar como Windows Service](#12-passo-9--instalar-como-windows-service-producao)
13. [Passo 10 — Verificar Todos os Servicos](#13-passo-10--verificar-todos-os-servicos)
14. [Resolucao de Problemas Comuns](#14-resolucao-de-problemas-comuns)
15. [Referencia Rapida: Portas e Variaveis](#15-referencia-rapida-portas-e-variaveis)

---

## 1. Visao Geral da Arquitetura

O OrderManagementApi e um monolito modular em ASP.NET Core 8 composto por tres modulos de negocio independentes — Catalog, Basket e Orders —, todos hospedados em um unico processo. Os modulos se comunicam internamente via MediatR (padrao CQRS) e externamente via MassTransit sobre RabbitMQ. O banco de dados e o PostgreSQL 16 com Entity Framework Core e driver Npgsql.

### Diagrama de Arquitetura

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

- **Auto-migration**: o `Program.cs` executa `UseMigrationAsync<T>()` para os tres DbContexts na inicializacao. As migracoes sao aplicadas automaticamente.
- **Windows Service**: o projeto inclui `Microsoft.Extensions.Hosting.WindowsServices` (8.0.0) e `builder.Host.UseWindowsService()` no `Program.cs`.
- **Swagger via configuracao**: habilitado automaticamente quando `"EnableSwagger": true` esta no `appsettings.json`. Funciona em qualquer environment (Development, Production, etc.).
- **Internacionalizacao (i18n)**: a API suporta 43 culturas via header `Accept-Language`. Nenhuma configuracao adicional e necessaria.

---

## 2. Por que Nao Usar Docker no Windows EC2 (t3/t2)

| Problema | Causa Tecnica |
|---|---|
| Docker no Windows utiliza Hyper-V | Docker Desktop e Docker Engine no Windows requerem Hyper-V ou WSL2 para criar VMs Linux internas. |
| EC2 t3.small = Nitro Hypervisor | A AWS executa instancias t3 sobre o Nitro Hypervisor. O guest Windows nao suporta virtualizacao aninhada. |
| WSL2 tambem falha | WSL2 igualmente depende de virtualizacao aninhada. Erro tipico: `WSL 2 requires an update to its kernel component`. |

**Solucao 1 (recomendada):** instalar os servicos nativamente no Windows, sem Docker — conforme descrito neste guia.

**Solucao 2 (alternativa):** utilizar uma instancia bare-metal (ex.: `c5.metal`) ou migrar para Amazon Linux 2023.

---

## 3. Pre-requisitos: Tipo de Instancia EC2 Recomendada

| Instancia | vCPU / RAM | Uso Ideal | Observacao |
|---|---|---|---|
| t3.medium | 2 / 4 GB | Dev / Entrevista | Suficiente para todos os servicos |
| t3.large | 2 / 8 GB | Staging / Demo | Configuracao confortavel |
| m5.large | 2 / 8 GB | Producao leve | Melhor desempenho de CPU burst |
| t3.small | 2 / 2 GB | Minimo absoluto | Risco de OOM com EF Core |
| t3.micro | 2 / 1 GB | Nao recomendado | RAM insuficiente para a stack |

### AMI Recomendada

Utilize a imagem **Windows Server 2022 Base**. Ao provisionar a instancia, certifique-se de:

- Abrir a porta **3389/TCP** (RDP) no Security Group para o IP de origem
- Abrir as portas **5000/TCP** (API) e **15672/TCP** (RabbitMQ Management UI) para o IP de acesso
- Criar ou associar um Key Pair para recuperacao de senha
- Alocar no minimo **30 GB** de armazenamento EBS (tipo gp3)

---

## 4. Passo 1 — Conectar via RDP e Preparar o Ambiente

### 1.1 — Obter a Senha do Administrator

No console da AWS, acesse **EC2 -> Instances** -> selecione a instancia -> **Connect -> RDP Client**. Clique em **Get Password**, cole o conteudo do arquivo `.pem` e clique em **Decrypt Password**. Registre a senha gerada.

### 1.2 — Conectar via Microsoft Remote Desktop

```
Computer:  <IP_PUBLICO_EC2>
Username:  Administrator
Password:  <senha obtida no passo 1.1>
```

### 1.3 — Abrir o PowerShell como Administrador

Clique com o botao direito no menu Iniciar -> **Windows PowerShell (Admin)**. Todos os comandos deste guia devem ser executados neste terminal.

### 1.4 — Verificar a Conectividade Basica

```powershell
nslookup google.com
[System.Environment]::OSVersion.VersionString
```

---

## 5. Passo 2 — Instalar o .NET 8 SDK

### 2.1 — Download via PowerShell (metodo recomendado)

```powershell
$url    = 'https://dot.net/v1/dotnet-install.ps1'
$script = Join-Path $env:TEMP 'dotnet-install.ps1'
Invoke-WebRequest -Uri $url -OutFile $script

# Instalar o .NET 8 SDK (inclui o runtime)
& $script -Channel 8.0
```

Alternativa (instalador grafico): acesse https://dotnet.microsoft.com/download/dotnet/8.0 pelo Edge dentro da instancia, baixe o **SDK 8.0.x — Windows x64 Installer** e execute-o.

### 2.2 — Verificar a Instalacao

Feche e reabra o PowerShell antes de executar os comandos abaixo:

```powershell
dotnet --version
# Saida esperada: 8.0.xxx

dotnet --list-sdks
# Saida esperada: 8.0.xxx [C:\Program Files\dotnet\sdk]

dotnet --list-runtimes
# Deve listar: Microsoft.AspNetCore.App 8.0.x
```

---

## 6. Passo 3 — Instalar o PostgreSQL 16

> **CORRIGIDO** — URL do download em linha unica e argumentos com aspas duplas para PowerShell.

### 3.1 — Download do Instalador EDB

```powershell
New-Item -ItemType Directory -Force -Path C:\Installers

$pg_url  = 'https://get.enterprisedb.com/postgresql/postgresql-16.6-1-windows-x64.exe'
$pg_file = 'C:\Installers\postgresql-16-setup.exe'

Invoke-WebRequest -Uri $pg_url -OutFile $pg_file
```

### 3.2 — Instalacao Silenciosa

```powershell
Start-Process `
    -FilePath $pg_file `
    -Wait `
    -ArgumentList "--mode", "unattended", `
                   "--superpassword", "postgres", `
                   "--serverport", "5432", `
                   "--prefix", "C:\PostgreSQL\16", `
                   "--datadir", "C:\PostgreSQL\16\data"
```

### 3.3 — Verificar e Configurar o Servico

```powershell
Get-Service -Name 'postgresql*'
# Status esperado: Running

# Adicionar o psql ao PATH do sistema
$pg_bin = 'C:\PostgreSQL\16\bin'
[Environment]::SetEnvironmentVariable('Path', $env:Path + ';' + $pg_bin, 'Machine')
```

Reabra o PowerShell e teste a conexao:

```powershell
psql -U postgres -c "SELECT version();"
# Senha: postgres
```

### 3.4 — Criar o Banco de Dados da Aplicacao

```powershell
psql -U postgres -c "CREATE DATABASE \"OrderManagementDb\";"
psql -U postgres -c "\l"
# A lista deve conter: OrderManagementDb
```

> O PostgreSQL 16 esta instalado como Windows Service. A senha do superusuario e `postgres`. As tabelas serao criadas automaticamente pelo auto-migration na primeira inicializacao da API.

---

## 7. Passo 4 — Instalar Erlang + RabbitMQ

> **CORRIGIDO** — TLS 1.2 forcado para downloads do GitHub + sincronizacao do cookie do Erlang.

> O RabbitMQ exige que o Erlang/OTP esteja instalado **antes** de sua propria instalacao. Respeite esta ordem: Erlang primeiro, RabbitMQ em seguida.

### 4.1 — Instalar Erlang/OTP 26

O GitHub bloqueia downloads sem TLS 1.2. Forcar o protocolo antes do `Invoke-WebRequest` resolve o erro de conexao que ocorria na versao original do guia.

```powershell
# Forca o uso do TLS 1.2 (padrao exigido pelo GitHub)
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$erlang_url  = 'https://github.com/erlang/otp/releases/download/OTP-26.2.5/otp_win64_26.2.5.exe'
$erlang_file = 'C:\Installers\erlang-setup.exe'

Invoke-WebRequest -Uri $erlang_url -OutFile $erlang_file
Start-Process -FilePath $erlang_file -Wait -ArgumentList '/S'
```

### 4.2 — Instalar RabbitMQ 3.13

```powershell
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$rmq_url  = 'https://github.com/rabbitmq/rabbitmq-server/releases/download/v3.13.7/rabbitmq-server-3.13.7.exe'
$rmq_file = 'C:\Installers\rabbitmq-setup.exe'

Invoke-WebRequest -Uri $rmq_url -OutFile $rmq_file
Start-Process -FilePath $rmq_file -Wait -ArgumentList '/S'

# Adicionar o diretorio sbin ao PATH do sistema
$rmq_bin = 'C:\Program Files\RabbitMQ Server\rabbitmq_server-3.13.7\sbin'
[Environment]::SetEnvironmentVariable('Path', $env:Path + ';' + $rmq_bin, 'Machine')
```

Reabra o PowerShell e habilite o plugin de gerenciamento web:

```powershell
rabbitmq-plugins enable rabbitmq_management
```

### 4.3 — Corrigir o Cookie do Erlang (fix obrigatorio)

Apos a instalacao, e necessario sincronizar o arquivo `.erlang.cookie` entre o perfil do SYSTEM e o perfil do Administrator. Sem isso, o `rabbitmqctl` falha com erro de autenticacao.

```powershell
# Copia o cookie do sistema para a pasta do Administrator
Copy-Item `
    -Path "C:\Windows\System32\config\systemprofile\.erlang.cookie" `
    -Destination "C:\Users\Administrator\.erlang.cookie" `
    -Force

# Reinicia o servico para garantir a sincronizacao
Restart-Service RabbitMQ
```

### 4.4 — Verificar o Servico RabbitMQ

```powershell
Get-Service -Name RabbitMQ
# Status esperado: Running

rabbitmqctl start_app
rabbitmqctl list_users
# Deve listar o usuario: guest
```

> Erlang e RabbitMQ estao instalados. O plugin de management esta habilitado na porta 15672. As credenciais padrao sao `guest` / `guest`.

---

## 8. Passo 5 — Configurar Firewall do Windows e Security Groups AWS

### 5.1 — Abrir Portas no Windows Defender Firewall

```powershell
New-NetFirewallRule -DisplayName 'OrderManagementApi' `
    -Direction Inbound -Protocol TCP -LocalPort 5000 -Action Allow

New-NetFirewallRule -DisplayName 'RabbitMQ Management' `
    -Direction Inbound -Protocol TCP -LocalPort 15672 -Action Allow

New-NetFirewallRule -DisplayName 'RabbitMQ AMQP' `
    -Direction Inbound -Protocol TCP -LocalPort 5672 -Action Allow

# Verificar as regras criadas
Get-NetFirewallRule | Where-Object DisplayName -like 'Order*'
```

### 5.2 — Configurar o Security Group no Console AWS

| Tipo | Protocolo | Porta | Origem | Finalidade |
|---|---|---|---|---|
| RDP | TCP | 3389 | Seu IP | Acesso RDP |
| Custom TCP | TCP | 5000 | 0.0.0.0/0 | API HTTP |
| Custom TCP | TCP | 15672 | Seu IP | RabbitMQ Management UI |
| Custom TCP | TCP | 5672 | Bloqueado | AMQP (apenas interno) |
| Custom TCP | TCP | 5432 | Bloqueado | PostgreSQL (apenas interno) |

> **Aviso de seguranca:** nunca exponha a porta 5432 (PostgreSQL) publicamente. O banco de dados deve ser acessivel exclusivamente pela propria instancia (localhost).

---

## 9. Passo 6 — Transferir o Projeto para a Instancia

### Opcao A — Git Clone (recomendada)

```powershell
winget install Git.Git --silent
# Reabra o PowerShell apos a instalacao

cd C:\
git clone https://github.com/gabrielandre-math/OrderManagement.git OrderManagement
# Resultado: C:\OrderManagement\
```

### Opcao B — Upload via RDP (File Transfer)

No cliente RDP, em **Local Resources -> More**, habilite **Drives** para montar o disco local. Dentro da sessao RDP, abra o Explorer -> This PC -> drives mapeados. Copie a pasta do projeto para `C:\OrderManagement`.

### Opcao C — AWS S3

```powershell
# Na maquina local:
aws s3 cp OrderManagement.tar s3://meu-bucket/

# Na instancia EC2:
winget install Amazon.AWSCLI --silent
aws s3 cp s3://meu-bucket/OrderManagement.tar C:\
tar -xf C:\OrderManagement.tar -C C:\
```

> Ao final, a estrutura do projeto deve estar em `C:\OrderManagement\`, contendo as subpastas `src\` e o arquivo de solucao `OrderManagementApi.slnx`.

---

## 10. Passo 7 — Configuracao (appsettings.json)

O `appsettings.json` base ja esta configurado corretamente para o ambiente Windows nativo. **Nenhuma edicao e necessaria** caso o PostgreSQL e o RabbitMQ estejam em execucao no localhost com as credenciais padrao.

Localizacao: `src\Bootstrapper\Api\appsettings.json`

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

### Mapeamento de Chaves

| Chave | Valor Padrao | Lida Por | Finalidade |
|---|---|---|---|
| `ConnectionStrings:DefaultSchema` | `Host=localhost;...` | EF Core / Npgsql | Connection string do PostgreSQL |
| `RabbitMQ:Host` | `localhost` | `MassTransitExtensions.cs` | Hostname do broker RabbitMQ |
| `RabbitMQ:Port` | `5672` | Informativo | Porta AMQP (MassTransit usa 5672 por padrao) |
| `MessageBroker:UserName` | `guest` | `MassTransitExtensions.cs` | Credencial RabbitMQ |
| `MessageBroker:Password` | `guest` | `MassTransitExtensions.cs` | Credencial RabbitMQ |
| `EnableSwagger` | `true` | `Program.cs` | Habilita Swagger em qualquer environment |

---

## 11. Passo 8 — Build e Publish da API

> **CORRIGIDO** — build via `Api.csproj` (contornando o `.slnx`) e resolucao de conflito `Microsoft.CodeAnalysis`.

### Por que nao usar o .slnx?

O arquivo `OrderManagementApi.slnx` e um formato de solucao novo da Microsoft (XML-based). O SDK do .NET 8 instalado via linha de comando nao reconhece esse formato nativamente — ele exige Visual Studio ou flags de preview. A solucao e apontar o build diretamente para o `Api.csproj`, que carrega todos os modulos automaticamente.

### 8.1 — Resolver Conflito de Versoes (Microsoft.CodeAnalysis)

Se o build retornar o erro `MSB4068` ou conflito de versao do `Microsoft.CodeAnalysis`, execute o bloco abaixo para forcar a versao 4.7.0:

```powershell
cd C:\OrderManagement

dotnet add src/Bootstrapper/Api/Api.csproj package Microsoft.CodeAnalysis.Common --version 4.7.0
dotnet add src/Bootstrapper/Api/Api.csproj package Microsoft.CodeAnalysis.CSharp --version 4.7.0
```

### 8.2 — Restaurar Pacotes NuGet

```powershell
cd C:\OrderManagement

dotnet restore src/Bootstrapper/Api/Api.csproj
```

### 8.3 — Build de Verificacao

```powershell
dotnet build src/Bootstrapper/Api/Api.csproj -c Release
# Saida esperada: Build succeeded.
```

### 8.4 — Publish (Geracao dos Artefatos de Producao)

```powershell
dotnet publish src/Bootstrapper/Api/Api.csproj -c Release -o C:\ApiPublish --no-restore
```

Verifique o conteudo gerado:

```powershell
ls C:\ApiPublish
# Deve conter: Api.exe, Api.dll, appsettings.json, entre outros.
```

> Os artefatos de producao estao em `C:\ApiPublish\` e prontos para execucao. O `Api.exe` ja inclui o suporte a Windows Service via `Microsoft.Extensions.Hosting.WindowsServices` (8.0.0).

---

## 12. Passo 9 — Instalar como Windows Service (Producao)

### Metodo A — Usando sc.exe (nativo do Windows)

```powershell
# Criar o servico
sc.exe create OrderManagementApi `
    binPath= "C:\ApiPublish\Api.exe --urls http://0.0.0.0:5000" `
    start= auto `
    DisplayName= "Order Management API"

# Configurar recuperacao automatica em falha
sc.exe failure OrderManagementApi reset= 60 actions= restart/5000/restart/10000/restart/30000

# Iniciar o servico
sc.exe start OrderManagementApi

# Verificar status
sc.exe query OrderManagementApi
# O campo STATE deve exibir: 4 RUNNING
```

### Metodo B — Usando NSSM

```powershell
winget install NSSM.NSSM

nssm install OrderManagementApi C:\ApiPublish\Api.exe
nssm set OrderManagementApi AppParameters "--urls http://0.0.0.0:5000"
nssm set OrderManagementApi AppEnvironmentExtra "ASPNETCORE_ENVIRONMENT=Production"
nssm set OrderManagementApi Start SERVICE_AUTO_START
nssm set OrderManagementApi ObjectName LocalSystem ""
nssm start OrderManagementApi
```

### Teste Rapido (Sem Instalar como Servico)

```powershell
cd C:\ApiPublish
.\Api.exe --urls "http://0.0.0.0:5000"
```

> Na primeira execucao, a API aplicara automaticamente todas as migracoes pendentes nos tres DbContexts via `UseMigrationAsync<T>()`. Nao e necessario executar `dotnet ef database update`.

---

## 13. Passo 10 — Verificar Todos os Servicos

### 10.1 — Status dos Servicos Windows

```powershell
Get-Service postgresql* | Select-Object Name, Status
# postgresql-x64-16: Running

Get-Service RabbitMQ | Select-Object Name, Status
# RabbitMQ: Running

Get-Service OrderManagementApi | Select-Object Name, Status
# OrderManagementApi: Running

netstat -an | findstr "5000 5432 5672 15672"
```

### 10.2 — Testar a API via PowerShell

```powershell
# Testar o Swagger UI (deve retornar HTTP 200)
Invoke-WebRequest -Uri 'http://localhost:5000/swagger' -UseBasicParsing

# Testar o endpoint de produtos do modulo Catalog
Invoke-WebRequest -Uri 'http://localhost:5000/products' -UseBasicParsing | Select-Object Content

# Testar internacionalizacao (resposta em portugues)
Invoke-WebRequest -Uri 'http://localhost:5000/products' -UseBasicParsing `
    -Headers @{ 'Accept-Language' = 'pt-BR' }
```

### 10.3 — Acessar a Partir do Computador Local

Substitua `IP_PUBLICO_EC2` pelo IP publico da instancia:

| Servico | URL de Acesso |
|---|---|
| Swagger UI (API Docs) | `http://IP_PUBLICO_EC2:5000/swagger` |
| API — Listar Produtos | `http://IP_PUBLICO_EC2:5000/products` |
| RabbitMQ Management UI | `http://IP_PUBLICO_EC2:15672` |
| RabbitMQ — Login | `guest` / `guest` |

> Se o Swagger UI carregar com os endpoints dos modulos Catalog, Basket e Orders visiveis, e o RabbitMQ Management exibir conexoes ativas, o deploy foi concluido com exito.

---

## 14. Resolucao de Problemas Comuns

### `dotnet` nao e reconhecido como comando

Feche e reabra o PowerShell apos instalar o .NET 8.

```powershell
[System.Environment]::GetEnvironmentVariable('Path', 'Machine')
```

### Connection to server on socket failed (PostgreSQL)

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
Start-Service RabbitMQ
Test-NetConnection -ComputerName localhost -Port 5672
# TcpTestSucceeded deve exibir: True
```

Verifique os logs: `C:\Users\%USERNAME%\AppData\Roaming\RabbitMQ\log\`

### `relation does not exist` / Tabelas Nao Encontradas

As migracoes sao aplicadas automaticamente pelo `UseMigrationAsync<T>()`. Se ocorrer, verifique:

```powershell
psql -U postgres -c "\l"
# Confirme que OrderManagementDb existe
# Verifique a connection string no appsettings.json
# Logs: eventvwr -> Windows Logs -> Application
```

### Swagger Nao Carrega (404 em /swagger)

O Swagger e controlado pela chave `EnableSwagger` no `appsettings.json`, **nao** pelo `ASPNETCORE_ENVIRONMENT`:

```powershell
# C:\ApiPublish\appsettings.json deve conter:
# "EnableSwagger": true
Restart-Service OrderManagementApi
```

### Windows Service Falha ao Iniciar

Teste a execucao manual antes de diagnosticar:

```powershell
cd C:\ApiPublish
.\Api.exe --urls "http://0.0.0.0:5000"
# Observe mensagens de erro no console
```

Causas comuns:
- PostgreSQL ou RabbitMQ nao estao rodando
- Connection string incorreta
- Porta 5000 ja em uso

Verifique logs do Event Viewer: `eventvwr` -> Windows Logs -> Application.

### Erro de globalizacao / ICU

O Windows Server 2022 ja inclui ICU libraries. Se ocorrer erro:

```powershell
winget install ICU.icu
```

---

## 15. Referencia Rapida: Portas e Variaveis

### Portas dos Servicos

| Servico | Porta | Protocolo | Expor Externamente? |
|---|---|---|---|
| API (HTTP) | 5000 | TCP / HTTP | Sim |
| Swagger UI | 5000/swagger | HTTP | Sim |
| PostgreSQL | 5432 | TCP | Nao (somente local) |
| RabbitMQ AMQP | 5672 | TCP | Nao (somente local) |
| RabbitMQ Mgmt UI | 15672 | HTTP | Somente IP autorizado |

### Comandos Essenciais de Gerenciamento

```powershell
# Verificar status de todos os servicos
Get-Service postgresql*, RabbitMQ, OrderManagementApi

# Reiniciar todos os servicos
Restart-Service postgresql-x64-16
Restart-Service RabbitMQ
Restart-Service OrderManagementApi

# Atualizar a API (parar -> substituir -> iniciar)
Stop-Service OrderManagementApi
# Atualize os arquivos em C:\ApiPublish\
Start-Service OrderManagementApi

# Consultar logs da API no Event Viewer
Get-EventLog -LogName Application -Source 'OrderManagementApi' -Newest 20

# Consultar logs do RabbitMQ
Get-Content "$env:APPDATA\RabbitMQ\log\rabbit@$env:COMPUTERNAME.log" -Tail 50

# Remover o servico (para recriacao)
sc.exe delete OrderManagementApi
```

### Arquivos-Chave do Projeto

| Arquivo | Finalidade |
|---|---|
| `src/Bootstrapper/Api/Program.cs` | Entry point. Contem `UseWindowsService()`, Swagger via configuracao e auto-migration. |
| `src/Bootstrapper/Api/Api.csproj` | Declara `Microsoft.Extensions.Hosting.WindowsServices` 8.0.0. |
| `src/Bootstrapper/Api/appsettings.json` | Configuracao base: ConnectionString, RabbitMQ, MessageBroker e EnableSwagger. |
| `src/Shared/Shared.Messaging/Extensions/MassTransitExtensions.cs` | Le `RabbitMQ:Host`, `MessageBroker:UserName` e `MessageBroker:Password`. |
| `src/Shared/Shared/Extensions/DatabaseExtensions.cs` | Implementa `UseMigrationAsync<T>()` — aplica migracoes na inicializacao. |

---

> Guia gerado com base no codigo-fonte do repositorio https://github.com/gabrielandre-math/OrderManagement (branch master). ASP.NET Core 8, .NET 8, PostgreSQL 16, RabbitMQ 3.x, MassTransit, MediatR, Carter. Deploy validado em instancia AWS EC2 Windows Server 2022 sem Docker.
