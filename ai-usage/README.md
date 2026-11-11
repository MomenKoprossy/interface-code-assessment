# AI usage

## Tools

**Claude Code** (desktop app), model **Claude Opus 5.5**. The main session acted as an orchestrator: it wrote the design
docs and the API contract, then delegated independent pieces of work to sub-agents through the Workflow tool, each in its
own git worktree. The sub-agents built the modules and the frontend, wrote the integration and architecture tests, ran two
code-review rounds with adversarial verification, and ran end-to-end browser test runs. The orchestrator re-ran the build
and the tests itself before accepting any sub-agent's work.

## What was AI-generated

**All of the code, tests, configuration and documentation.** The basis is my own **DotnetPlayGround** template: its
`BuildingBlocks` and `ServiceDefaults` were vendored, then modified by the AI. Every modification is listed in
[`backend/src/BuildingBlocks/README.md`](../backend/src/BuildingBlocks/README.md).

## Who decided what

I decided:
- to build on my own template, keeping all six building blocks in the repo and wiring only Core, Data and Web;
- to use sub-agents for building, testing and reviewing, and to repeat browser end-to-end runs until one came back clean;
- what got published.

Claude proposed, and I accepted:
- a modular monolith instead of the template's microservices, and dropping the template's Blazor frontend;
- React + TypeScript + Vite + plain SCSS, with no UI kit;
- the orchestration shape: one owner per file set, worktree isolation, and verifying review findings before fixing them.

I reviewed and ran the result. Every push runs CI.

## Agents, skills, plugins, MCP servers and instructions

| Item | Kind | Why |
|---|---|---|
| Sub-agents (Workflow tool), all Claude Opus 5.5 | agents | parallel work split by owned area (backend modules, frontend, DevOps, tests, reviewers, verifiers, browser testers), so no two agents edited the same files |
| Fresh-context reviewer agent (Agent tool) | agent | graded the repo against this brief with no context from the build session, as an independent check |
| `workflow-authoring` skill | skill | reference for writing the Workflow scripts |
| `playwright-cli` skill + Playwright CLI | skill + CLI | end-to-end runs in a real browser and the README demo recording |
| `built-in-browser` skill + Claude Browser MCP | skill + MCP | the orchestrator's first smoke test of the compose stack, and the frontend agent's visual checks |
| GitHub CLI | CLI | creating the private repo, reading CI runs and logs |
| `savvy-progress` | plugin (MCP) | sub-agents report step progress to the agents panel |
| `ponytail` | plugin (SessionStart hook) | an output style that pushes for the shortest working diff; side effect: agents wrote `ponytail:` marker comments, later reworded to plain English |
| `blast-radius` | plugin | disabled for this project |
| `agentmemory` | plugin (MCP) | failed to connect; not used |
| Claude Code memory | memory | one note: the session watchdog was already offered for this project |
| Session watchdog (scheduled task + in-session cron + heartbeat file) | my automation | resumes a long run after a usage limit, never while a Claude process is running. Its headless resume used `--dangerously-skip-permissions`, which I confirmed for this project. Removed at the end |
| Global instructions (`~/.claude/CLAUDE.md`) | custom instructions | my rules for every project: tear down only what you started, read-only recon agents, run the full suite before declaring done, offer the watchdog once per project |
| Project instructions (`.claude/CLAUDE.md`, kept local, not committed) | custom instructions | one rule set every agent followed: layer rules, money handling, CPM, no secrets, tests ship with the change |

Available but not used: Claude in Chrome, web search and fetch, codegraph, context7.

My prompts and the chat session are not included.
