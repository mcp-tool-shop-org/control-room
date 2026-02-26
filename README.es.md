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

**Una aplicación de escritorio centrada en el usuario local para gestionar y ejecutar scripts, servidores y tareas, con total visibilidad.**

## ¿Qué es Control Room?

Control Room transforma sus scripts en operaciones observables y repetibles. En lugar de ejecutar `python train.py --config=prod` en una terminal y esperar lo mejor, obtiene:

- **Ejecuciones con registro detallado** — Cada ejecución se registra con la salida estándar/error estándar, códigos de salida, tiempos de ejecución y archivos adjuntos.
- **Identificación de fallos** — Los errores recurrentes se agrupan y se rastrean en todas las ejecuciones.
- **Perfiles** — Defina combinaciones predefinidas de argumentos/entornos (Prueba rápida, Completa, Depuración) para cada script.
- **Paleta de comandos** — Ejecución mediante teclado con búsqueda difusa.

## Características

### Perfiles (¡Nuevo!)

Defina múltiples configuraciones de ejecución para cada script:

```
Thing: "train-model"
├── Default          (no args)
├── Smoke            --epochs=1 --subset=100
├── Full             --epochs=50 --wandb
└── Debug            --verbose --no-cache  DEBUG=1
```

La paleta de comandos muestra cada perfil como una acción separada. Reintentar una ejecución fallida utiliza el mismo perfil que falló.

### Grupos de fallos

Los fallos se identifican mediante la firma del error. La página de "Fallos" muestra los problemas recurrentes agrupados por firma, con el número de repeticiones y las marcas de tiempo de la primera y última aparición.

### Línea de tiempo

Visualice todas las ejecuciones cronológicamente. Filtre por la firma del fallo para ver cada aparición de un error específico.

### Exportación ZIP

Exporte cualquier ejecución como un archivo ZIP que contenga:
- `run-info.json` — Metadatos completos (argumentos, entorno, tiempos de ejecución, perfil utilizado).
- `stdout.txt` / `stderr.txt` — Salida completa.
- `events.jsonl` — Flujo de eventos legible por máquina.
- `artifacts/` — Cualquier archivo adjunto recopilado.

## Tecnologías utilizadas

- **.NET MAUI** — Interfaz de usuario de escritorio multiplataforma (centrado en Windows).
- **SQLite (modo WAL)** — Persistencia centrada en el usuario local.
- **CommunityToolkit.Mvvm** — MVVM con generadores de código.

## Cómo empezar

### Requisitos previos

- .NET 10 SDK
- Windows 10/11

### Compilación

```bash
dotnet restore
dotnet build
```

### Ejecución

```bash
dotnet run --project ControlRoom.App
```

## Estructura del proyecto

```
ControlRoom/
├── ControlRoom.Domain/        # Domain models (Thing, Run, ThingConfig, etc.)
├── ControlRoom.Application/   # Use cases (RunLocalScript, etc.)
├── ControlRoom.Infrastructure/ # SQLite storage, queries
└── ControlRoom.App/           # MAUI UI layer
```

## Licencia

MIT — consulte [LICENSE](LICENSE)

## Contribuciones

¡Las contribuciones son bienvenidas! Abra primero un problema para discutir los cambios propuestos.
