# AGENTS.md — Multi-Agent Engineering Architecture

This repository uses a structured multi-agent coordination protocol. Any AI assistant or harness (Antigravity, Claude Code, Cursor, Windsurf, or custom scripts) operating in this workspace must follow this topology and workflow.

---

## Team Topology (4 Specialists + 1 Orchestrator)

| Role | Definition Path | Scope / Working Directory | Core Stack |
| :--- | :--- | :--- | :--- |
| **0. Orchestrator** | `.agents/agents/orchestrator.md` | Workspace Root | Dependency graph, handoff verification, phase gating |
| **1. Cloud & API Architect** | `.agents/agents/infra-syndicate-agent.md` | `infra/terraform/`, `backend/.../deployment_resources.json` | Terraform, AWS Syndicate, DynamoDB, API Gateway |
| **2. Backend Engineer** | `.agents/agents/backend-dev-agent.md` | `backend/restaurant-backend-app/` | .NET 8 (C#) Lambdas, xUnit/NUnit, Syndicate Handlers |
| **3. Frontend Engineer** | `.agents/agents/frontend-dev-agent.md` | `frontend/` | Angular 18+, TypeScript, Angular Signals, Jasmine/Karma |
| **4. QA & Reviewer** | `.agents/agents/qa-review-agent.md` | `aqa/`, CI/CD, Git Diffs | Reqnroll (C# BDD), ESLint, dotnet test, Code Review |

---

## Execution Graph & Order of Operations

Agents MUST execute in strict dependency order:

```
[Phase 1: Contract & Cloud Resources]
   infra-syndicate-agent
   ├── Defines DynamoDB schema / GSI
   ├── Adds API Gateway endpoints & routes
   └── Outputs: API Spec & DTO contract (Swagger / JSON schema)
              │
              ├───────────────────────────────┐
              ▼                               ▼
   [Phase 2: Backend Implementation]    [Phase 3: Frontend Implementation]
   backend-dev-agent                    frontend-dev-agent
   ├── Implements C# Lambda handlers    ├── Implements Angular components & signals
   ├── Writes & passes unit tests       ├── Integrates API service
   └── Verifies Syndicate config        └── Passes Karma / Jasmine tests
              │                               │
              └───────────────┬───────────────┘
                              ▼
                     [Phase 4: Quality Gate]
                     qa-review-agent
                     ├── Verifies git diff & boundary integrity
                     ├── Runs Reqnroll AQA / E2E suites
                     └── Delivers unbiased Code Review verdict
```

---

## Handoff & Boundary Rules

1. **Boundary Isolation:**
   - No agent may modify files outside its declared scope unless explicitly directed by the Orchestrator.
2. **Contract-First Principle:**
   - The `infra-syndicate-agent` must establish or update the API contract (in `deployment_resources.json` and `swagger_src/` or shared schema) before `backend-dev-agent` and `frontend-dev-agent` write application code.
3. **Self-Testing Obligation:**
   - Developers (`backend-dev-agent` and `frontend-dev-agent`) must write and run their own unit tests before marking a phase complete.
4. **Independent Gatekeeping:**
   - The `qa-review-agent` has veto power: code with regressions, missing tests, or violated project standards is rejected back to the responsible agent.

