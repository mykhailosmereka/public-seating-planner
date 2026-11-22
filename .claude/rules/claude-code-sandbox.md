# Claude Code sandbox gotchas
- `.claude/settings.json` and `settings.local.json` are edit-denied. For a new outbound domain (`sandbox.network.allowedDomains` in `.claude/settings.json`) or any other settings change, ask the user.
- On WSL the sandbox shell may resolve `npm`/`node` to the Windows install, which fails inside the sandbox ("WSL 1 is not supported"). Load the Linux Node from nvm in the same command: `. ~/.nvm/nvm.sh && npm run build`.
- Run `dotnet build` with `-m:1` inside the sandbox (MSBuild worker nodes can't create their pipes in the read-only `/tmp`). Don't add `-m:1` to repo files.
- `dotnet test`, Testcontainers and `dotnet format` don't work in the sandbox (`SocketException (13): Permission denied`). Never exclude them from it. For tests, build with `-m:1` and run `tools/run-tests-sandboxed.sh` (setup: `docs/running-tests.md`). For formatting, ask the user to run `dotnet format`.
- The `csharp-lsp` and `typescript-lsp` plugins need `csharp-ls` and `typescript-language-server` installed separately, at pinned versions (`docs/claude-code-lsp.md`). If LSP calls fail with a missing server, point the user there.
- 0-byte read-only dotfiles in the repo root (`.gitmodules`, `.gitconfig`, `.bashrc`, `.idea`, `.vscode`, `.mcp.json`, ...) are sandbox masks and show as untracked. Never stage or commit them.
