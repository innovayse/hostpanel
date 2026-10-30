---
title: Components
---

# Components

This page showcases the reusable Vue components available to all contributors writing documentation pages. Import is not required — every component is registered globally and can be used directly in any `.md` file.

---

## Callout

A highlighted callout box with a colored left border. Use it to draw attention to important information.

**Prop:** `type` — `info` (default) | `warning` | `danger` | `tip`

**Usage:**

```md
<Callout type="info">
This is an informational note.
</Callout>

<Callout type="tip">
A helpful tip for readers.
</Callout>

<Callout type="warning">
Something to be careful about.
</Callout>

<Callout type="danger">
This action is irreversible.
</Callout>
```

**Preview:**

<Callout type="info">
This is an informational note. Use it for general guidance or context.
</Callout>

<Callout type="tip">
A helpful tip for readers. Use it to highlight best practices.
</Callout>

<Callout type="warning">
Something to be careful about. Use it for gotchas or non-obvious behavior.
</Callout>

<Callout type="danger">
This action is irreversible. Use it for destructive or security-sensitive steps.
</Callout>

---

## Steps

A numbered steps list with styled circular counters. Place standard `<li>` elements inside.

**Usage:**

```md
<Steps>
  <li>Clone the repository.</li>
  <li>Install dependencies with <code>pnpm install</code>.</li>
  <li>Start the dev server with <code>pnpm dev</code>.</li>
</Steps>
```

**Preview:**

<Steps>
  <li>Clone the repository.</li>
  <li>Install dependencies with <code>pnpm install</code>.</li>
  <li>Start the dev server with <code>pnpm dev</code>.</li>
</Steps>

---

## Tabs

VitePress ships with a built-in `<Tabs>` / `<Tab>` component pair. No custom component needed.

**Usage:**

```
<Tabs>
  <Tab label="npm">

    ```bash
    npm install
    ```

  &#x3C;/Tab>
  <Tab label="pnpm">

    ```bash
    pnpm install
    ```

  &#x3C;/Tab>
&#x3C;/Tabs>
```

See the [VitePress docs on tabs](https://vitepress.dev/guide/markdown#custom-containers) for full details.

---

## SettingTable

A structured table for documenting configuration options or settings objects.

**Prop:** `settings` — array of `{ name, type, default, description }` objects.

**Usage:**

```md
<SettingTable :settings="[
  { name: 'host', type: 'string', default: 'localhost', description: 'Database host address.' },
  { name: 'port', type: 'number', default: '5432', description: 'Database port.' },
  { name: 'ssl', type: 'boolean', default: 'false', description: 'Enable SSL connections.' },
  { name: 'poolSize', type: 'number', default: '10', description: 'Connection pool size.' },
]" />
```

**Preview:**

<SettingTable :settings="[
  { name: 'host', type: 'string', default: 'localhost', description: 'Database host address.' },
  { name: 'port', type: 'number', default: '5432', description: 'Database port.' },
  { name: 'ssl', type: 'boolean', default: 'false', description: 'Enable SSL connections.' },
  { name: 'poolSize', type: 'number', default: '10', description: 'Connection pool size.' },
]" />

---

## Label

An inline badge for tagging headings, settings, or features.

**Props:**
- `type` — `admin` | `new` | `local` | `sso`
- `version` _(optional)_ — when `type="new"`, renders "New in `<version>`"

**Usage:**

```md
Server Settings <Label type="admin" />

Dark mode <Label type="new" version="2.1" />

Runs on your machine <Label type="local" />

Single sign-on required <Label type="sso" />
```

**Preview:**

<br>

Server Settings <Label type="admin" />

Dark mode <Label type="new" version="2.1" />

Runs on your machine <Label type="local" />

Single sign-on required <Label type="sso" />
