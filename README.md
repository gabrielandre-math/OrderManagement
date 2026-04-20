# Order Management API

A modular .NET 8 API with a module-based architecture (Catalog, Basket, Orders), using PostgreSQL, RabbitMQ, MediatR, FluentValidation, Carter, MassTransit, and internationalization (i18n) with support for 33 languages.

---

## Table of Contents

- [Prerequisites](#prerequisites)
- [Running Locally](#running-locally)
- [Deploy on AWS EC2 Free Tier (Windows + RDP)](#deploy-on-aws-ec2-free-tier-windows--rdp)
  - [1. Create the EC2 Instance](#1-create-the-ec2-instance)
  - [2. Connect via RDP](#2-connect-via-rdp)
  - [3. Install Docker on EC2](#3-install-docker-on-ec2)
  - [4. Clone the Project](#4-clone-the-project)
  - [5. Start the Application](#5-start-the-application)
  - [6. Open Ports in the Security Group](#6-open-ports-in-the-security-group)
  - [7. Validate the Deployment](#7-validate-the-deployment)
- [Endpoints](#endpoints)
- [Internationalization (i18n)](#internationalization-i18n)
- [AWS Cost Estimate](#aws-cost-estimate)

---

## Prerequisites

| Tool | Minimum Version |
|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download/dotnet/8.0) | 8.0 |
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) | 24+ |
| [Git](https://git-scm.com/) | 2.40+ |

---

## Running Locally

```bash
# 1. Clone the repository
git clone <repository-url>
cd OrderManagementApi

# 2. Start everything with Docker Compose (API + PostgreSQL + RabbitMQ)
docker-compose up -d --build

# 3. Check if it's running
docker-compose ps

# 4. Test
curl http://localhost:5000/swagger
```

| Service | URL |
|---|---|
| API | http://localhost:5000 |
| Swagger | http://localhost:5000/swagger |
| RabbitMQ Management | http://localhost:15672 (guest/guest) |
| PostgreSQL | localhost:5432 (postgres/postgres) |

To stop:
```bash
docker-compose down
```

To stop and **delete all data**:
```bash
docker-compose down -v
```

---

## Deploy on AWS EC2 Free Tier (Windows + RDP)

### 1. Create the EC2 Instance

1. Go to the [AWS Console](https://console.aws.amazon.com/ec2/)
2. Click **Launch Instance**
3. Configure:

| Field | Value |
|---|---|
| **Name** | `OrderManagementApi` |
| **AMI** | `Microsoft Windows Server 2022 Base` (Free tier eligible) |
| **Instance type** | `t2.micro` (Free tier — 1 vCPU, 1 GB RAM) |
| **Key pair** | Create a new one → `ordermanagement-key` → **Download .pem** |
| **Storage** | **30 GB** gp3 (free tier maximum) |

4. Under **Network settings**, create a Security Group with the following rules:

| Type | Port | Source | Description |
|---|---|---|---|
| RDP | 3389 | My IP | RDP access |
| Custom TCP | 5000 | 0.0.0.0/0 | API |
| Custom TCP | 15672 | My IP | RabbitMQ UI |

5. Click **Launch Instance**

> **Note:** `t2.micro` has only 1 GB of RAM. The Docker build may fail due to insufficient memory. In that case, consider `t2.small` (2 GB) or `t3.small` — outside the free tier, costing approximately $0.02/hour (~$15/month).

---

### 2. Connect via RDP

1. In the EC2 console, select the instance → **Connect** → **RDP client** tab
2. Click **Get password** → upload the `.pem` file → **Decrypt password**
3. Note the **Public DNS**, **Username** (`Administrator`) and **Password**
4. On your machine, open **Remote Desktop Connection** (`mstsc`):
   - **Computer:** `<instance-Public-DNS>`
   - **Username:** `Administrator`
   - **Password:** the decrypted password

---

### 3. Install Docker on EC2

Inside the instance via RDP, open **PowerShell as Administrator** and run:

```powershell
# ─── Option A: Docker Desktop (easier, uses more RAM) ───

# Download and install Docker Desktop
Invoke-WebRequest -Uri "https://desktop.docker.com/win/main/amd64/Docker%20Desktop%20Installer.exe" -OutFile "$env:TEMP\DockerDesktopInstaller.exe"
Start-Process -Wait -FilePath "$env:TEMP\DockerDesktopInstaller.exe" -ArgumentList "install","--quiet","--accept-license"

# Restart the machine (required)
Restart-Computer -Force
```

After restarting, reconnect via RDP and verify:
```powershell
docker --version
docker-compose --version
```

> **Recommended alternative for t2.micro:** Use an Amazon Linux 2023 AMI instead of Windows, as it consumes significantly less RAM:
> ```bash
> sudo yum install -y docker
> sudo systemctl start docker
> sudo systemctl enable docker
> sudo usermod -aG docker ec2-user
> sudo curl -L "https://github.com/docker/compose/releases/latest/download/docker-compose-$(uname -s)-$(uname -m)" -o /usr/local/bin/docker-compose
> sudo chmod +x /usr/local/bin/docker-compose
> ```

---

### 4. Clone the Project

```powershell
# Install Git (if not already installed)
winget install --id Git.Git -e --source winget

# Clone the repository
cd C:\
git clone <repository-url>
cd OrderManagementApi
```

Alternatively, copy the files via RDP (drag and drop or shared folder).

---

### 5. Start the Application

```powershell
cd C:\OrderManagementApi

# Build and start all containers
docker-compose up -d --build

# Follow the logs
docker-compose logs -f api

# Check status
docker-compose ps
```

Expected output:
```
NAME                      STATUS
orderdb                   running   0.0.0.0:5432->5432
messagebroker             running   0.0.0.0:5672->5672, 0.0.0.0:15672->15672
ordermanagement-api       running   0.0.0.0:5000->8080
```

> **If the build fails due to insufficient memory**, increase virtual memory:
> ```powershell
> # On Windows, increase virtual memory:
> # System Properties → Advanced → Performance Settings → Advanced → Virtual Memory → Change
> # Set custom size: Initial 2048 MB, Maximum 4096 MB
> ```

---

### 6. Open Ports in the Security Group

If you did not do this in Step 1, go to the AWS console:

1. **EC2** → **Instances** → select the instance
2. **Security** tab → click the **Security Group**
3. **Edit inbound rules** → add:

| Type | Port | Source |
|---|---|---|
| Custom TCP | 5000 | 0.0.0.0/0 |
| Custom TCP | 15672 | My IP |

4. **Save rules**

Also open the ports in the **Windows Firewall** inside the EC2:
```powershell
New-NetFirewallRule -DisplayName "API Port 5000" -Direction Inbound -Port 5000 -Protocol TCP -Action Allow
New-NetFirewallRule -DisplayName "RabbitMQ UI" -Direction Inbound -Port 15672 -Protocol TCP -Action Allow
```

---

### 7. Validate the Deployment

Replace `<EC2-PUBLIC-IP>` with the public IP of the instance (visible in the EC2 console):

```bash
# Swagger
http://<EC2-PUBLIC-IP>:5000/swagger

# Test endpoint with i18n
curl -X GET http://<EC2-PUBLIC-IP>:5000/api/products \
  -H "Accept-Language: ja"

# RabbitMQ Management
http://<EC2-PUBLIC-IP>:15672  (guest/guest)
```

---

## Endpoints

| Method | Route | Description |
|---|---|---|
| `GET` | `/api/products` | List products |
| `GET` | `/api/products/{id}` | Get product by ID |
| `POST` | `/api/products` | Create product |
| `PUT` | `/api/products/{id}` | Update product |
| `DELETE` | `/api/products/{id}` | Delete product |

---

## Internationalization (i18n)

The API supports 33 languages via the `Accept-Language` header. If the requested language has no translation, it falls back to `en-US`.

| Language | Code | Language | Code |
|---|---|---|---|
| English (default) | `en-US` | Polish | `pl` |
| Portuguese (Brazil) | `pt-BR` | Dutch | `nl` |
| Spanish | `es` | Swedish | `sv` |
| French | `fr` | Danish | `da` |
| Japanese | `ja` | Norwegian | `no` |
| Chinese Simplified | `zh-CN` | Finnish | `fi` |
| Chinese Traditional | `zh-TW` | Czech | `cs` |
| Korean | `ko` | Hungarian | `hu` |
| German | `de` | Romanian | `ro` |
| Italian | `it` | Bulgarian | `bg` |
| Russian | `ru` | Greek | `el` |
| Arabic | `ar` | Turkish | `tr` |
| Hindi | `hi` | Thai | `th` |
| Vietnamese | `vi` | Indonesian | `id` |
| Malay | `ms` | Ukrainian | `uk` |
| Slovak | `sk` | Croatian | `hr` |
| Serbian | `sr` | Slovenian | `sl` |
| Lithuanian | `lt` | Latvian | `lv` |
| Estonian | `et` | Hebrew | `he` |
| Persian | `fa` | Bengali | `bn` |
| Swahili | `sw` | Catalan | `ca` |
| Basque | `eu` | | |

Example request with Japanese:
```bash
curl -X PUT http://localhost:5000/api/products/{id} \
  -H "Content-Type: application/json" \
  -H "Accept-Language: ja" \
  -d '{"name":"","description":"Test","price":-1}'

# Response in Japanese:
# { "title": "Validation.Failed", "status": 400, "detail": "製品名は必須です。; 製品価格はゼロより大きくなければなりません。" }
```

---

## AWS Cost Estimate

| Resource | Free Tier | After 12 months |
|---|---|---|
| EC2 t2.micro | **750h/month free** (12 months) | ~$8.50/month |
| EBS 30 GB gp3 | **30 GB free** (12 months) | ~$2.40/month |
| Data transfer | **100 GB/month free** | $0.09/GB |
| **Total** | **$0.00** | **~$11/month** |

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
