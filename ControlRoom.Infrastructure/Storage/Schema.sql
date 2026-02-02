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

-- =====================================================
-- OBSERVABILITY & SELF-HEALING (Phase 3)
-- =====================================================

-- Metrics: Time-series data points
CREATE TABLE IF NOT EXISTS metrics (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  name TEXT NOT NULL,
  type INTEGER NOT NULL,          -- MetricType
  value REAL NOT NULL,
  timestamp TEXT NOT NULL,
  tags_json TEXT NOT NULL DEFAULT '{}'
);

CREATE INDEX IF NOT EXISTS idx_metrics_name_ts ON metrics(name, timestamp DESC);
CREATE INDEX IF NOT EXISTS idx_metrics_timestamp ON metrics(timestamp DESC);

-- Metric aggregates: Pre-computed rollups for faster queries
CREATE TABLE IF NOT EXISTS metric_aggregates (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  name TEXT NOT NULL,
  window_start TEXT NOT NULL,
  window_end TEXT NOT NULL,
  resolution TEXT NOT NULL,       -- e.g., '1m', '5m', '1h', '1d'
  count INTEGER NOT NULL,
  min REAL NOT NULL,
  max REAL NOT NULL,
  sum REAL NOT NULL,
  avg REAL NOT NULL,
  p50 REAL NOT NULL,
  p90 REAL NOT NULL,
  p99 REAL NOT NULL,
  variance REAL NOT NULL DEFAULT 0,
  tags_json TEXT NOT NULL DEFAULT '{}'
);

CREATE INDEX IF NOT EXISTS idx_metric_aggregates_name_window ON metric_aggregates(name, window_start DESC);
CREATE UNIQUE INDEX IF NOT EXISTS idx_metric_aggregates_unique ON metric_aggregates(name, window_start, resolution, tags_json);

-- Alert rules: Define conditions that trigger alerts
CREATE TABLE IF NOT EXISTS alert_rules (
  rule_id TEXT PRIMARY KEY,
  name TEXT NOT NULL,
  description TEXT NOT NULL DEFAULT '',
  metric_name TEXT NOT NULL,
  condition INTEGER NOT NULL,     -- AlertCondition
  threshold REAL NOT NULL,
  evaluation_window_ms INTEGER NOT NULL,
  cooldown_ms INTEGER NOT NULL,
  severity INTEGER NOT NULL,      -- AlertSeverity
  is_enabled INTEGER NOT NULL DEFAULT 1,
  tags_json TEXT NOT NULL DEFAULT '{}',
  actions_json TEXT NOT NULL DEFAULT '[]',
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_alert_rules_metric ON alert_rules(metric_name);
CREATE INDEX IF NOT EXISTS idx_alert_rules_enabled ON alert_rules(is_enabled);

-- Alerts: Fired alert instances
CREATE TABLE IF NOT EXISTS alerts (
  alert_id TEXT PRIMARY KEY,
  rule_id TEXT NOT NULL,
  rule_name TEXT NOT NULL,
  severity INTEGER NOT NULL,
  message TEXT NOT NULL,
  current_value REAL NOT NULL,
  threshold REAL NOT NULL,
  fired_at TEXT NOT NULL,
  resolved_at TEXT NULL,
  status INTEGER NOT NULL,        -- AlertStatus
  tags_json TEXT NOT NULL DEFAULT '{}',
  FOREIGN KEY (rule_id) REFERENCES alert_rules(rule_id)
);

CREATE INDEX IF NOT EXISTS idx_alerts_rule_id ON alerts(rule_id);
CREATE INDEX IF NOT EXISTS idx_alerts_status ON alerts(status);
CREATE INDEX IF NOT EXISTS idx_alerts_fired_at ON alerts(fired_at DESC);
CREATE INDEX IF NOT EXISTS idx_alerts_severity ON alerts(severity);

-- Health checks: Service/endpoint health monitoring
CREATE TABLE IF NOT EXISTS health_checks (
  check_id TEXT PRIMARY KEY,
  name TEXT NOT NULL,
  description TEXT NOT NULL DEFAULT '',
  type INTEGER NOT NULL,          -- HealthCheckType
  config_json TEXT NOT NULL,
  interval_ms INTEGER NOT NULL,
  timeout_ms INTEGER NOT NULL,
  is_enabled INTEGER NOT NULL DEFAULT 1,
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_health_checks_enabled ON health_checks(is_enabled);

-- Health check results: History of check executions
CREATE TABLE IF NOT EXISTS health_check_results (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  check_id TEXT NOT NULL,
  check_name TEXT NOT NULL,
  status INTEGER NOT NULL,        -- HealthStatus
  checked_at TEXT NOT NULL,
  response_time_ms INTEGER NOT NULL,
  message TEXT NULL,
  details_json TEXT NULL,
  FOREIGN KEY (check_id) REFERENCES health_checks(check_id)
);

CREATE INDEX IF NOT EXISTS idx_health_check_results_check_id ON health_check_results(check_id, checked_at DESC);
CREATE INDEX IF NOT EXISTS idx_health_check_results_checked_at ON health_check_results(checked_at DESC);

-- Self-healing rules: Automated remediation
CREATE TABLE IF NOT EXISTS self_healing_rules (
  rule_id TEXT PRIMARY KEY,
  name TEXT NOT NULL,
  description TEXT NOT NULL DEFAULT '',
  trigger_condition TEXT NOT NULL,  -- Expression to evaluate
  remediation_runbook_id TEXT NOT NULL,
  max_executions_per_hour INTEGER NOT NULL DEFAULT 3,
  cooldown_ms INTEGER NOT NULL,
  requires_approval INTEGER NOT NULL DEFAULT 0,
  is_enabled INTEGER NOT NULL DEFAULT 1,
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL,
  FOREIGN KEY (remediation_runbook_id) REFERENCES runbooks(runbook_id)
);

CREATE INDEX IF NOT EXISTS idx_self_healing_rules_enabled ON self_healing_rules(is_enabled);

-- Self-healing executions: Record of remediation attempts
CREATE TABLE IF NOT EXISTS self_healing_executions (
  execution_id TEXT PRIMARY KEY,
  rule_id TEXT NOT NULL,
  triggering_alert_id TEXT NULL,
  remediation_execution_id TEXT NULL,
  status INTEGER NOT NULL,        -- SelfHealingStatus
  started_at TEXT NOT NULL,
  completed_at TEXT NULL,
  result TEXT NULL,
  FOREIGN KEY (rule_id) REFERENCES self_healing_rules(rule_id),
  FOREIGN KEY (triggering_alert_id) REFERENCES alerts(alert_id),
  FOREIGN KEY (remediation_execution_id) REFERENCES runbook_executions(execution_id)
);

CREATE INDEX IF NOT EXISTS idx_self_healing_executions_rule_id ON self_healing_executions(rule_id);
CREATE INDEX IF NOT EXISTS idx_self_healing_executions_started_at ON self_healing_executions(started_at DESC);
