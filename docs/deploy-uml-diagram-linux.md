# OrderManagementApi — Diagrama UML 2.0 de Processos de Deploy

## Amazon Linux 2023 via SSH (AWS EC2, Docker Compose)

> Use este diagrama como referencia rapida durante o deploy. Cada swimlane representa uma fase. Os comandos estao inline para copiar e colar direto no terminal SSH.

---

## PlantUML Source

Copie o bloco abaixo em https://www.plantuml.com/plantuml/uml para gerar o diagrama visual.

```plantuml
@startuml OrderManagementApi_Deploy_Linux_EC2

title OrderManagementApi — Processo de Deploy\nAmazon Linux 2023 via SSH (AWS EC2, Docker Compose)\n[UML 2.0 Activity Diagram]

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
  **AMI:** Amazon Linux 2023
  **Tipo:** t3.small (2 vCPU / 2 GB)
  **Disco:** 30 GiB gp3
  **Key Pair:** gandre.pem;

  :Configurar Security Group
  ----
  TCP 22 <- Meu IP (SSH)
  TCP 5000 <- 0.0.0.0/0 (API publica)
  TCP 15672 <- Meu IP (RabbitMQ UI)
  TCP 5432 <- Sem exposicao (interno Docker)
  TCP 5672 <- Sem exposicao (interno Docker);

  note right
    NUNCA exponha SSH (22)
    ou RabbitMQ (15672)
    para 0.0.0.0/0
  end note
}

partition "FASE 1 — Conectar via SSH" {
  if (Metodo de conexao?) then (EC2 Instance Connect)
    :Console AWS -> Instancia -> Conectar
    -> aba EC2 Instance Connect -> Conectar
    ----
    Terminal abre no navegador;

    note right
      Se porta 22 restrita a Meu IP,
      Instance Connect pode falhar
      (IPs da AWS). Use SSH local.
    end note

  else (SSH local)
    :Conectar pelo terminal local
    ----
    ""cd ~/Downloads""
    ""ssh -i gandre.pem ec2-user@<IP_PUBLICO_EC2>""
    ----
    Primeira vez: digitar **yes** para fingerprint;
  endif

  :Verificar conectividade
  ----
  Prompt esperado:
  [ec2-user@ip-xxx-xxx-xxx-xxx ~]$;
}

partition "FASE 2 — Instalar Git e Docker" {
  :Atualizar pacotes do sistema
  ----
  ""sudo yum update -y"";

  :Instalar Git e Docker
  ----
  ""sudo yum install git docker -y"";

  :Iniciar e habilitar Docker
  ----
  ""sudo systemctl start docker""
  ""sudo systemctl enable docker"";

  :Verificar Docker
  ----
  ""sudo systemctl status docker"";

  if (Docker active running?) then (nao)
    :Verificar logs
    ----
    ""sudo journalctl -u docker --no-pager -n 50"";
  else (sim)
  endif
}

partition "FASE 3 — Instalar Docker Compose v2" #FCE4EC {
  :Criar diretorio de plugins
  ----
  ""sudo mkdir -p /usr/local/lib/docker/cli-plugins"";

  :Baixar Docker Compose v2
  ----
  ""sudo curl -SL https://github.com/docker/compose/releases/download/v2.26.1/docker-compose-linux-x86_64 -o /usr/local/lib/docker/cli-plugins/docker-compose"";

  :Dar permissao de execucao
  ----
  ""sudo chmod +x /usr/local/lib/docker/cli-plugins/docker-compose"";

  :Verificar versao
  ----
  ""docker compose version"";

  note right
    Esperado: Docker Compose
    version v2.26.1
    Usar **docker compose**
    (sem hifen, v2)
  end note

  if (docker compose v2.x OK?) then (nao)
    :Verificar binario em
    /usr/local/lib/docker/cli-plugins/
    e permissao +x;
  else (sim)
  endif
}

partition "FASE 4 — Clonar Repositorio" {
  :Navegar e clonar
  ----
  ""cd /home/ec2-user""
  ""git clone https://github.com/gabrielandre-math/OrderManagement.git OrderManagementApi"";

  :Entrar no projeto e verificar
  ----
  ""cd OrderManagementApi""
  ""ls -la"";

  if (docker-compose.yml presente?) then (nao)
    :Verificar URL do clone
    e branch correta;
  else (sim)
  endif
}

partition "FASE 5 — Build e Subir Containers" #FCE4EC {
  :Build + iniciar todos os containers
  ----
  ""sudo docker compose up -d --build"";

  note right
    Esse comando:
    1. Builda a imagem da API
    2. Puxa postgres:15 e rabbitmq:3-management
    3. Cria rede interna Docker
    4. Inicia os 3 containers
  end note

  :Acompanhar logs (opcional)
  ----
  ""sudo docker compose logs -f""
  ----
  Ctrl+C para sair sem parar containers;

  :Verificar status
  ----
  ""sudo docker compose ps"";

  if (3 containers running?) then (nao)
    :Verificar logs do container com falha
    ----
    ""sudo docker compose logs postgres""
    ""sudo docker compose logs rabbitmq""
    ""sudo docker compose logs api""
    ----
    Se OOM: considerar t3.medium (4 GB);
  else (sim)
  endif
}

partition "FASE 6 — Validacao Final" {
  note right
    No Linux NAO e necessario
    configurar firewall interno.
    Security Groups da AWS
    sao suficientes.
  end note

  :Testar API via curl (dentro da EC2)
  ----
  ""curl -s http://localhost:5000/swagger | head -20""
  ""curl -s http://localhost:5000/products"";

  :Testar acesso externo (navegador local)
  ----
  http://<IP_PUBLICO_EC2>:5000/swagger
  http://<IP_PUBLICO_EC2>:15672
  (guest / guest);

  if (Swagger + RabbitMQ OK?) then (sim)
    #LimeGreen:DEPLOY CONCLUIDO COM SUCESSO;
  else (nao)
    :Verificar Security Group e containers
    ----
    Porta 5000 aberta para 0.0.0.0/0?
    Porta 15672 aberta para Meu IP?
    ""sudo docker compose ps""
    ""sudo docker compose logs -f"";
  endif
}

stop

legend right
  |= Porta |= Servico |= Expor? |
  | 5000 | API HTTP + Swagger | Sim (0.0.0.0/0) |
  | 5432 | PostgreSQL | Nao (interno Docker) |
  | 5672 | RabbitMQ AMQP | Nao (interno Docker) |
  | 15672 | RabbitMQ Mgmt UI | So Meu IP |
  | 22 | SSH | So Meu IP |
  ----
  **Credenciais Padrao:**
  PostgreSQL: postgres (via docker-compose.yml)
  RabbitMQ: guest / guest
  ----
  **Comandos de Manutencao:**
  sudo docker compose down
  sudo docker compose down -v (APAGA BANCO)
  sudo docker compose restart api
  sudo docker compose logs postgres
  sudo docker compose logs rabbitmq
endlegend

@enduml
```

