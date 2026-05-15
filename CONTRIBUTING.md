# Contributing to Innovayse

Thank you for your interest in contributing! This document explains how to get started.

---

## Before You Begin

- Read the [LICENSE](LICENSE). By contributing, you agree that your contributions will be licensed under the same terms.
- Check [open issues](../../issues) before starting work — someone may already be working on it.
- For large changes, open an issue first to discuss the approach before writing code.

---

## Development Setup

Follow the [Quick Start](README.md#quick-start) guide in the README to get the project running locally.

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
2. Create a branch from `main`: `git checkout -b feat/your-feature` or `fix/your-bug`
3. Write your changes following the code standards below
4. Commit with a clear message (see [Commit Messages](#commit-messages))
5. Push and open a Pull Request against `main`
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
feat: add Stripe payment gateway integration
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

See [README.md](README.md#project-structure) for a full overview of the codebase layout.

---

## Questions

Open a GitHub Discussion or an issue tagged `question`.
