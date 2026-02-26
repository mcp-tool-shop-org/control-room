<p align="center">
  <a href="README.ja.md">日本語</a> | <a href="README.zh.md">中文</a> | <a href="README.es.md">Español</a> | <a href="README.fr.md">Français</a> | <a href="README.hi.md">हिन्दी</a> | <a href="README.it.md">Italiano</a> | <a href="README.pt-BR.md">Português (BR)</a>
</p>

<p align="center">
  
            <img src="https://raw.githubusercontent.com/mcp-tool-shop-org/brand/main/logos/control-room/readme.png"
           alt="Control Room" width="400">
</p>

<p align="center">
  <a href="https://github.com/mcp-tool-shop-org/control-room/actions/workflows/ci.yml"><img src="https://github.com/mcp-tool-shop-org/control-room/actions/workflows/ci.yml/badge.svg" alt="CI"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-yellow" alt="MIT License"></a>
  <a href="https://mcp-tool-shop-org.github.io/control-room/"><img src="https://img.shields.io/badge/Landing_Page-live-blue" alt="Landing Page"></a>
</p>

**完全な可視性を持つ、スクリプト、サーバー、タスクの管理と実行を行うための、ローカル環境に最適化されたデスクトップアプリケーションです。**

## Control Roomとは？

Control Roomは、スクリプトを監視可能で、再現可能な操作に変換します。ターミナルで`python train.py --config=prod`を実行し、結果を祈る代わりに、以下の機能が利用できます。

- **証拠レベルの実行記録**：すべての実行は、標準出力/標準エラー出力、終了コード、実行時間、および関連ファイルを記録します。
- **エラーのフィンガープリント**：再発するエラーはグループ化され、実行全体で追跡されます。
- **プロファイル**：スクリプトごとに、あらかじめ定義された引数/環境の組み合わせ（テスト、フル、デバッグなど）を設定できます。
- **コマンドパレット**：キーボード操作による実行が可能で、あいまい検索も利用できます。

## 機能

### プロファイル（新機能！）

各スクリプトに対して、複数の実行構成を定義できます。

```
Thing: "train-model"
├── Default          (no args)
├── Smoke            --epochs=1 --subset=100
├── Full             --epochs=50 --wandb
└── Debug            --verbose --no-cache  DEBUG=1
```

コマンドパレットでは、各プロファイルが個別の操作として表示されます。失敗した実行を再試行する場合、同じプロファイルが使用されます。

### エラーグループ

エラーは、エラーの内容に基づいてフィンガープリントされます。エラーページでは、再発する問題がフィンガープリントごとにグループ化され、発生回数と、最初に/最後に確認されたタイムスタンプが表示されます。

### タイムライン

すべての実行を時系列で表示します。エラーフィンガープリントでフィルタリングすると、特定の特定のエラーのすべての発生を表示できます。

### ZIPエクスポート

任意の実行を、以下の内容を含むZIPファイルとしてエクスポートできます。
- `run-info.json`：完全なメタデータ（引数、環境、実行時間、使用プロファイル）
- `stdout.txt` / `stderr.txt`：完全な出力
- `events.jsonl`：機械可読なイベントストリーム
- `artifacts/`：収集された関連ファイル

## 技術スタック

- **.NET MAUI**：クロスプラットフォームのデスクトップUI（Windowsに最適化）
- **SQLite (WALモード)**：ローカル環境でのデータ永続化
- **CommunityToolkit.Mvvm**：ソースジェネレーターを使用したMVVM

## 始め方

### 前提条件

- .NET 10 SDK
- Windows 10/11

### ビルド

```bash
dotnet restore
dotnet build
```

### 実行

```bash
dotnet run --project ControlRoom.App
```

## プロジェクト構造

```
ControlRoom/
├── ControlRoom.Domain/        # Domain models (Thing, Run, ThingConfig, etc.)
├── ControlRoom.Application/   # Use cases (RunLocalScript, etc.)
├── ControlRoom.Infrastructure/ # SQLite storage, queries
└── ControlRoom.App/           # MAUI UI layer
```

## ライセンス

MIT — [LICENSE](LICENSE) を参照

## 貢献

貢献を歓迎します！変更を提案する前に、まずIssueを作成して議論してください。
