# OrderManagementApi — Diagrama UML 2.0 de Processos de Deploy

## Windows Server via RDP (AWS EC2, Sem Docker)

> Use este diagrama como referencia rapida durante o deploy. Cada swimlane representa uma fase. Os comandos estao inline para copiar e colar direto no PowerShell.

---

## PlantUML Source

Copie o bloco abaixo em https://www.plantuml.com/plantuml/uml para gerar o diagrama visual.

```plantuml
@startuml OrderManagementApi_Deploy_Windows_EC2

title OrderManagementApi — Processo de Deploy\nWindows Server 2022 via RDP (AWS EC2, Sem Docker)\n[UML 2.0 Activity Diagram]

skinparam backgroundColor #FEFEFE
skinparam activity {
  BackgroundColor #E8F4FD
  BorderColor #2980B9
  FontSize 11
}
skinparam partition {
  BackgroundColor #F0F0F0
  BorderColor #7F8C8D
  FontSize 13
  FontStyle bold
}
skinparam note {
  BackgroundColor #FFF3CD
  BorderColor #D4A017
  FontSize 10
}

start

partition "FASE 0 — Provisionar EC2" {
  :Criar instancia EC2
  **AMI:** Windows Server 2022 Base
  **Tipo:** t3.medium (2 vCPU / 4 GB)
  **Disco:** 30 GB gp3;

  :Configurar Security Group
  ----
  TCP 3389 ← Seu IP (RDP)
  TCP 5000 ← 0.0.0.0/0 (API)
  TCP 15672 ← Seu IP (RabbitMQ UI)
  TCP 5672 ← Bloqueado (AMQP interno)
  TCP 5432 ← Bloqueado (PostgreSQL interno);

  :Obter senha do Administrator
  ----
  EC2 → Instances → Connect → RDP Client
  Get Password → colar .pem → Decrypt;
}

partition "FASE 1 — Conectar via RDP" {
  :Conectar com Remote Desktop
  ----
  Computer: <IP_PUBLICO_EC2>
  Username: Administrator
  Password: <senha decriptada>;

  :Abrir PowerShell como Admin
  ----
  Menu Iniciar → botao direito →
  Windows PowerShell (Admin);

  :Verificar conectividade
  ----
  ""nslookup google.com""
  ""[System.Environment]::OSVersion.VersionString"";
}

partition "FASE 2 — Instalar .NET 8 SDK" {
  :Baixar e executar instalador
  ----
  ""$url = 'https://dot.net/v1/dotnet-install.ps1'""
  ""$script = Join-Path $env:TEMP 'dotnet-install.ps1'""
  ""Invoke-WebRequest -Uri $url -OutFile $script""
  ""& $script -Channel 8.0"";

  note right
    Fechar e reabrir
    PowerShell apos instalar
  end note

  :Verificar instalacao
  ----
  ""dotnet --version""
  ""dotnet --list-sdks""
  ""dotnet --list-runtimes"";

  if (dotnet retorna 8.0.x?) then (sim)
  else (nao)
    :Verificar PATH
    ----
    ""[System.Environment]::GetEnvironmentVariable('Path','Machine')"";
    stop
  endif
}

partition "FASE 3 — Instalar PostgreSQL 16" #FCE4EC {
  :Criar pasta e baixar instalador
  ----
  ""New-Item -ItemType Directory -Force -Path C:\Installers""
  ""$pg_url = 'https://get.enterprisedb.com/postgresql/postgresql-16.6-1-windows-x64.exe'""
  ""$pg_file = 'C:\Installers\postgresql-16-setup.exe'""
  ""Invoke-WebRequest -Uri $pg_url -OutFile $pg_file"";

  :Instalacao silenciosa
  ----
  ""Start-Process `""
  ""  -FilePath $pg_file `""
  ""  -Wait `""
  ""  -ArgumentList \"--mode\", \"unattended\", `""
  ""    \"--superpassword\", \"postgres\", `""
  ""    \"--serverport\", \"5432\", `""
  ""    \"--prefix\", \"C:\PostgreSQL\16\", `""
  ""    \"--datadir\", \"C:\PostgreSQL\16\data\""";

  :Adicionar psql ao PATH
  ----
  ""$pg_bin = 'C:\PostgreSQL\16\bin'""
  ""[Environment]::SetEnvironmentVariable('Path', $env:Path + ';' + $pg_bin, 'Machine')"";

  note right
    Reabrir PowerShell
  end note

  :Verificar servico + criar banco
  ----
  ""Get-Service -Name 'postgresql*'""
  ""psql -U postgres -c \"SELECT version();\"
  ""psql -U postgres -c \"CREATE DATABASE \\\"OrderManagementDb\\\";\"
  ""psql -U postgres -c \"\l\""";

  if (postgresql Running + banco criado?) then (sim)
  else (nao)
    :Start-Service postgresql-x64-16
    Verificar pg_hba.conf;
    stop
  endif
}

