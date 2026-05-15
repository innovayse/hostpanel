# JSDoc Rules — Innovayse Frontend (Vue / TypeScript)

## Rule

**ALL exported functions, composables, Pinia stores, types, interfaces, and Vue component props MUST have JSDoc comments.**

Private (non-exported) functions inside composables/stores also require JSDoc.

---

## Required Tags

| Member type | Required tags |
|-------------|--------------|
| Composable function | `@description`, `@returns` |
| Regular function | `@description` (or summary line), `@param`, `@returns` |
| Type / Interface | `@description` for the type + `@description` per property |
| Pinia store | `@description` |
| Store action | `@description`, `@param`, `@returns` |
| Vue component (`.vue`) | `@description` in `<script setup>` top comment |
| Prop | inline comment above prop definition |

---

## Examples

### Composable

```ts
/**
 * Fetches and manages the current client's invoices.
 *
 * Wraps the `/api/portal/client/invoices` proxy endpoint.
 * Handles loading and error states automatically.
 *
 * @returns Reactive invoices list, loading flag, error message, and fetch action.
 */
export function useInvoices() {
  /** Fetched invoice list, empty until {@link fetchAll} resolves. */
  const invoices = ref<InvoiceDto[]>([])

  /** True while an API request is in flight. */
  const loading = ref(false)

  /** Human-readable error message, null when no error. */
  const error = ref<string | null>(null)

  /**
   * Loads all invoices for the authenticated client.
   *
   * @throws Will set {@link error} instead of throwing — safe to call without try/catch.
   */
  async function fetchAll(): Promise<void> {
    loading.value = true
    error.value = null
    try {
      invoices.value = await useApi<InvoiceDto[]>('/client/invoices')
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'Unknown error'
    } finally {
      loading.value = false
    }
  }

  return { invoices, loading, error, fetchAll }
}
```

### Type / Interface

```ts
/** DTO representing a single invoice returned by the API. */
export interface InvoiceDto {
  /** Unique invoice identifier. */
  id: number

  /** Total amount due in the client's billing currency. */
  total: number

  /** ISO 4217 currency code (e.g. "USD", "EUR"). */
  currency: string

  /** Current lifecycle status of the invoice. */
  status: 'unpaid' | 'paid' | 'overdue' | 'cancelled'

  /** ISO 8601 creation timestamp. */
  createdAt: string
}
```

### Pinia Store

```ts
/**
 * Manages the authenticated client's billing state.
 *
 * Invoices are lazy-loaded — call {@link fetchInvoices} before reading {@link invoices}.
 */
export const useInvoiceStore = defineStore('invoices', () => {
  /** All invoices for the current client. */
  const invoices = ref<InvoiceDto[]>([])

  /** True while invoices are being fetched. */
  const loading = ref(false)

  /**
   * Loads all invoices from the server and populates {@link invoices}.
   *
   * @returns Promise that resolves when invoices are loaded.
   */
  async function fetchInvoices(): Promise<void> { ... }

  return { invoices, loading, fetchInvoices }
})
```

### Vue Component (`<script setup>`)

```vue
<script setup lang="ts">
/**
 * Displays a paginated table of client invoices with pay/download actions.
 *
 * Fetches invoices on mount via {@link useInvoices}.
 * Emits `pay` when the user clicks Pay on a row.
 */

const props = defineProps<{
  /** Filter invoices by status. Pass undefined to show all. */
  statusFilter?: 'unpaid' | 'paid' | 'overdue'
}>()

const emit = defineEmits<{
  /** Emitted when the user initiates payment for an invoice. */
  pay: [invoiceId: number]
}>()
</script>
```

### Utility Function

```ts
/**
 * Formats a numeric amount as a localized currency string.
 *
 * @param amount - The numeric value to format.
 * @param currency - ISO 4217 currency code (e.g. "USD").
 * @param locale - BCP 47 locale tag. Defaults to the user's browser locale.
 * @returns Formatted string, e.g. "$1,234.56".
 */
export function formatCurrency(amount: number, currency: string, locale?: string): string {
  return new Intl.NumberFormat(locale, { style: 'currency', currency }).format(amount)
}
```

---

## Enforcement (ESLint)

Add to `eslint.config.mjs`:

```js
import jsdoc from 'eslint-plugin-jsdoc'

export default [
  jsdoc.configs['flat/recommended-typescript'],
  {
    plugins: { jsdoc },
    rules: {
      'jsdoc/require-jsdoc': ['warn', {
        require: {
          FunctionDeclaration: true,
          ArrowFunctionExpression: true,
          FunctionExpression: true,
        },
        publicOnly: false, // ALL functions, not just exported
      }],
      'jsdoc/require-description': 'warn',
      'jsdoc/require-param': 'warn',
      'jsdoc/require-returns': 'warn',
      'jsdoc/check-param-names': 'error',
      'jsdoc/check-types': 'off', // TypeScript handles types
    },
  },
]
```

Install: `yarn add -D eslint-plugin-jsdoc`
