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
