# git-bot — act as the `booking-ai-analyst[bot]` account

Make individual commits, pushes, PRs, or issues under the **booking-ai-analyst**
GitHub App (the bot) — while your working repo stays on your **personal** account
the whole time.

## TL;DR

```powershell
# Your repo's default identity is your personal account. Bot actions are per-command:
git add <files>
.\tools\git-bot\bot.ps1 commit -m "your message"          # commit authored by the bot
.\tools\git-bot\bot.ps1 push                               # push current branch as the bot
.\tools\git-bot\bot.ps1 pr --base main --head <branch> --title "..." --body "..."
.\tools\git-bot\bot.ps1 issue --title "..." --body "..."
.\tools\git-bot\bot.ps1 whoami                             # show who's who + token state
```

Plain `git commit` / `git push` / `gh` keep using your personal account. Only the
command you run through `bot.ps1` is attributed to the bot.

## Quick identity switch (`git me` / `git bot` / `git who`)

This repo also has three git aliases for toggling the **commit author identity**
without `bot.ps1`:

```powershell
git bot      # switch this repo's commit identity to the bot
git me       # switch it back to your personal account (Zip <phatnguyen.tit@gmail.com>)
git who      # print the currently-active identity
```

> **Repository-local only.** These aliases live in this repo's `.git/config`
> (set with `git config --local alias.*`), **not** in your global git config. They
> work only inside this repository, are not shared via `git clone`, and do not affect
> any other repo on your machine.

Two things to keep in mind:

- **Identity only, not auth.** `git bot` changes *who the commit says it is*, but
  pushing/PRing as the bot still needs the hourly token — use `bot.ps1 push` / `bot.ps1 pr`
  for that. `git me` + plain `git push` uses your normal personal credential.
- **It is stateful.** Whatever you last switched to sticks (per-repo) until you switch
  back, so you can commit under the wrong name if you forget. Run `git who` first if
  unsure. (The per-command `bot.ps1 commit` has no such footgun — it sets the bot
  identity for that one commit only.)

To recreate these aliases (e.g. on a fresh clone, since `.git/config` is local):

```powershell
git config --local alias.me  '!git config user.name "Zip" && git config user.email "phatnguyen.tit@gmail.com" && echo "git identity -> Zip <phatnguyen.tit@gmail.com>"'
git config --local alias.bot '!git config user.name "booking-ai-analyst[bot]" && git config user.email "292146594+booking-ai-analyst[bot]@users.noreply.github.com" && echo "git identity -> booking-ai-analyst[bot]"'
git config --local alias.who '!echo "current git identity: $(git config user.name) <$(git config user.email)>"'
```

## Files in this folder

| File | Purpose | Tracked in git? |
|---|---|---|
| `bot.ps1` | Per-action wrapper: `commit` / `push` / `pr` / `issue` / `whoami` as the bot. **Does not** mutate repo identity. | ✅ yes |
| `bot-session.ps1` | Shared helpers: bot identity constants + `Get-BotToken` (mints & caches a ~1h installation token). Dot-sourced by the others. | ✅ yes |
| `ai-analyst-token.ps1` | Heavier "mode switch": makes the **whole current shell** the bot (sets `$env:GH_TOKEN`, rewrites `origin`, sets repo git config). Use only if you want a full bot session. | ✅ yes |
| `ai-analyst-app.pem` | GitHub App **private key**. Signs the JWT used to mint tokens. | ❌ **NEVER** — gitignored (`*.pem`, `tools/git-bot/*.pem`) |

> ⚠️ **Security:** `ai-analyst-app.pem` is a credential. Anyone holding it can act as the
> bot. It is gitignored and must never be committed, shared, or pasted into logs/PRs.
> If it leaks, revoke and regenerate the key from the GitHub App settings page.

## Identifiers

| Field | Value | Where to find it |
|---|---|---|
| App ID | `4008612` | GitHub App settings page (default in `bot-session.ps1`) |
| Installation ID | `139135129` | `github.com/settings/installations/<ID>` (default in `bot-session.ps1`) |
| Bot author | `booking-ai-analyst[bot] <292146594+booking-ai-analyst[bot]@users.noreply.github.com>` | constants in `bot-session.ps1` |

## How `bot.ps1` stays non-mutating

| Action | What it does | Repo side effects |
|---|---|---|
| `commit` | `git -c user.name=<bot> -c user.email=<bot> commit @args` | none — per-command override |
| `push` | pushes via an inline `https://x-access-token:<token>@…` URL | none — `origin` is never rewritten |
| `pr` / `issue` | sets `$env:GH_TOKEN` **inside the script's own process**, then runs `gh` | none — your shell's env is untouched |

Because nothing is written to `origin` or `git config`, an expired token or bot
author identity can never leak into your next terminal — the footgun of the
mode-switch script (below) is avoided entirely.

The installation token is cached in `$env:TEMP\ai-analyst-token-<app>-<install>.json`
and reused until ~5 min before expiry, so a `commit → push → pr` sequence only mints once.

## Heavier alternative: full bot session (`ai-analyst-token.ps1`)

Use this only when you want every `git`/`gh` command in the **current shell** to be
the bot. It mints a token, sets `$env:GH_TOKEN`, rewrites `origin` with the token,
and sets the repo git author to the bot.

```powershell
.\tools\git-bot\ai-analyst-token.ps1     # defaults to App 4008612 / install 139135129
git add <files>; git commit -m "..."; git push origin <branch>
gh pr create --base main --head <branch> --title "..." --body-file <body.md>
```

To return to your personal account afterwards:

```powershell
git remote set-url origin https://github.com/phatnguyentit/bookingsystem-microservice.git
git config --unset user.name
git config --unset user.email   # falls back to your global personal identity
```

## Gotchas

- **Token expiry:** the installation token lasts ~1 hour. `bot.ps1` re-mints
  automatically when the cache is stale. For `ai-analyst-token.ps1`, re-run it.
- **`gh api user` returns 403** ("Resource not accessible by integration"). Expected —
  App installation tokens can't call `/user`. PR/issue create/close/view work fine.
- **One PR per head→base:** GitHub blocks a second PR for the same branch pair. Close
  the existing one first (`gh pr close <n>`) to recreate it under the bot.
- **Same-shell rule (mode switch only):** `ai-analyst-token.ps1` sets `$env:GH_TOKEN`
  in the current session, so its `git`/`gh` commands must run in the same PowerShell
  invocation. `bot.ps1` has no such constraint — each call is self-contained.
- **Minting in git-bash fails:** native Windows `python`/`openssl` can't resolve
  `/tmp`-style paths, and PS 5.1 can't `ImportFromPem` the PKCS#1 key — which is why
  `bot-session.ps1` ships its own C# DER parser. Use the PowerShell scripts.
