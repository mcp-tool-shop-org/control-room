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

**Une application de bureau conçue pour fonctionner localement, permettant de gérer et d'exécuter des scripts, des serveurs et des tâches, avec une visibilité complète.**

## Qu'est-ce que Control Room ?

Control Room transforme vos scripts en opérations observables et reproductibles. Au lieu d'exécuter `python train.py --config=prod` dans un terminal et d'espérer le meilleur, vous obtenez :

- **Exécutions avec preuves irréfutables** — Chaque exécution est enregistrée avec les flux standard de sortie/erreur, les codes de sortie, les informations de temps et les artefacts.
- **Identification des erreurs récurrentes** — Les erreurs récurrentes sont regroupées et suivies à travers les exécutions.
- **Profils** — Définissez des combinaisons prédéfinies d'arguments/d'environnement (Test rapide, Complet, Débogage) pour chaque script.
- **Palette de commandes** — Exécution via le clavier avec recherche floue.

## Fonctionnalités

### Profils (Nouveau !)

Définissez plusieurs configurations d'exécution pour chaque script :

```
Thing: "train-model"
├── Default          (no args)
├── Smoke            --epochs=1 --subset=100
├── Full             --epochs=50 --wandb
└── Debug            --verbose --no-cache  DEBUG=1
```

La palette de commandes affiche chaque profil comme une action distincte. La tentative de relance d'une exécution échouée utilise le même profil qui a échoué.

### Groupes d'erreurs

Les erreurs sont identifiées par leur signature. La page "Erreurs" affiche les problèmes récurrents regroupés par signature, avec le nombre d'occurrences et les horodatages de la première et de la dernière occurrence.

### Chronologie

Visualisez toutes les exécutions de manière chronologique. Filtrez par signature d'erreur pour voir toutes les occurrences d'une erreur spécifique.

### Exportation ZIP

Exportez n'importe quelle exécution au format ZIP, contenant :
- `run-info.json` — Métadonnées complètes (arguments, environnement, temps d'exécution, profil utilisé).
- `stdout.txt` / `stderr.txt` — Sortie complète.
- `events.jsonl` — Flux d'événements lisible par machine.
- `artifacts/` — Tous les artefacts collectés.

## Technologies utilisées

- **.NET MAUI** — Interface utilisateur de bureau multiplateforme (principalement pour Windows).
- **SQLite (mode WAL)** — Persistance locale.
- **CommunityToolkit.Mvvm** — MVVM avec générateurs de code source.

## Premiers pas

### Prérequis

- SDK .NET 10
- Windows 10/11

### Compilation

```bash
dotnet restore
dotnet build
```

### Exécution

```bash
dotnet run --project ControlRoom.App
```

## Structure du projet

```
ControlRoom/
├── ControlRoom.Domain/        # Domain models (Thing, Run, ThingConfig, etc.)
├── ControlRoom.Application/   # Use cases (RunLocalScript, etc.)
├── ControlRoom.Infrastructure/ # SQLite storage, queries
└── ControlRoom.App/           # MAUI UI layer
```

## Licence

MIT — voir [LICENSE](LICENSE)

## Contributions

Les contributions sont les bienvenues ! Veuillez ouvrir un ticket au préalable pour discuter des modifications proposées.
