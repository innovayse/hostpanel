# Vue.js Code Style Rules — Innovayse Frontend

## General

- Vue 3 Composition API + `<script setup lang="ts">` only — no Options API
- TypeScript strict mode — no `any`
- One component per file
- File name: PascalCase for components (`InvoiceTable.vue`), kebab-case for pages (`invoice-detail.vue`)
- Pinia for all shared state — no Vuex, no raw `provide/inject` for business data

## Component Structure Order

```vue
<script setup lang="ts">
// 1. imports
// 2. props / emits
// 3. composables
// 4. reactive state
// 5. computed
// 6. methods
// 7. lifecycle hooks
// 8. watchers
</script>

<template>
  <!-- single root element or Fragment -->
</template>

<style scoped>
/* only if not using Tailwind exclusively */
</style>
```

## Props & Emits

- Always define props with `defineProps<{}>()` — no runtime props object
- Always define emits with `defineEmits<{}>()` — typed emit
- Required props have no default — optional props use `withDefaults`

```ts
const props = withDefaults(defineProps<{
  invoiceId: number
  loading?: boolean
}>(), {
  loading: false
})

const emit = defineEmits<{
  submitted: [invoiceId: number]
  cancelled: []
}>()
```

## Composables

- File name: `use` prefix + PascalCase (`useInvoices.ts`)
- Return only what is needed — no leaking internal refs
- All composables that fetch data handle `loading`, `error`, and `data`

```ts
export function useInvoices() {
  const invoices = ref<InvoiceDto[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  async function fetchAll() { ... }

  return { invoices, loading, error, fetchAll }
}
```

## API Calls

- Never call `$fetch` / `useFetch` directly in components
- All API calls go through composables or Pinia actions
- Nuxt: use `useApi` composable (wraps server proxy)
- Vue Admin: use `useApi` composable (wraps `/proxy/` Vite proxy)

## Naming Conventions

| Type | Convention | Example |
|------|-----------|---------|
| Component | PascalCase | `InvoiceTable.vue` |
| Page | kebab-case | `invoice-detail.vue` |
| Composable | `use` + PascalCase | `useInvoices.ts` |
| Pinia store | `use` + PascalCase + `Store` | `useInvoiceStore.ts` |
| Emitted event | camelCase | `invoiceSubmitted` |
| CSS class | kebab-case (Tailwind) | `invoice-card` |

## Template Rules

- No logic in templates beyond simple ternary — extract to `computed`
- Always use `:key` on `v-for` — use stable unique ID, never index
- `v-if` and `v-for` never on the same element — use `<template>` wrapper
- Always use `v-bind` shorthand (`:`) and `v-on` shorthand (`@`)

```vue
<!-- CORRECT -->
<InvoiceRow
  v-for="invoice in invoices"
  :key="invoice.id"
  :invoice="invoice"
  @pay="handlePay"
/>

<!-- WRONG -->
<InvoiceRow v-for="(invoice, i) in invoices" :key="i" />
```

## Tailwind CSS

- No inline `style` attributes — use Tailwind classes
- Extract repeated class combos to component or `@apply` in scoped style
- Dark mode: use `dark:` variant — no manual theme toggling

## Page shell, gutters and scrolling

**The layout owns the gutter. A view never pads itself.**

`Layout.vue` renders one wrapper inside `<main>` that carries the content gutter:

```vue
<main class="relative flex-1 min-h-0 overflow-auto">
  <div class="flex min-h-full w-full flex-col p-4 sm:p-6 lg:p-8">
    <RouterView />
  </div>
</main>
```

A routed view therefore starts at its own content — no `p-4 sm:p-6 lg:p-8` on a view
root, and no re-padding further down. This was previously repeated on 63 view roots,
which is why a view added without it looked broken and a view with a different value
looked subtly off; neither is discoverable from the view itself.

**Exactly one element on the page scrolls, and it is that `<main>`.** The shell is
`h-dvh overflow-hidden`, not `min-h-dvh` — the document itself must never grow past
the viewport, because the sidebar is a flex sibling of the content column and scrolls
away with the document when it does. Two consequences worth stating, because both
fail silently:

- **Every flex ancestor between the shell and `<main>` needs `min-h-0`.** A flex child
  defaults to `min-height: auto`, refuses to shrink below its content, and the
  `overflow-auto` never engages — the overflow moves to the document instead.
- **The sidebar sizes with `h-full`, never `min-h-dvh`.** It is stretched by the
  shell; asking for viewport height again is what makes it overflow by the height of
  anything above it.

A view that needs the full height (a nested module layout, a split pane, a board) asks
for it with `h-full min-h-0 flex-1`, which the gutter wrapper's `min-h-full` flex
column grants. A module that owns a second sidebar and must sit flush against the
shell edge cancels the gutter with `-m-4 sm:-m-6 lg:-m-8` and re-applies it inside its
own scroll pane — see `ReportsLayout.vue` and `IntegrationsLayout.vue`. That is the
only sanctioned reason to write the gutter values anywhere but `Layout.vue`.

Full-page views rendered **outside** `Layout` — the auth screens — are not bound by
any of this and keep their own `min-h-dvh`.

## TypeScript in Vue

- No `as any` — never
- Define explicit types for all props, emits, composable return values
- Use `Ref<T>` and `ComputedRef<T>` in type annotations where needed
- Import types with `import type { ... }`

## No

- No `defineComponent()` — use `<script setup>` only
- No `this` — Composition API only
- No mutations of props — emit events instead
- No `document.querySelector` in components — use template refs
- No `setTimeout`/`setInterval` without cleanup in `onUnmounted`
