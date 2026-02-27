# Scorecard

> Score a repo before remediation. Fill this out first, then use SHIP_GATE.md to fix.

**Repo:** control-room
**Date:** 2026-02-27
**Type tags:** [desktop]

## Pre-Remediation Assessment

| Category | Score | Notes |
|----------|-------|-------|
| A. Security | 8/10 | Excellent SECURITY.md, missing README data scope |
| B. Error Handling | 8/10 | SQLite WAL, user-friendly UI errors |
| C. Operator Docs | 8/10 | Good README, CHANGELOG, LICENSE |
| D. Shipping Hygiene | 8/10 | CI, Microsoft Store, Chocolatey packaging |
| E. Identity (soft) | 10/10 | Logo, translations, landing page, metadata |
| **Overall** | **42/50** | |

## Key Gaps

1. No SHIP_GATE.md or SCORECARD.md
2. No Security & Data Scope section in README

## Remediation Priority

| Priority | Item | Estimated effort |
|----------|------|-----------------|
| 1 | Add Security & Data Scope in README | 3 min |
| 2 | Add SHIP_GATE.md + SCORECARD.md, bump to 1.0.2 | 5 min |

## Post-Remediation

| Category | Before | After |
|----------|--------|-------|
| A. Security | 8/10 | 10/10 |
| B. Error Handling | 8/10 | 10/10 |
| C. Operator Docs | 8/10 | 10/10 |
| D. Shipping Hygiene | 8/10 | 10/10 |
| E. Identity (soft) | 10/10 | 10/10 |
| **Overall** | **42/50** | **50/50** |
