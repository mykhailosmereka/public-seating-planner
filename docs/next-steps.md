# Next Steps

Follow-ups identified while setting up `CLAUDE.md`.

## Claude Code setup
- [ ] Install the skill-creator plugin: `/plugin install skill-creator@claude-plugins-official`, then `/skill-creator <skill-name>` to create or refine skills.
- [ ] Browse official plugins with `/plugin` (they bundle skills, agents, hooks and MCP servers).
- [ ] Once React code exists, install UI helpers:
  - `/plugin install frontend-design@claude-plugins-official` (design principles and component patterns)
  - `/plugin install playwright@claude-plugins-official` (real browser, screenshots, visual bug fixing)
- [ ] Re-run `/init` after the first projects are added to refresh `CLAUDE.md`.

## Repository setup
- [ ] Add a test project early so Claude can verify its own changes.
- [ ] Set up linting: `.editorconfig` / Roslyn analyzers for .NET, ESLint for React.
- [ ] Check that `gh` (GitHub CLI) is installed and the GitHub remote is configured.
- [ ] Enforce branch policy with GitHub branch protection (no direct commits to `main` or `release/*`); `CLAUDE.md` only documents it.
- [ ] Decide the target branch for `hotfix/*` (`main` or `release/*`) and document it.

## CLAUDE.md gaps
- [ ] Add the exact Cake entry point (e.g. `dotnet cake build.cake`) once the script exists.
- [ ] Document the Anthropic API key variable name and how it is supplied (Aspire AppHost parameter, user secrets or AWS).
- [ ] Note that under Aspire, connection strings and ports are mostly injected by the AppHost.

## Settings and sandbox
- [ ] `.claude/settings.json` allows `npm run test` but not `npm test`; add the short form if wanted.
- [ ] Clean up stale comments in `.devcontainer/` files (leftover "example-…" and `../3.7-sandbox/` references).