partition "FASE 4 — Instalar Erlang + RabbitMQ" #FCE4EC {
  :4.1 Instalar Erlang/OTP 26
  ----
  **FIX TLS 1.2:**
  ""[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12""
  ----
  ""$erlang_url = 'https://github.com/erlang/otp/releases/download/OTP-26.2.5/otp_win64_26.2.5.exe'""
  ""$erlang_file = 'C:\Installers\erlang-setup.exe'""
  ""Invoke-WebRequest -Uri $erlang_url -OutFile $erlang_file""
  ""Start-Process -FilePath $erlang_file -Wait -ArgumentList '/S'"";

  :4.2 Instalar RabbitMQ 3.13
  ----
  ""[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12""
  ""$rmq_url = 'https://github.com/rabbitmq/rabbitmq-server/releases/download/v3.13.7/rabbitmq-server-3.13.7.exe'""
  ""$rmq_file = 'C:\Installers\rabbitmq-setup.exe'""
  ""Invoke-WebRequest -Uri $rmq_url -OutFile $rmq_file""
  ""Start-Process -FilePath $rmq_file -Wait -ArgumentList '/S'"";

  :Adicionar sbin ao PATH
  ----
  ""$rmq_bin = 'C:\Program Files\RabbitMQ Server\rabbitmq_server-3.13.7\sbin'""
  ""[Environment]::SetEnvironmentVariable('Path', $env:Path + ';' + $rmq_bin, 'Machine')"";

  note right
    Reabrir PowerShell
  end note

  :Habilitar Management Plugin
  ----
  ""rabbitmq-plugins enable rabbitmq_management"";

  :4.3 FIX Cookie do Erlang (obrigatorio)
  ----
  ""Copy-Item `""
  ""  -Path \"C:\Windows\System32\config\systemprofile\.erlang.cookie\" `""
  ""  -Destination \"C:\Users\Administrator\.erlang.cookie\" `""
  ""  -Force""
  ""Restart-Service RabbitMQ"";

  :4.4 Verificar RabbitMQ
  ----
  ""Get-Service -Name RabbitMQ""
  ""rabbitmqctl start_app""
  ""rabbitmqctl list_users"";

  if (RabbitMQ Running + guest listado?) then (sim)
  else (nao)
    :Verificar logs em
    %APPDATA%\RabbitMQ\log\;
    stop
  endif
}

partition "FASE 5 — Firewall Windows" {
  :Abrir portas no Firewall
  ----
  ""New-NetFirewallRule -DisplayName 'OrderManagementApi' `""
  ""  -Direction Inbound -Protocol TCP -LocalPort 5000 -Action Allow""
  ----
  ""New-NetFirewallRule -DisplayName 'RabbitMQ Management' `""
  ""  -Direction Inbound -Protocol TCP -LocalPort 15672 -Action Allow""
  ----
  ""New-NetFirewallRule -DisplayName 'RabbitMQ AMQP' `""
  ""  -Direction Inbound -Protocol TCP -LocalPort 5672 -Action Allow"";
}

partition "FASE 6 — Transferir Projeto" {
  :Instalar Git e clonar repositorio
  ----
  ""winget install Git.Git --silent""
  **Reabrir PowerShell**
  ""cd C:\""
  ""git clone https://github.com/gabrielandre-math/OrderManagement.git OrderManagement"";

  :Verificar estrutura
  ----
  C:\OrderManagement\src\
  C:\OrderManagement\OrderManagementApi.slnx;
}

partition "FASE 7 — Configuracao" {
  :appsettings.json ja esta correto
  ----
  ConnectionStrings → localhost:5432 / postgres
  RabbitMQ:Host → localhost
  MessageBroker → guest / guest
  EnableSwagger → true
  ----
  **Nenhuma edicao necessaria**
  (valores padrao apontam para localhost);

  note right
    Swagger funciona em
    qualquer environment
    via EnableSwagger: true
  end note
}

partition "FASE 8 — Build e Publish" #FCE4EC {
  :8.1 (se necessario) Resolver conflito CodeAnalysis
  ----
  ""cd C:\OrderManagement""
  ""dotnet add src/Bootstrapper/Api/Api.csproj package Microsoft.CodeAnalysis.Common --version 4.7.0""
  ""dotnet add src/Bootstrapper/Api/Api.csproj package Microsoft.CodeAnalysis.CSharp --version 4.7.0"";

  note right
    Executar apenas se
    MSB4068 ocorrer
    no build
  end note

  :8.2 Restaurar pacotes
  ----
  ""dotnet restore src/Bootstrapper/Api/Api.csproj"";

  repeat
    :8.3 Build de verificacao
    ----
    ""dotnet build src/Bootstrapper/Api/Api.csproj -c Release"";

  backward :Executar passo 8.1 (fix CodeAnalysis)
  e tentar build novamente;

  repeat while (Build succeeded?) is (nao) not (sim)

  :8.4 Publish
  ----
  ""dotnet publish src/Bootstrapper/Api/Api.csproj -c Release -o C:\ApiPublish --no-restore""
  ----
  ""ls C:\ApiPublish""
  Deve conter: Api.exe, Api.dll, appsettings.json;
}

partition "FASE 9 — Windows Service" {
  if (Teste rapido ou Producao?) then (Teste rapido)
    :Executar diretamente
    ----
    ""cd C:\ApiPublish""
    "".\Api.exe --urls \"http://0.0.0.0:5000\""";
  else (Producao — sc.exe)
    :Criar servico Windows
    ----
    ""sc.exe create OrderManagementApi `""
    ""  binPath= \"C:\ApiPublish\Api.exe --urls http://0.0.0.0:5000\" `""
    ""  start= auto `""
    ""  DisplayName= \"Order Management API\""";

    :Configurar recuperacao em falha
    ----
    ""sc.exe failure OrderManagementApi reset= 60 actions= restart/5000/restart/10000/restart/30000"";

    :Iniciar servico
    ----
    ""sc.exe start OrderManagementApi"";

    :Verificar status
    ----
    ""sc.exe query OrderManagementApi""
    STATE deve ser: 4 RUNNING;
  endif

  note right
    Auto-migration executa
    na 1a inicializacao:
    UseMigrationAsync<T>()
    para CatalogDbContext,
    BasketDbContext e
    OrdersDbContext
  end note
}

partition "FASE 10 — Validacao Final" {
  :Verificar servicos Windows
  ----
  ""Get-Service postgresql* | Select Name, Status""
  ""Get-Service RabbitMQ | Select Name, Status""
  ""Get-Service OrderManagementApi | Select Name, Status""
  ""netstat -an | findstr \"5000 5432 5672 15672\""";

  :Testar API via PowerShell
  ----
  ""Invoke-WebRequest -Uri 'http://localhost:5000/swagger' -UseBasicParsing""
  ""Invoke-WebRequest -Uri 'http://localhost:5000/products' -UseBasicParsing""
  ""Invoke-WebRequest -Uri 'http://localhost:5000/products' -UseBasicParsing `""
  ""  -Headers @{ 'Accept-Language' = 'pt-BR' }"";

  :Testar acesso externo
  ----
  http://IP_PUBLICO:5000/swagger
  http://IP_PUBLICO:5000/products
  http://IP_PUBLICO:15672 (guest/guest);

  if (Swagger carrega + endpoints visiveis?) then (sim)
    #LimeGreen:DEPLOY CONCLUIDO COM SUCESSO;
  else (nao)
    :Consultar troubleshooting
    ----
    eventvwr → Windows Logs → Application
    Verificar: PostgreSQL, RabbitMQ, API;
    stop
  endif
}

stop

legend right
  |= Porta |= Servico |= Expor? |
  | 5000 | API HTTP + Swagger | Sim |
  | 5432 | PostgreSQL | Nao (local) |
  | 5672 | RabbitMQ AMQP | Nao (local) |
  | 15672 | RabbitMQ Mgmt UI | So seu IP |
  | 3389 | RDP | Seu IP |
  ----
  **Credenciais Padrao:**
  PostgreSQL: postgres / postgres
  RabbitMQ: guest / guest
  ----
  **Comandos de Gerenciamento:**
  Restart-Service postgresql-x64-16
  Restart-Service RabbitMQ
  Restart-Service OrderManagementApi
  sc.exe delete OrderManagementApi
endlegend

@enduml
```

---

## Como Visualizar

1. **Online:** Cole o bloco PlantUML em https://www.plantuml.com/plantuml/uml
2. **VS Code:** Instale a extensao "PlantUML" (jebbs.plantuml) e abra o preview com `Alt+D`
3. **IntelliJ / Rider:** Plugin PlantUML integration nativo
4. **CLI:** `java -jar plantuml.jar docs/deploy-uml-diagram.md`

---

## Resumo das Fases

| Fase | Descricao | Passos Corrigidos |
|------|-----------|-------------------|
| 0 | Provisionar EC2 (AMI, Security Group, Key Pair) | — |
| 1 | Conectar via RDP + PowerShell Admin | — |
| 2 | Instalar .NET 8 SDK | — |
| 3 | Instalar PostgreSQL 16 + criar banco | URL fix, aspas duplas |
| 4 | Instalar Erlang + RabbitMQ | TLS 1.2, cookie fix |
| 5 | Firewall Windows + Security Groups AWS | — |
| 6 | Git clone do repositorio | — |
| 7 | Validar appsettings.json (nenhuma edicao) | — |
| 8 | Build e Publish via Api.csproj | .slnx bypass, CodeAnalysis fix |
| 9 | Instalar como Windows Service (sc.exe) | — |
| 10 | Validacao final (Swagger + endpoints + RabbitMQ) | — |
