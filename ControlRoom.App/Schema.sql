PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS things (
  thing_id TEXT PRIMARY KEY,
  name TEXT NOT NULL,
  kind INTEGER NOT NULL,
  config_json TEXT NOT NULL,
  created_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS runs (
  run_id TEXT PRIMARY KEY,
  thing_id TEXT NOT NULL,
  started_at TEXT NOT NULL,
  ended_at TEXT NULL,
  status INTEGER NOT NULL,
  exit_code INTEGER NULL,
  summary TEXT NULL,
  FOREIGN KEY (thing_id) REFERENCES things(thing_id)
);

-- Event log backbone
CREATE TABLE IF NOT EXISTS run_events (
  seq INTEGER PRIMARY KEY AUTOINCREMENT,
  run_id TEXT NOT NULL,
  at TEXT NOT NULL,
  kind INTEGER NOT NULL,
  payload_json TEXT NOT NULL,
  FOREIGN KEY (run_id) REFERENCES runs(run_id)
);

CREATE INDEX IF NOT EXISTS idx_run_events_run_id_seq ON run_events(run_id, seq);
CREATE INDEX IF NOT EXISTS idx_runs_started_at ON runs(started_at DESC);

CREATE TABLE IF NOT EXISTS artifacts (
  artifact_id TEXT PRIMARY KEY,
  run_id TEXT NOT NULL,
  media_type TEXT NOT NULL,
  locator TEXT NOT NULL,
  sha256_hex TEXT NULL,
  created_at TEXT NOT NULL,
  FOREIGN KEY (run_id) REFERENCES runs(run_id)
);

-- App settings (key-value store)
CREATE TABLE IF NOT EXISTS settings (
  key TEXT PRIMARY KEY,
  value_json TEXT NOT NULL,
  updated_at TEXT NOT NULL
);

-- =====================================================
-- RUNBOOK AUTOMATION ENGINE (Phase 2)
-- =====================================================

-- Runbooks: Multi-step workflow definitions
CREATE TABLE IF NOT EXISTS runbooks (
  runbook_id TEXT PRIMARY KEY,
  name TEXT NOT NULL,
  description TEXT NOT NULL DEFAULT '',
  config_json TEXT NOT NULL,  -- RunbookConfig serialized
  is_enabled INTEGER NOT NULL DEFAULT 1,
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL,
  version INTEGER NOT NULL DEFAULT 1
);

CREATE INDEX IF NOT EXISTS idx_runbooks_name ON runbooks(name);
CREATE INDEX IF NOT EXISTS idx_runbooks_enabled ON runbooks(is_enabled);

-- Runbook executions: Each time a runbook runs
CREATE TABLE IF NOT EXISTS runbook_executions (
  execution_id TEXT PRIMARY KEY,
  runbook_id TEXT NOT NULL,
  status INTEGER NOT NULL,  -- RunbookExecutionStatus
  started_at TEXT NOT NULL,
  ended_at TEXT NULL,
  trigger_info TEXT NULL,   -- JSON: what triggered this execution
  error_message TEXT NULL,
  FOREIGN KEY (runbook_id) REFERENCES runbooks(runbook_id)
);

CREATE INDEX IF NOT EXISTS idx_runbook_executions_runbook_id ON runbook_executions(runbook_id);
CREATE INDEX IF NOT EXISTS idx_runbook_executions_started_at ON runbook_executions(started_at DESC);
CREATE INDEX IF NOT EXISTS idx_runbook_executions_status ON runbook_executions(status);

-- Step executions: Each step within a runbook execution
CREATE TABLE IF NOT EXISTS step_executions (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  execution_id TEXT NOT NULL,
  step_id TEXT NOT NULL,
  step_name TEXT NOT NULL,
  run_id TEXT NULL,           -- Links to runs table if a script was executed
  status INTEGER NOT NULL,    -- StepExecutionStatus
  started_at TEXT NULL,
  ended_at TEXT NULL,
  attempt INTEGER NOT NULL DEFAULT 1,
  error_message TEXT NULL,
  output TEXT NULL,           -- Captured output/result
  FOREIGN KEY (execution_id) REFERENCES runbook_executions(execution_id),
  FOREIGN KEY (run_id) REFERENCES runs(run_id)
);

CREATE INDEX IF NOT EXISTS idx_step_executions_execution_id ON step_executions(execution_id);
CREATE INDEX IF NOT EXISTS idx_step_executions_step_id ON step_executions(step_id);

-- Trigger history: When triggers fire
CREATE TABLE IF NOT EXISTS trigger_history (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  runbook_id TEXT NOT NULL,
  trigger_type INTEGER NOT NULL,  -- TriggerType
  fired_at TEXT NOT NULL,
  execution_id TEXT NULL,         -- Resulting execution (if created)
  payload_json TEXT NULL,         -- Trigger-specific data (webhook body, file changes, etc.)
  FOREIGN KEY (runbook_id) REFERENCES runbooks(runbook_id),
  FOREIGN KEY (execution_id) REFERENCES runbook_executions(execution_id)
);

CREATE INDEX IF NOT EXISTS idx_trigger_history_runbook_id ON trigger_history(runbook_id);
CREATE INDEX IF NOT EXISTS idx_trigger_history_fired_at ON trigger_history(fired_at DESC);
