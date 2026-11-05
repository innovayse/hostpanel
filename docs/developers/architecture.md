---
title: Architecture overview
description: The pieces, layers and rules that make up Hostpanel.
---

# Architecture overview

<Callout type="note">
When this page and the rules files disagree, the rules files decide.
</Callout>

## The pieces

```
Browser
  ├── Admin panel (admin/)      calls    API
  └── Client portal (client/)   calls    server routes  calls  API
                                                                 │
                                                   Database, Queue, Cache
```

| Piece | What it is |
|-------|-----------|
| **API** (`Innovayse.Hostpanel.API`) | The backend. Handles all HTTP requests, runs background jobs, talks to the database, message queue and cache. |
| **Admin panel** (`admin/`) | Single-page application for staff. Reads and writes the API. |
| **Client portal** (`client/`) | Server-rendered application for customers. Has its own server routes that proxy the API, so the API is never called directly from the browser. |
| **Database** | Relational store. Every table, column, key, index and constraint name is PascalCase. |
| **Message queue** | Carries commands and queries between the API process and its background workers. |
| **Cache** | Shared cache used for rate limiting, session state and short-lived data. |

The plugin SDK (`Hostpanel.Sdk`) is MIT-licensed and lives as a git submodule at `backend/src/Hostpanel.Sdk`. It is the only part of the codebase that third-party plugin authors consume.

## The layers

Dependencies point inward. Outer layers may reference inner ones; inner layers never reference outer ones. Architecture tests in `Innovayse.Hostpanel.Architecture.Tests` enforce this.

| Layer | Project | Holds |
|-------|---------|-------|
| **SharedKernel** | `Innovayse.Hostpanel.SharedKernel` | Primitives shared across all layers: base types, result types, extension methods. |
| **Domain** | `Innovayse.Hostpanel.Domain` | Entities, enums and domain logic. No dependency on infrastructure or application concerns. |
| **Application** | `Innovayse.Hostpanel.Application` | Use cases: commands, queries, handlers, validators. Defines ports (`I<Port>` interfaces) for everything the application needs from the outside. |
| **Infrastructure** | `Innovayse.Hostpanel.Infrastructure` | Adapters that implement the application's ports: the ORM, email, storage, the message bus, background jobs. |
| **API** | `Innovayse.Hostpanel.API` | Controllers, middleware, startup. The composition root: wires everything together. |
| **Shared** | `Shared` | Cross-cutting code for the main project: `BaseApiController`, `StatusMapping`, shared models. Plugins do not reference this project. |

## The plugin SDK and bundled plugins

The SDK lives at `backend/src/Hostpanel.Sdk` as a git submodule (hosted separately at `github.com/innovayse/hostpanel-sdk`). Clone the repository with `--recurse-submodules`, or run `git submodule update --init` after a plain clone.

Bundled plugins ship with the server image and live under `backend/src/Innovayse.Providers.*`. They are the reference implementations for plugin authors.

See the [Write a plugin](./write-a-plugin) guide for the full plugin authoring guide.

## The repository

| Folder | Contents |
|--------|---------|
| `backend/` | `src/` (projects), `tests/` (test projects), `plugins/` (migration addon), `agent/` (SSH key agent) |
| `admin/` | Admin panel |
| `client/` | Client portal |
| `docker/` | Compose files, environment templates, nginx config |
| `docs/` | This documentation site |
| `rules/` | Code style rules. The source of truth for code style decisions. |
| `scripts/` | CI and maintenance scripts |

## The rules folder

`rules/*.md` is the source of truth for code style. When this page and the rules disagree, the rules win.

| File | Covers |
|------|--------|
| `rules/project.md` | Project overview, where things go, what differs from workspace conventions |
| `rules/clean-architecture.md` | Layer rules and dependency direction |
| `rules/csharp-style.md` | C# style: braces, constructors, naming, one type per file |
| `rules/dotnet-format.md` | Formatter setup and pre-commit steps |
| `rules/xml-docs.md` | XML doc comment requirements for C# |
| `rules/jsdoc.md` | JSDoc requirements for TypeScript and Vue |
| `rules/vuejs-style.md` | Vue component and composable conventions |
