# Running the tests

The backend tests run against a real PostgreSQL server rather than an in-memory fake, so that query translation, collation and constraints behave exactly as in production. By default the test fixture starts that server itself through [Testcontainers](https://dotnet.testcontainers.org/), which needs Docker. That is all you need when you run the tests yourself. Running them from a Claude Code session is different: Claude Code executes commands in a sandbox that blocks Unix sockets and connections to host ports. The sandbox therefore cannot reach Docker, and even `dotnet test` cannot start. Taking those commands out of the sandbox would let anything they run (test code, MSBuild targets, NuGet packages) act with your full user rights, and Docker access is effectively root. Instead, the sandboxed setup uses a long-running, dedicated PostgreSQL container that the sandbox reaches through its network proxy, on one allowlisted port. The tests connect as a restricted role that can only create and drop its own databases. The tests stay sandboxed and only one narrow, low-privilege channel is opened.

- **Running the tests yourself:** steps 1–2.
- **Letting Claude Code run them in its sandbox (WSL):** steps 1 and 3.

## Prerequisites

- .NET 10 SDK
- Windows with WSL 2 (Ubuntu), or Linux
- For the sandbox setup: `socat` (`sudo apt install socat`; Claude Code's Linux sandbox needs it anyway)

## 1. Install Docker

Pick **one** option. Running both makes them compete for `/var/run/docker.sock`.

### Option A: Docker Desktop (Windows)

1. Install [Docker Desktop](https://docs.docker.com/desktop/setup/install/windows-install/) and start it.
2. **Settings → General**: enable **Use the WSL 2 based engine**.
3. **Settings → Resources → WSL integration**: enable integration for your distro (list distros with `wsl -l -v` in PowerShell).
4. **Apply & restart**, then run `wsl --shutdown` in PowerShell and open a new WSL terminal.

### Option B: Docker Engine inside WSL / Linux

```bash
# Official convenience script; see https://docs.docker.com/engine/install/ubuntu/ for the apt-repository method
curl -fsSL https://get.docker.com | sudo sh
sudo usermod -aG docker "$USER"
```

Then restart WSL (`wsl --shutdown` in PowerShell) or log out and back in on Linux. If Docker Desktop is also installed, turn off its WSL integration for this distro.

### Verify

```bash
docker run --rm hello-world
```

## 2. Run the tests yourself

From the repository root:

```bash
dotnet test --solution src/SeatingPlanner.slnx
```

- The root `global.json` opts into Microsoft.Testing.Platform, which xUnit v3 requires on the .NET 10 SDK.
- Testcontainers pulls `postgres:18.3` (pinned by digest in `PostgresFixture`). The first run takes longer.
- Every test gets its own freshly migrated database, and the container is removed afterwards.
- IDE test runners (Rider, Visual Studio, VS Code C# Dev Kit) work the same way, provided Docker is reachable from wherever the IDE runs.

To run the tests against an existing server instead of Testcontainers, set `SEATINGPLANNER_TEST_POSTGRES` (see step 3).

## 3. Let Claude Code run the tests in its sandbox (WSL)

Do this once per machine. Run every command in a normal WSL terminal, **not** through Claude.

### 3.1 Start a dedicated test PostgreSQL container

```bash
docker run -d --name seatingplanner-tests-pg \
  -p 127.0.0.1:54329:5432 \
  -e POSTGRES_PASSWORD="$(openssl rand -hex 24)" \
  -v seatingplanner-tests-pg:/var/lib/postgresql \
  --restart unless-stopped \
  postgres:18.3@sha256:7e32e9833a6fb1c92c32552794cb6ed569d51b445a54907d35fc112ef39684db
```

- `127.0.0.1:` keeps the port off your network. Never publish it as `-p 54329:5432`.
- The superuser password is random and not stored anywhere. Admin access works without it via `docker exec … psql -U postgres`.
- Use the same image digest as `PostgresFixture` (`tests/SeatingPlanner.Modules.Events.Tests/Infrastructure/PostgresFixture.cs`).

### 3.2 Create the restricted test role

```bash
TEST_PW="$(openssl rand -hex 24)"; echo "$TEST_PW"   # keep this for step 3.4
docker exec seatingplanner-tests-pg psql -U postgres -c \
  "CREATE ROLE events_tests LOGIN PASSWORD '$TEST_PW' CREATEDB NOSUPERUSER NOCREATEROLE NOREPLICATION NOBYPASSRLS;"
```

Never let the tests connect as a superuser. A superuser can run shell commands inside the container (`COPY … TO PROGRAM`), and that would let a sandboxed process get around the sandbox's network restrictions.

### 3.3 Verify

```bash
docker port seatingplanner-tests-pg                              # 5432/tcp -> 127.0.0.1:54329
docker exec seatingplanner-tests-pg psql -U postgres -c '\du'    # events_tests: Create DB only
```

### 3.4 Configure Claude Code

1. `.claude/settings.json` (committed) already allows the port through the sandbox proxy:
   ```json
   "sandbox": { "network": { "allowedDomains": [ "...", "127.0.0.1:54329" ] } }
   ```
   If you choose a different port, change it there and in step 3.1.
2. Add the connection string to `.claude/settings.local.json`. This file is git-ignored; never put the connection string in the repository.
   ```json
   {
     "env": {
       "SEATINGPLANNER_TEST_POSTGRES": "Host=127.0.0.1;Port=54329;Database=postgres;Username=events_tests;Password=<TEST_PW from 3.2>"
     }
   }
   ```
   Merge this into the existing `env` block if there is one.
3. Restart Claude Code, for example with `claude --continue`. The environment is only read at startup.

### 3.5 Run

Claude runs (and you can run) the following from the repository root:

```bash
dotnet build src/SeatingPlanner.slnx -m:1
tools/run-tests-sandboxed.sh
```

- `-m:1` is needed inside the sandbox only, because MSBuild worker nodes cannot create their pipes there. Don't add it to repository files.
- `tools/run-tests-sandboxed.sh` opens a `socat` tunnel through the sandbox proxy to `127.0.0.1:54329`, runs every built test project under `tests/`, and closes the tunnel. Outside the sandbox it connects directly.
- The fixture only accepts a loopback host with a user name containing `test`, and drops every database it created when the run ends.

## Troubleshooting

| Symptom | Cause / fix |
|---|---|
| `DockerUnavailableException` | Docker is not running or not reachable from this shell (step 1). Inside the Claude sandbox this is expected; use step 3. |
| `Testing with VSTest target is no longer supported` | Run from the repository so the root `global.json` is picked up. |
| `SocketException (13): Permission denied` from `dotnet test` | You are inside the Claude sandbox. Use `tools/run-tests-sandboxed.sh` (step 3.5). |
| Build fails with "0 Error(s)" or hangs inside the sandbox | Add `-m:1` to the `dotnet build` command. |
| `Set SEATINGPLANNER_TEST_POSTGRES …` | The variable is missing: check `settings.local.json` and restart Claude Code (step 3.4). |
| `password authentication failed for user …` | Wrong password or role. Reset it: `docker exec seatingplanner-tests-pg psql -U postgres -c "ALTER ROLE events_tests PASSWORD '<new>';"` |
| `… must point to a local test server …` | The connection string uses a non-loopback host or a user without `test` in its name. |
| Connection refused / timeout through the tunnel | The container is stopped (`docker start seatingplanner-tests-pg`), or the port is missing from `allowedDomains`. |

## Security rules

- Don't add `dotnet test`, `docker` or `docker compose` to `sandbox.excludedCommands`, and don't set `allowAllUnixSockets`. Each one gives sandboxed code root-equivalent or full-user access.
- The test role must stay `NOSUPERUSER`, and the container port must stay bound to `127.0.0.1`.
- Keep the test container dedicated to tests: no real or shared data.
- Keep the connection string only in `.claude/settings.local.json`.
