# Order Management API

API modular em .NET 8 com arquitetura de módulos (Catalog, Basket, Orders), utilizando PostgreSQL, RabbitMQ, MediatR, FluentValidation, Carter, MassTransit e internacionalização (i18n) com suporte a 33 idiomas.

---

## Sumário

- [Pré-requisitos](#pré-requisitos)
- [Execução local](#execução-local)
- [Deploy na AWS EC2 Free Tier (Windows + RDP)](#deploy-na-aws-ec2-free-tier-windows--rdp)
  - [1. Criar a instância EC2](#1-criar-a-instância-ec2)
  - [2. Conectar via RDP](#2-conectar-via-rdp)
  - [3. Instalar Docker na EC2](#3-instalar-docker-na-ec2)
  - [4. Clonar o projeto](#4-clonar-o-projeto)
  - [5. Subir a aplicação](#5-subir-a-aplicação)
  - [6. Liberar portas no Security Group](#6-liberar-portas-no-security-group)
  - [7. Validar o deploy](#7-validar-o-deploy)
- [Endpoints](#endpoints)
- [Internacionalização (i18n)](#internacionalização-i18n)
- [Estimativa de custos AWS](#estimativa-de-custos-aws)
- [Observações](#observações)

---

## Pré-requisitos

| Ferramenta | Versão mínima |
|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download/dotnet/8.0) | 8.0 |
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) | 24+ |
| [Git](https://git-scm.com/) | 2.40+ |

---

## Execução local

```bash
# 1. Clonar o repositório
git clone <url-do-repositorio>
cd OrderManagementApi

# 2. Subir tudo com Docker Compose (API + PostgreSQL + RabbitMQ)
docker-compose up -d --build

# 3. Verificar se está rodando
docker-compose ps

# 4. Testar
curl http://localhost:5000/swagger
```

| Serviço | URL |
|---|---|
| API | http://localhost:5000 |
| Swagger | http://localhost:5000/swagger |
| RabbitMQ Management | http://localhost:15672 (guest/guest) |
| PostgreSQL | localhost:5432 (postgres/postgres) |

Para parar:
```bash
docker-compose down
```

Para parar e **apagar dados**:
```bash
docker-compose down -v
```

---

## Deploy na AWS EC2 Free Tier (Windows + RDP)

### 1. Criar a instância EC2

1. Acesse o [Console AWS](https://console.aws.amazon.com/ec2/)
2. Clique em **Launch Instance**
3. Configure:

| Campo | Valor |
|---|---|
| **Name** | `OrderManagementApi` |
| **AMI** | `Microsoft Windows Server 2022 Base` (Free tier eligible) |
| **Instance type** | `t2.micro` (Free tier — 1 vCPU, 1 GB RAM) |
| **Key pair** | Crie um novo → `ordermanagement-key` → **Download .pem** |
| **Storage** | **30 GB** gp3 (máximo do free tier) |

4. Em **Network settings**, crie um Security Group com as regras:

| Tipo | Porta | Origem | Descrição |
|---|---|---|---|
| RDP | 3389 | Meu IP | Acesso RDP |
| Custom TCP | 5000 | 0.0.0.0/0 | API |
| Custom TCP | 15672 | Meu IP | RabbitMQ UI |

5. Clique **Launch Instance**

> **Nota:** O `t2.micro` possui apenas 1 GB de RAM. O build do Docker pode falhar por falta de memória. Nesse caso, considere `t2.small` (2 GB) ou `t3.small` — fora do free tier, custam aproximadamente $0.02/hora (~$15/mês).

---

### 2. Conectar via RDP

1. No console EC2, selecione a instância → **Connect** → aba **RDP client**
2. Clique em **Get password** → faça upload do arquivo `.pem` → **Decrypt password**
3. Anote o **Public DNS**, **Username** (`Administrator`) e **Password**
4. No seu computador, abra **Remote Desktop Connection** (`mstsc`):
   - **Computer:** `<Public-DNS-da-instância>`
   - **Username:** `Administrator`
   - **Password:** a senha decriptada

---

### 3. Instalar Docker na EC2

Dentro da instância via RDP, abra o **PowerShell como Administrador** e execute:

```powershell
# ─── Opção A: Docker Desktop (mais fácil, usa mais RAM) ───

# Baixar e instalar Docker Desktop
Invoke-WebRequest -Uri "https://desktop.docker.com/win/main/amd64/Docker%20Desktop%20Installer.exe" -OutFile "$env:TEMP\DockerDesktopInstaller.exe"
Start-Process -Wait -FilePath "$env:TEMP\DockerDesktopInstaller.exe" -ArgumentList "install","--quiet","--accept-license"

# Reiniciar a máquina (obrigatório)
Restart-Computer -Force
```

Após reiniciar, reconecte via RDP e verifique:
```powershell
docker --version
docker-compose --version
```

> **Alternativa recomendada para t2.micro:** Utilizar uma AMI Amazon Linux 2023 em vez de Windows, pois consome significativamente menos RAM:
> ```bash
> sudo yum install -y docker
> sudo systemctl start docker
> sudo systemctl enable docker
> sudo usermod -aG docker ec2-user
> sudo curl -L "https://github.com/docker/compose/releases/latest/download/docker-compose-$(uname -s)-$(uname -m)" -o /usr/local/bin/docker-compose
> sudo chmod +x /usr/local/bin/docker-compose
> ```

---

### 4. Clonar o projeto

```powershell
# Instalar Git (se não tiver)
winget install --id Git.Git -e --source winget

# Clonar o repositório
cd C:\
git clone <url-do-repositorio>
cd OrderManagementApi
```

Alternativamente, copie os arquivos via RDP (arrastar e soltar ou pasta compartilhada).

---

### 5. Subir a aplicação

```powershell
cd C:\OrderManagementApi

# Build e start de todos os containers
docker-compose up -d --build

# Acompanhar os logs
docker-compose logs -f api

# Verificar status
docker-compose ps
```

Resultado esperado:
```
NAME                      STATUS
orderdb                   running   0.0.0.0:5432->5432
messagebroker             running   0.0.0.0:5672->5672, 0.0.0.0:15672->15672
ordermanagement-api       running   0.0.0.0:5000->8080
```

> **Se o build falhar por falta de memória**, aumente a memória virtual:
> ```powershell
> # No Windows, aumente o virtual memory:
> # System Properties → Advanced → Performance Settings → Advanced → Virtual Memory → Change
> # Set custom size: Initial 2048 MB, Maximum 4096 MB
> ```

---

### 6. Liberar portas no Security Group

Se não fez no Passo 1, vá ao console AWS:

1. **EC2** → **Instances** → selecione a instância
2. Aba **Security** → clique no **Security Group**
3. **Edit inbound rules** → adicione:

| Type | Port | Source |
|---|---|---|
| Custom TCP | 5000 | 0.0.0.0/0 |
| Custom TCP | 15672 | My IP |

4. **Save rules**

Também libere no **Windows Firewall** dentro da EC2:
```powershell
New-NetFirewallRule -DisplayName "API Port 5000" -Direction Inbound -Port 5000 -Protocol TCP -Action Allow
New-NetFirewallRule -DisplayName "RabbitMQ UI" -Direction Inbound -Port 15672 -Protocol TCP -Action Allow
```

---

### 7. Validar o deploy

Substitua `<EC2-PUBLIC-IP>` pelo IP público da instância (visível no console EC2):

```bash
# Swagger
http://<EC2-PUBLIC-IP>:5000/swagger

# Testar endpoint com i18n
curl -X GET http://<EC2-PUBLIC-IP>:5000/api/products \
  -H "Accept-Language: ja"

# RabbitMQ Management
http://<EC2-PUBLIC-IP>:15672  (guest/guest)
```

---

## Endpoints

| Método | Rota | Descrição |
|---|---|---|
| `GET` | `/api/products` | Listar produtos |
| `GET` | `/api/products/{id}` | Buscar produto por ID |
| `POST` | `/api/products` | Criar produto |
| `PUT` | `/api/products/{id}` | Atualizar produto |
| `DELETE` | `/api/products/{id}` | Deletar produto |

---

## Internacionalização (i18n)

A API suporta 33 idiomas via header `Accept-Language`. Se o idioma solicitado não possuir tradução, o fallback é `en-US`.

| Idioma | Código | Idioma | Código |
|---|---|---|---|
| English (padrão) | `en-US` | Polski | `pl` |
| Português (Brasil) | `pt-BR` | Nederlands | `nl` |
| Español | `es` | Svenska | `sv` |
| Français | `fr` | Dansk | `da` |
| 日本語 | `ja` | Norsk | `no` |
| 简体中文 | `zh-CN` | Suomi | `fi` |
| 繁體中文 | `zh-TW` | Čeština | `cs` |
| 한국어 | `ko` | Magyar | `hu` |
| Deutsch | `de` | Română | `ro` |
| Italiano | `it` | Български | `bg` |
| Русский | `ru` | Ελληνικά | `el` |
| العربية | `ar` | Türkçe | `tr` |
| हिन्दी | `hi` | ไทย | `th` |
| Tiếng Việt | `vi` | Bahasa Indonesia | `id` |
| Bahasa Melayu | `ms` | Українська | `uk` |
| Slovenčina | `sk` | Hrvatski | `hr` |
| Српски | `sr` | Slovenščina | `sl` |
| Lietuvių | `lt` | Latviešu | `lv` |
| Eesti | `et` | עברית | `he` |
| فارسی | `fa` | বাংলা | `bn` |
| Kiswahili | `sw` | Català | `ca` |
| Euskara | `eu` | | |

Exemplo de requisição com idioma japonês:
```bash
curl -X PUT http://localhost:5000/api/products/{id} \
  -H "Content-Type: application/json" \
  -H "Accept-Language: ja" \
  -d '{"name":"","description":"Test","price":-1}'

# Resposta em japonês:
# { "title": "Validation.Failed", "status": 400, "detail": "製品名は必須です。; 製品価格はゼロより大きくなければなりません。" }
```

---

## Estimativa de custos AWS

| Recurso | Free Tier | Após 12 meses |
|---|---|---|
| EC2 t2.micro | **750h/mês grátis** (12 meses) | ~$8.50/mês |
| EBS 30 GB gp3 | **30 GB grátis** (12 meses) | ~$2.40/mês |
| Data transfer | **100 GB/mês grátis** | $0.09/GB |
| **Total** | **$0.00** | **~$11/mês** |

---

## Observações

1. **t2.micro (1 GB RAM)** é apertado para Docker + PostgreSQL + RabbitMQ + API. Se travar, use `t2.small` ($0.023/h ≈ $17/mês).

2. **Não exponha o PostgreSQL (5432)** para a internet. Mantenha apenas acesso interno entre containers.

3. **Pare a instância** quando não estiver usando para não gastar horas do free tier:
   ```
   Console AWS → EC2 → Instances → Stop instance
   ```

4. **Elastic IP:** O IP público muda quando você para/inicia a instância. Para IP fixo, associe um Elastic IP (grátis enquanto associado a uma instância rodando).

5. **Backups:** Configure snapshots automáticos do EBS para não perder dados do PostgreSQL.
