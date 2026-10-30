# Contributing to HostPanel

Thanks for helping. HostPanel is built by a small team, and every issue, fix,
translation and review makes a real difference.

---

## Ways to help (no code required)

- **Report a bug** or **request a feature** through [the issue forms](../../issues/new/choose)
- **Translate**: the UI ships in English, Russian and Armenian. Strings live in
  `admin/src/locales/{en,ru,hy}/<area>.json` (loaded by `messages.ts`) and `client/locales/<lang>/`. A new language or a fix to an
  existing one is a great first PR
- **Improve the docs**: if something in the README tripped you up, it will trip the next
  person too
- **Answer questions** in [Discussions](../../discussions)
- **Star the repo** so other hosting companies can find it

## Your first code contribution

1. Pick an issue labelled [`good first issue`](../../issues?q=is%3Aissue+is%3Aopen+label%3A%22good+first+issue%22)
   or [`help wanted`](../../issues?q=is%3Aissue+is%3Aopen+label%3A%22help+wanted%22)
2. Leave a comment saying you are taking it. We will assign it to you
3. Fork the repo and branch from **`develop`** (not `main`)
4. Make your change, run the tests, open a pull request **against `develop`**
5. We aim to give a first review within **3 working days**

For anything bigger than a bug fix (a new payment gateway, registrar or control panel),
open an issue first so we can agree on the approach before you spend time on code.

---

## Before you begin

- HostPanel is licensed under the [AGPL-3.0-or-later](LICENSE). By contributing, you agree
  that your contribution is licensed under the same terms.
- Check [open issues](../../issues) before starting work: someone may already be on it.

---

## Development setup

Follow the [Quick start](README.md#quick-start-with-docker) and [Development](README.md#run-from-source)
sections of the README. A clone builds and runs with `AUTH_MODE=local`; you do not
need any token or account.

### The plugin SDK is a submodule

`backend/src/Innovayse.SDK` is a git submodule of
[innovayse/hostpanel-sdk](https://github.com/innovayse/hostpanel-sdk). Clone with
`git clone --recurse-submodules`, or run `git submodule update --init` after a plain clone and after
any pull that moves the submodule; without it the backend has no SDK project to build.

A change to the SDK itself (a plugin contract, a base class, the package readme) is a pull request
in that repository. When the host has to support the change too, open the matching pull request
here as well: it moves the submodule to the new SDK commit and carries the host side. Mention each
pull request in the other.

### Running the backend tests while the dev stack is up

Run them **inside** the API container, or stop the stack first:

```bash
docker compose -f docker/docker-compose.shared.yml -f docker/docker-compose.dev.yml exec hostpanel-api dotnet test tests/Innovayse.Hostpanel.Integration.Tests
```

Running `dotnet test` or `dotnet restore` on the host against this tree while
the dev stack (`docker/docker-compose.dev.yml`) is running writes into each project's `obj/`, and the
container's `dotnet watch` is polling that same bind mount. It reports the churn
as source changes and eventually dies enumerating it:

```
Unhandled exception. System.ArgumentException: An item with the same key has
already been added. Key: /app/src/Innovayse.SDK/obj/project.nuget.cache
   at Microsoft.DotNet.Watch.PollingDirectoryWatcher.CheckForChangedFiles()
```

The API then 502s for the few minutes it takes to rebuild. Polling is not
optional here — inotify events do not cross a Windows or macOS bind mount, so
without it the container never sees an edit at all.

---

## How to Contribute

### Reporting Bugs

Open a GitHub issue with:
- A clear title and description
- Steps to reproduce
- Expected vs actual behavior
- Your environment (OS, .NET version, Node version, browser)

### Suggesting Features

Open a GitHub issue tagged `enhancement`. Describe:
- The problem you are solving
- Your proposed solution
- Any alternatives you considered

### Submitting Code

1. Fork the repository
2. Create a branch from `develop`: `git checkout -b feat/your-feature develop` or `fix/your-bug`
3. Write your changes following the code standards below
4. Commit with a clear message (see [Commit Messages](#commit-messages))
5. Push and open a Pull Request against `develop`. `main` only accepts merges from
   `develop` and `hotfix/*`, and a CI check blocks anything else
6. Fill in the PR template and link the related issue

---

## Code Standards

### Backend (C#)

- Follow `rules/clean-architecture.md` — strict layer boundaries
- Follow `rules/csharp-style.md` — C# 12+, primary constructors, records
- All members (including private) must have XML doc comments — see `rules/xml-docs.md`
- Run `dotnet format` before committing — see `rules/dotnet-format.md`
- Controllers are thin: dispatch to Wolverine bus only, no business logic

### Frontend (Vue / TypeScript)

- Vue 3 Composition API with `<script setup lang="ts">` only — no Options API
- All exported functions, composables, stores, and types must have JSDoc — see `rules/jsdoc.md`
- No `any` types
- All API calls go through composables — never `$fetch` directly in components
- Tailwind CSS only — no inline styles

---

## Commit Messages

Use [Conventional Commits](https://www.conventionalcommits.org/):

```
feat: add payment gateway plugin
fix: correct invoice total calculation on item removal
docs: update Quick Start setup steps
chore: upgrade EF Core to 8.0.5
```

Types: `feat`, `fix`, `docs`, `chore`, `refactor`, `test`, `ci`

---

## Pull Request Checklist

Before submitting, confirm:

- [ ] Code follows the style rules above
- [ ] `dotnet format` has been run (backend changes)
- [ ] No hardcoded secrets, credentials, or environment-specific values
- [ ] XML docs added to all new C# members
- [ ] JSDoc added to all new exported TypeScript/Vue functions
- [ ] The PR description explains what changed and why

---

## Project Structure

See [README.md](README.md#architecture-in-brief) for a full overview of the codebase layout.

---

## Questions

Ask in [GitHub Discussions](../../discussions) or chat on [Discord](https://discord.gg/NfqTkyTgD). Security issues: see [SECURITY.md](SECURITY.md),
please do not open a public issue.

Everyone taking part is expected to follow the [Code of Conduct](CODE_OF_CONDUCT.md).
