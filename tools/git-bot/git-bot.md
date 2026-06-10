# git-bot — commit & PR as the `booking-ai-analyst[bot]` account

Quick workflow for making commits and opening pull requests under the
**booking-ai-analyst** GitHub App (the bot), instead of a personal account.

## Files in this folder

| File | Purpose | Tracked in git? |
|---|---|---|
| `ai-analyst-token.ps1` | Mints a ~1-hour installation token, sets `$env:GH_TOKEN`, rewrites the `origin` remote with the bot token, and sets the bot git author identity. | ✅ yes |
| `ai-analyst-app.pem` | GitHub App **private key**. Signs the JWT used to mint tokens. | ❌ **NEVER** — gitignored (`*.pem`, `tools/git-bot/*.pem`) |

> ⚠️ **Security:** `ai-analyst-app.pem` is a credential. Anyone holding it can act as the
> bot. It is gitignored and must never be committed, shared, or pasted into logs/PRs.
> If it leaks, revoke and regenerate the key from the GitHub App settings page.

## Identifiers

| Field | Value | Where to find it |
|---|---|---|
| App ID | `4008612` | GitHub App settings page |
| Installation ID | `139135129` | `github.com/settings/installations/<ID>`, or `GET /repos/{owner}/{repo}/installation` with an app JWT |
| Bot author | `booking-ai-analyst[bot] <292146594+booking-ai-analyst[bot]@users.noreply.github.com>` | Set automatically by the script |

## Usage

Run from the repo root in **PowerShell**. Because the script sets `$env:GH_TOKEN` and
the bot identity in the *current* session, the token script and the `git`/`gh` commands
that use it must run in the **same PowerShell invocation**.

```powershell
# 1. Acquire bot session (token valid ~1 hour)
.\tools\git-bot\ai-analyst-token.ps1 `
    -AppId 4008612 `
    -InstallationId 139135129 `
    -PemPath ".\tools\git-bot\ai-analyst-app.pem"

# 2. Commit (author is now the bot) and push
git add <files>
git commit -m "your message"
git push origin <branch>

# 3. Open a PR as the bot (gh uses $env:GH_TOKEN)
gh pr create --base main --head <branch> --title "..." --body-file <body.md>
```

## What the script does (steps)

1. Builds an RS256 JWT (`iss = AppId`, 10-min expiry) signed with the `.pem`
   (a small C# helper parses the PKCS#1 `BEGIN RSA PRIVATE KEY` format on .NET Framework).
2. `POST /app/installations/{InstallationId}/access_tokens` → fresh `ghs_` token.
3. Resolves the bot's slug and **user id** to build the `…@users.noreply.github.com`
   commit email that links commits to the bot avatar.
4. Sets `$env:GH_TOKEN`, rewrites `origin` to `https://x-access-token:<token>@…`,
   and runs `git config user.name/user.email` to the bot identity.

## Gotchas

- **Token expiry:** the installation token lasts ~1 hour. Re-run the script to refresh.
  After it expires, pushes fail with *"Password authentication is not supported"* —
  that's the stale token in the `origin` URL, not a real auth problem.
- **`gh api user` returns 403** ("Resource not accessible by integration"). This is
  expected — App installation tokens can't call `/user`. PR create/close/view work fine.
- **One PR per head→base:** GitHub blocks a second PR for the same branch pair. Close the
  existing PR first (`gh pr close <n>`) if you need to recreate it under the bot.
- **Don't run the token script and the git/gh commands in separate tool calls** — the
  PowerShell session (and `$env:GH_TOKEN`) does not persist across separate invocations.
- **Minting the token in git-bash fails:** native Windows `python` shelling out to a
  Windows `openssl` can't resolve `/tmp`-style paths, and PS 5.1 can't `ImportFromPem`
  the PKCS#1 key — which is exactly why this script ships its own C# DER parser. Use it.

## Restoring a clean remote

The script embeds the token in the `origin` URL. To strip it back to a plain URL:

```powershell
git remote set-url origin https://github.com/phatnguyentit/bookingsystem-microservice.git
```
