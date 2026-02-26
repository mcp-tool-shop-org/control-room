<p align="center">
  <a href="README.ja.md">日本語</a> | <a href="README.zh.md">中文</a> | <a href="README.es.md">Español</a> | <a href="README.fr.md">Français</a> | <a href="README.hi.md">हिन्दी</a> | <a href="README.it.md">Italiano</a> | <a href="README.pt-BR.md">Português (BR)</a>
</p>

<p align="center">
  <img src="https://raw.githubusercontent.com/mcp-tool-shop-org/brand/main/logos/control-room/readme.png" alt="Control Room" width="400">
</p>

<p align="center">
  <a href="https://github.com/mcp-tool-shop-org/control-room/actions/workflows/ci.yml"><img src="https://github.com/mcp-tool-shop-org/control-room/actions/workflows/ci.yml/badge.svg" alt="CI"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-yellow" alt="MIT License"></a>
  <a href="https://mcp-tool-shop-org.github.io/control-room/"><img src="https://img.shields.io/badge/Landing_Page-live-blue" alt="Landing Page"></a>
</p>

**Un'applicazione desktop "local-first" per la gestione e l'esecuzione di script, server e attività, con piena visibilità e tracciabilità.**

## Cos'è Control Room?

Control Room trasforma i tuoi script in operazioni osservabili e ripetibili. Invece di eseguire `python train.py --config=prod` in un terminale e sperare nel meglio, ottieni:

- **Esecuzioni con dati affidabili** — Ogni esecuzione viene registrata con output standard/errori standard, codici di uscita, tempi di esecuzione e artefatti.
- **Identificazione degli errori** — Gli errori ricorrenti vengono raggruppati e tracciati in tutte le esecuzioni.
- **Profili** — Definisci combinazioni predefinite di argomenti/ambiente (Smoke, Full, Debug) per ogni script.
- **Palette dei comandi** — Esecuzione tramite tastiera con ricerca fuzzy.

## Funzionalità

### Profili (Nuovo!)

Definisci più configurazioni di esecuzione per ogni script:

```
Thing: "train-model"
├── Default          (no args)
├── Smoke            --epochs=1 --subset=100
├── Full             --epochs=50 --wandb
└── Debug            --verbose --no-cache  DEBUG=1
```

La palette dei comandi mostra ogni profilo come un'azione separata. Riavviare un'esecuzione fallita utilizza lo stesso profilo che ha causato l'errore.

### Gruppi di errori

Gli errori vengono identificati in base alla loro "impronta". La pagina "Errori" mostra i problemi ricorrenti raggruppati per "impronta", con il numero di occorrenze e i timestamp di prima/ultima occorrenza.

### Cronologia

Visualizza tutte le esecuzioni in ordine cronologico. Filtra per "impronta" dell'errore per vedere tutte le occorrenze di un errore specifico.

### Esportazione ZIP

Esporta qualsiasi esecuzione come file ZIP contenente:
- `run-info.json` — Metadati completi (argomenti, ambiente, tempi di esecuzione, profilo utilizzato).
- `stdout.txt` / `stderr.txt` — Output completo.
- `events.jsonl` — Flusso di eventi leggibile dalle macchine.
- `artifacts/` — Eventuali artefatti raccolti.

## Tecnologie utilizzate

- **.NET MAUI** — Interfaccia utente desktop multipiattaforma (con focus su Windows).
- **SQLite (modalità WAL)** — Persistenza "local-first".
- **CommunityToolkit.Mvvm** — MVVM con generatori di codice.

## Come iniziare

### Prerequisiti

- .NET 10 SDK
- Windows 10/11

### Compilazione

```bash
dotnet restore
dotnet build
```

### Esecuzione

```bash
dotnet run --project ControlRoom.App
```

## Struttura del progetto

```
ControlRoom/
├── ControlRoom.Domain/        # Domain models (Thing, Run, ThingConfig, etc.)
├── ControlRoom.Application/   # Use cases (RunLocalScript, etc.)
├── ControlRoom.Infrastructure/ # SQLite storage, queries
└── ControlRoom.App/           # MAUI UI layer
```

## Licenza

MIT — vedi [LICENSE](LICENSE)

## Contributi

I contributi sono benvenuti! Apri prima un problema per discutere le modifiche proposte.
