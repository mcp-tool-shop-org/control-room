import type { SiteConfig } from '@mcptoolshop/site-theme';

export const config: SiteConfig = {
  title: 'Control Room',
  description: 'Local-first desktop app for managing and executing scripts with full observability',
  logoBadge: 'CR',
  brandName: 'Control Room',
  repoUrl: 'https://github.com/mcp-tool-shop-org/control-room',
  footerText: 'MIT Licensed — built by <a href="https://github.com/mcp-tool-shop-org" style="color:var(--color-muted);text-decoration:underline">mcp-tool-shop-org</a>',

  hero: {
    badge: '.NET MAUI',
    headline: 'Control Room,',
    headlineAccent: 'scripts that explain themselves.',
    description: 'Turns your scripts into observable, repeatable operations. Evidence-grade logging, failure fingerprinting, run profiles, and ZIP export \u2014 all local, all keyboard-driven.',
    primaryCta: { href: '#quick-start', label: 'Get started' },
    secondaryCta: { href: '#features', label: 'Learn more' },
    previews: [
      { label: 'Build', code: 'dotnet build' },
      { label: 'Run', code: 'dotnet run --project ControlRoom.App' },
      { label: 'Profile', code: 'Smoke \u2192 --epochs=1 --subset=100' },
    ],
  },

  sections: [
    {
      kind: 'features',
      id: 'features',
      title: 'Why Control Room?',
      subtitle: 'Stop hoping scripts work. Know they did.',
      features: [
        { title: 'Evidence-Grade Runs', desc: 'Every execution is logged with stdout/stderr, exit codes, timing, and artifacts. Nothing is lost.' },
        { title: 'Failure Fingerprinting', desc: 'Recurring errors are grouped by signature and tracked across runs with first/last seen timestamps.' },
        { title: 'Run Profiles', desc: 'Define preset argument and environment combinations per script \u2014 Smoke, Full, Debug \u2014 and switch with one click.' },
        { title: 'Command Palette', desc: 'Keyboard-driven execution with fuzzy search. Each profile appears as a separate action.' },
        { title: 'ZIP Export', desc: 'Export any run as a ZIP with metadata, stdout/stderr, event stream, and artifacts. Share or archive with confidence.' },
        { title: 'Local-First', desc: 'SQLite in WAL mode. No cloud, no accounts, no telemetry. Your runs stay on your machine.' },
      ],
    },
    {
      kind: 'code-cards',
      id: 'quick-start',
      title: 'Quick Start',
      cards: [
        {
          title: 'Build & run',
          code: '# Prerequisites: .NET 10 SDK, Windows 10/11\n\ndotnet restore\ndotnet build\ndotnet run --project ControlRoom.App',
        },
        {
          title: 'Define profiles',
          code: 'Thing: "train-model"\n\u251c\u2500\u2500 Default          (no args)\n\u251c\u2500\u2500 Smoke            --epochs=1 --subset=100\n\u251c\u2500\u2500 Full             --epochs=50 --wandb\n\u2514\u2500\u2500 Debug            --verbose --no-cache  DEBUG=1',
        },
      ],
    },
    {
      kind: 'data-table',
      id: 'observability',
      title: 'Observability',
      subtitle: 'What gets captured for every run.',
      columns: ['Data', 'Description'],
      rows: [
        ['stdout / stderr', 'Complete output streams, captured in real time'],
        ['Exit code', 'Process exit code with success/failure classification'],
        ['Timing', 'Start time, end time, and duration'],
        ['Profile', 'Which argument/environment preset was used'],
        ['Artifacts', 'Any collected output files'],
        ['Failure fingerprint', 'Error signature for grouping recurring issues'],
      ],
    },
    {
      kind: 'data-table',
      id: 'export',
      title: 'ZIP Export',
      subtitle: 'Share or archive any run.',
      columns: ['File', 'Contents'],
      rows: [
        ['run-info.json', 'Full metadata \u2014 args, env, timing, profile used'],
        ['stdout.txt', 'Complete standard output'],
        ['stderr.txt', 'Complete standard error'],
        ['events.jsonl', 'Machine-readable event stream'],
        ['artifacts/', 'Any collected output artifacts'],
      ],
    },
    {
      kind: 'data-table',
      id: 'tech',
      title: 'Tech Stack',
      columns: ['Layer', 'Technology'],
      rows: [
        ['UI Framework', '.NET MAUI (Windows focus)'],
        ['Runtime', '.NET 10'],
        ['MVVM', 'CommunityToolkit.Mvvm with source generators'],
        ['Storage', 'SQLite (WAL mode) \u2014 local-first persistence'],
        ['Architecture', 'Clean Architecture \u2014 Domain, Application, Infrastructure, App'],
      ],
    },
  ],
};