---

## Como Visualizar

1. **Online:** Cole o bloco PlantUML em https://www.plantuml.com/plantuml/uml
2. **VS Code:** Instale a extensao "PlantUML" (jebbs.plantuml) e abra o preview com `Alt+D`
3. **IntelliJ / Rider:** Plugin PlantUML integration nativo
4. **CLI:** `java -jar plantuml.jar docs/deploy-uml-diagram-linux.md`

---

## Resumo das Fases

| Fase | Descricao | Observacao |
|------|-----------|------------|
| 0 | Provisionar EC2 (AMI Amazon Linux 2023, SG, Key Pair) | t3.small minimo |
| 1 | Conectar via SSH (Instance Connect ou terminal local) | — |
| 2 | Instalar Git + Docker (yum) | systemctl enable docker |
| 3 | Instalar Docker Compose v2 (plugin) | Sem hifen: `docker compose` |
| 4 | Clonar repositorio do GitHub | Verificar docker-compose.yml |
| 5 | Build e subir containers (`docker compose up -d --build`) | 3 containers: api, postgres, rabbitmq |
| 6 | Validacao final (Swagger + RabbitMQ UI) | Sem firewall interno no Linux |

---

## Comparativo Rapido: Linux vs Windows

| Criterio | Amazon Linux 2023 | Windows Server 2022/2025 |
|----------|-------------------|--------------------------|
| Docker | Nativo no Kernel | Requer WSL2/Hyper-V |
| Nested Virtualization | Nao necessario | Bloqueado em t2/t3 |
| Acesso Remoto | SSH (porta 22) | RDP (porta 3389) |
| Firewall Interno | Nao requer config | Windows Defender — configurar |
| RAM base do SO | ~300 MB | ~1.2 GB |
| Recomendacao | **PRODUCAO** | Referencia / troubleshoot |
