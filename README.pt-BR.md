<p align="center">
  <a href="README.ja.md">日本語</a> | <a href="README.zh.md">中文</a> | <a href="README.es.md">Español</a> | <a href="README.fr.md">Français</a> | <a href="README.hi.md">हिन्दी</a> | <a href="README.it.md">Italiano</a> | <a href="README.pt-BR.md">Português (BR)</a>
</p>

<p align="center">
  <img src="https://raw.githubusercontent.com/mcp-tool-shop-org/control-room/main/assets/logo-control-room.png" alt="Control Room" width="400">
</p>

<p align="center">
  <a href="https://github.com/mcp-tool-shop-org/control-room/actions/workflows/ci.yml"><img src="https://github.com/mcp-tool-shop-org/control-room/actions/workflows/ci.yml/badge.svg" alt="CI"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-yellow" alt="MIT License"></a>
  <a href="https://mcp-tool-shop-org.github.io/control-room/"><img src="https://img.shields.io/badge/Landing_Page-live-blue" alt="Landing Page"></a>
</p>

**Um aplicativo de desktop, com foco na experiência local, para gerenciar e executar scripts, servidores e tarefas, com total visibilidade.**

## O que é o Control Room?

O Control Room transforma seus scripts em operações observáveis e repetíveis. Em vez de executar `python train.py --config=prod` em um terminal e torcer para que tudo dê certo, você obtém:

- **Execuções com informações detalhadas** — Cada execução é registrada com saída padrão/erro padrão, códigos de saída, tempo de execução e arquivos.
- **Identificação de falhas** — Erros recorrentes são agrupados e rastreados em todas as execuções.
- **Perfis** — Defina combinações predefinidas de argumentos/ambiente (Teste rápido, Completo, Depuração) para cada script.
- **Paleta de comandos** — Execução via teclado com pesquisa aproximada.

## Recursos

### Perfis (Novo!)

Defina múltiplas configurações de execução para cada script:

```
Thing: "train-model"
├── Default          (no args)
├── Smoke            --epochs=1 --subset=100
├── Full             --epochs=50 --wandb
└── Debug            --verbose --no-cache  DEBUG=1
```

A paleta de comandos mostra cada perfil como uma ação separada. Tentar novamente uma execução com falha usa o mesmo perfil que falhou.

### Grupos de Falhas

As falhas são identificadas por uma assinatura de erro. A página de Falhas mostra problemas recorrentes agrupados por essa assinatura, com a contagem de ocorrências e os carimbos de data/hora da primeira e última ocorrência.

### Linha do Tempo

Visualize todas as execuções em ordem cronológica. Filtre por assinatura de falha para ver todas as ocorrências de um erro específico.

### Exportação em ZIP

Exporte qualquer execução como um arquivo ZIP contendo:
- `run-info.json` — Metadados completos (argumentos, ambiente, tempo de execução, perfil usado).
- `stdout.txt` / `stderr.txt` — Saída completa.
- `events.jsonl` — Fluxo de eventos legível por máquina.
- `artifacts/` — Quaisquer arquivos coletados.

## Tecnologias Utilizadas

- **.NET MAUI** — Interface de usuário de desktop multiplataforma (foco em Windows).
- **SQLite (modo WAL)** — Persistência com foco na experiência local.
- **CommunityToolkit.Mvvm** — MVVM com geradores de código.

## Como Começar

### Pré-requisitos

- SDK .NET 10
- Windows 10/11

### Construção

```bash
dotnet restore
dotnet build
```

### Execução

```bash
dotnet run --project ControlRoom.App
```

## Estrutura do Projeto

```
ControlRoom/
├── ControlRoom.Domain/        # Domain models (Thing, Run, ThingConfig, etc.)
├── ControlRoom.Application/   # Use cases (RunLocalScript, etc.)
├── ControlRoom.Infrastructure/ # SQLite storage, queries
└── ControlRoom.App/           # MAUI UI layer
```

## Licença

MIT — veja [LICENSE](LICENSE)

## Contribuições

Contribuições são bem-vindas! Por favor, abra um problema primeiro para discutir as alterações propostas.
