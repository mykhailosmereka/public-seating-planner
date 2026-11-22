# Language servers for Claude Code

The project enables two Claude Code plugins in `.claude/settings.json`: `csharp-lsp` and `typescript-lsp`. They let Claude Code use language servers for go-to-definition, find-references, symbol search and diagnostics after edits, instead of relying only on text search and builds.

The plugins only tell Claude Code which program to start (`csharp-ls` for `.cs` files, `typescript-language-server` for `.ts`/`.tsx`). They do **not** install those programs. Until you install them yourself, both plugins silently do nothing.

Install the servers on every OS you run Claude Code from: in WSL for a WSL session, on Windows for a Windows session.

## Prerequisites

- .NET 10 SDK (the same one you build the backend with)
- Node.js with npm. On WSL, use a Linux Node (for example via [nvm](https://github.com/nvm-sh/nvm)), not the Windows install under `/mnt/c/Program Files/nodejs`.

## 1. C#: install `csharp-ls`

```bash
dotnet tool install --global csharp-ls --version 0.28.0
csharp-ls --version
```

Pin the version. `csharp-ls` 0.29.0 and later target .NET 11, and the .NET 10 SDK refuses them with a misleading error:

```
The settings file in the tool's NuGet package is invalid: Settings file 'DotnetToolSettings.xml' was not found in the package.
```

Versions 0.22.0–0.28.0 target .NET 10. For the same reason, don't run a plain `dotnet tool update --global csharp-ls`. If the tool is already installed, use `dotnet tool update --global csharp-ls --version 0.28.0`. Move to a newer version once the repo uses .NET 11.

`dotnet tool install --global` puts the tool in `~/.dotnet/tools` (`%USERPROFILE%\.dotnet\tools` on Windows). That folder must be on your `PATH`; the .NET SDK installer normally adds it.

## 2. TypeScript: install `typescript-language-server` and TypeScript 5.x

```bash
npm install --global typescript-language-server typescript@5.9.3
typescript-language-server --version
```

Why the global `typescript@5.9.3`:

- Claude Code starts the server from the repo root, which has no `node_modules`. The frontend's own TypeScript in `src/frontend/node_modules` is therefore not found, and the server falls back to a global TypeScript.
- TypeScript 7 (the native Go rewrite) no longer ships `lib/tsserver.js`, which `typescript-language-server` requires. With a global TypeScript 7 the server fails with `Could not find a valid TypeScript installation`.

Keep the global version in line with `typescript` in `src/frontend/package.json`.

## 3. Restart Claude Code and check

Claude Code looks for language servers only at startup, so restart it after installing. Then ask it to list the symbols in a C# file and a TypeScript file, or to find the references to a class or function.

Expect a short warm-up. `csharp-ls` loads the whole solution, which takes a while on `/mnt/c` and uses a few hundred MB of memory. `typescript-language-server` may return incomplete results for cross-file queries until it has loaded the frontend project.

## Opting out

If you don't want the language servers, disable the plugins for yourself in `.claude/settings.local.json` instead of changing the shared settings:

```json
{
  "enabledPlugins": {
    "csharp-lsp@claude-plugins-official": false,
    "typescript-lsp@claude-plugins-official": false
  }
}
```
