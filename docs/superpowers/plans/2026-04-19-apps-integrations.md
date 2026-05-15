# Apps & Integrations Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a new `/integrations` module to the admin panel that lets admins view, enable/disable, and configure all third-party service integrations (payments, registrars, provisioning, email, fraud).

**Architecture:** Section-grouped main page + dedicated config page per integration slug. Pinia store handles all API calls. Four components split by responsibility: section card, integration row, config form (dynamic by slug), status sidebar.

**Tech Stack:** Vue 3 + `<script setup lang="ts">`, Pinia, Vue Router, Tailwind CSS, `useApi` composable (existing).

---

## File Map

| Action | File |
|--------|------|
| Create | `admin/src/modules/integrations/types/integration.types.ts` |
| Create | `admin/src/modules/integrations/stores/integrationsStore.ts` |
| Create | `admin/src/modules/integrations/components/IntegrationRow.vue` |
| Create | `admin/src/modules/integrations/components/IntegrationSection.vue` |
| Create | `admin/src/modules/integrations/components/IntegrationStatusSidebar.vue` |
| Create | `admin/src/modules/integrations/components/IntegrationConfigForm.vue` |
| Create | `admin/src/modules/integrations/views/IntegrationsView.vue` |
| Create | `admin/src/modules/integrations/views/IntegrationDetailView.vue` |
| Modify | `admin/src/router/index.ts` |
| Modify | `admin/src/components/layout/AppSidebar.vue` |

---

## Task 1: Types

**Files:**
- Create: `admin/src/modules/integrations/types/integration.types.ts`

- [ ] **Step 1: Create the types file**

```typescript
/** Category grouping for an integration. */
export type IntegrationCategory =
  | 'payments'
  | 'registrars'
  | 'provisioning'
  | 'email'
  | 'fraud'

/** Summary of a single integration returned by GET /api/admin/integrations. */
export interface IntegrationDto {
  /** URL-safe slug used in routes and API calls (e.g. "stripe", "cpanel"). */
  slug: string
  /** Human-readable name (e.g. "Stripe"). */
  name: string
  /** Short description shown in the integration row. */
  description: string
  /** Category this integration belongs to. */
  category: IntegrationCategory
  /** Whether this integration is currently enabled. */
  isEnabled: boolean
  /** ISO 8601 timestamp of the last successful connection test, or null. */
  lastTestedAt: string | null
}

/** Full config for a single integration returned by GET /api/admin/integrations/:slug. */
export interface IntegrationDetailDto extends IntegrationDto {
  /**
   * Key-value pairs of configuration fields.
   * Secret values are masked (e.g. "sk_live_••••••••").
   */
  config: Record<string, string>
}

/** Payload sent to PUT /api/admin/integrations/:slug. */
export interface IntegrationConfigPayload {
  /** Whether the integration should be enabled after save. */
  isEnabled: boolean
  /** Full (unmasked) config values the admin has edited. */
  config: Record<string, string>
}

/** Result returned by POST /api/admin/integrations/:slug/test. */
export interface IntegrationTestResult {
  /** Whether the connection test succeeded. */
  success: boolean
  /** Human-readable message (error detail or "Connection OK"). */
  message: string
}

/**
 * Static metadata for rendering each integration card.
 * Not fetched from the API — defined client-side.
 */
export interface IntegrationMeta {
  /** Matches IntegrationDto.slug. */
  slug: string
  /** Tailwind background color class for the logo block (e.g. "bg-[#635bff]"). */
  color: string
  /** Category this integration belongs to. */
  category: IntegrationCategory
  /** Ordered list of config field keys and their labels. */
  fields: IntegrationField[]
  /** Optional hint shown in the status sidebar (e.g. webhook URL instructions). */
  hint?: string
}

/** A single configurable field for an integration. */
export interface IntegrationField {
  /** Field key matching the config Record key. */
  key: string
  /** Human-readable label. */
  label: string
  /** Input type — "text" for most, "password" for secrets, "select" for enums. */
  type: 'text' | 'password' | 'select' | 'textarea'
  /** Options for "select" type fields. */
  options?: string[]
}
```

- [ ] **Step 2: Commit**

```bash
git add admin/src/modules/integrations/types/integration.types.ts
git commit -m "feat(integrations): add integration TypeScript types"
```

---

## Task 2: Integration Metadata (client-side static config)

**Files:**
- Create: `admin/src/modules/integrations/types/integration.meta.ts`

This file defines the static per-integration metadata (colors, fields, hints) so the dynamic config form knows what to render. Not fetched from the API.

- [ ] **Step 1: Create the metadata file**

```typescript
import type { IntegrationMeta } from './integration.types'

/**
 * Static metadata for all supported integrations.
 * Keyed by slug for O(1) lookup.
 */
export const INTEGRATION_META: Record<string, IntegrationMeta> = {
  stripe: {
    slug: 'stripe',
    color: 'bg-[#635bff]',
    category: 'payments',
    hint: 'Add this webhook endpoint in your Stripe dashboard: https://yourdomain.com/api/webhooks/stripe',
    fields: [
      { key: 'secretKey', label: 'Secret Key', type: 'password' },
      { key: 'publishableKey', label: 'Publishable Key', type: 'text' },
      { key: 'webhookSecret', label: 'Webhook Secret', type: 'password' },
      { key: 'mode', label: 'Mode', type: 'select', options: ['Live', 'Test'] },
    ],
  },
  paypal: {
    slug: 'paypal',
    color: 'bg-[#009cde]',
    category: 'payments',
    fields: [
      { key: 'clientId', label: 'Client ID', type: 'text' },
      { key: 'clientSecret', label: 'Client Secret', type: 'password' },
      { key: 'mode', label: 'Mode', type: 'select', options: ['Live', 'Sandbox'] },
    ],
  },
  'bank-transfer': {
    slug: 'bank-transfer',
    color: 'bg-green-600',
    category: 'payments',
    fields: [
      { key: 'accountName', label: 'Account Name', type: 'text' },
      { key: 'iban', label: 'IBAN', type: 'text' },
      { key: 'bankName', label: 'Bank Name', type: 'text' },
      { key: 'instructions', label: 'Payment Instructions', type: 'textarea' },
    ],
  },
  namecheap: {
    slug: 'namecheap',
    color: 'bg-amber-600',
    category: 'registrars',
    fields: [
      { key: 'apiKey', label: 'API Key', type: 'password' },
      { key: 'apiUsername', label: 'API Username', type: 'text' },
      { key: 'clientIp', label: 'Whitelisted Client IP', type: 'text' },
    ],
  },
  resellerclub: {
    slug: 'resellerclub',
    color: 'bg-sky-500',
    category: 'registrars',
    fields: [
      { key: 'resellerId', label: 'Reseller ID', type: 'text' },
      { key: 'apiKey', label: 'API Key', type: 'password' },
    ],
  },
  enom: {
    slug: 'enom',
    color: 'bg-violet-700',
    category: 'registrars',
    fields: [
      { key: 'accountId', label: 'Account ID', type: 'text' },
      { key: 'apiKey', label: 'API Key', type: 'password' },
    ],
  },
  cpanel: {
    slug: 'cpanel',
    color: 'bg-orange-600',
    category: 'provisioning',
    fields: [
      { key: 'host', label: 'WHM Host', type: 'text' },
      { key: 'port', label: 'Port', type: 'text' },
      { key: 'username', label: 'Username', type: 'text' },
      { key: 'apiToken', label: 'API Token', type: 'password' },
    ],
  },
  plesk: {
    slug: 'plesk',
    color: 'bg-blue-700',
    category: 'provisioning',
    fields: [
      { key: 'host', label: 'Plesk Host', type: 'text' },
      { key: 'port', label: 'Port', type: 'text' },
      { key: 'username', label: 'Username', type: 'text' },
      { key: 'password', label: 'Password', type: 'password' },
    ],
  },
  smtp: {
    slug: 'smtp',
    color: 'bg-teal-700',
    category: 'email',
    fields: [
      { key: 'host', label: 'SMTP Host', type: 'text' },
      { key: 'port', label: 'Port', type: 'text' },
      { key: 'username', label: 'Username', type: 'text' },
      { key: 'password', label: 'Password', type: 'password' },
      { key: 'fromAddress', label: 'From Address', type: 'text' },
      { key: 'encryption', label: 'Encryption', type: 'select', options: ['TLS', 'SSL', 'None'] },
    ],
  },
  maxmind: {
    slug: 'maxmind',
    color: 'bg-red-800',
    category: 'fraud',
    fields: [
      { key: 'accountId', label: 'Account ID', type: 'text' },
      { key: 'licenseKey', label: 'License Key', type: 'password' },
    ],
  },
}

/**
 * Ordered category definitions for rendering sections on the main page.
 */
export const INTEGRATION_CATEGORIES = [
  { key: 'payments' as const, label: 'Payment Gateways', icon: '💳' },
  { key: 'registrars' as const, label: 'Domain Registrars', icon: '🌐' },
  { key: 'provisioning' as const, label: 'Hosting / Provisioning', icon: '🖥️' },
  { key: 'email' as const, label: 'Email / SMTP', icon: '📧' },
  { key: 'fraud' as const, label: 'Fraud Protection', icon: '🛡️' },
]
```

- [ ] **Step 2: Commit**

```bash
git add admin/src/modules/integrations/types/integration.meta.ts
git commit -m "feat(integrations): add static integration metadata"
```

---

## Task 3: Pinia Store

**Files:**
- Create: `admin/src/modules/integrations/stores/integrationsStore.ts`

- [ ] **Step 1: Create the store**

```typescript
import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useApi } from '../../../composables/useApi'
import type {
  IntegrationDto,
  IntegrationDetailDto,
  IntegrationConfigPayload,
  IntegrationTestResult,
} from '../types/integration.types'

/**
 * Pinia store for managing integration list and per-integration config.
 */
export const useIntegrationsStore = defineStore('integrations', () => {
  const { request } = useApi()

  /** All integrations summary list. */
  const integrations = ref<IntegrationDto[]>([])

  /** Currently loaded integration detail (config page). */
  const current = ref<IntegrationDetailDto | null>(null)

  /** True while any request is in flight. */
  const loading = ref(false)

  /** Error message, null when no error. */
  const error = ref<string | null>(null)

  /** Result of the last connection test. */
  const testResult = ref<IntegrationTestResult | null>(null)

  /**
   * Fetches the summary list of all integrations.
   *
   * @returns Promise that resolves when data is loaded.
   */
  async function fetchAll(): Promise<void> {
    loading.value = true
    error.value = null
    try {
      integrations.value = await request<IntegrationDto[]>('/admin/integrations')
    } catch {
      error.value = 'Failed to load integrations.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Fetches full config detail for a single integration by slug.
   *
   * @param slug - Integration slug (e.g. "stripe", "cpanel").
   * @returns Promise that resolves when data is loaded.
   */
  async function fetchOne(slug: string): Promise<void> {
    loading.value = true
    error.value = null
    current.value = null
    try {
      current.value = await request<IntegrationDetailDto>(`/admin/integrations/${slug}`)
    } catch {
      error.value = 'Failed to load integration config.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Saves updated config for an integration.
   *
   * @param slug - Integration slug.
   * @param payload - Updated enabled state and config values.
   * @returns Promise that resolves when save is complete.
   */
  async function saveConfig(slug: string, payload: IntegrationConfigPayload): Promise<void> {
    loading.value = true
    error.value = null
    try {
      current.value = await request<IntegrationDetailDto>(`/admin/integrations/${slug}`, {
        method: 'PUT',
        body: JSON.stringify(payload),
      })
    } catch {
      error.value = 'Failed to save integration config.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Runs a live connection test for an integration.
   *
   * @param slug - Integration slug.
   * @returns Promise that resolves with the test result.
   */
  async function testConnection(slug: string): Promise<void> {
    loading.value = true
    testResult.value = null
    try {
      testResult.value = await request<IntegrationTestResult>(`/admin/integrations/${slug}/test`, {
        method: 'POST',
      })
    } catch {
      testResult.value = { success: false, message: 'Connection test failed.' }
    } finally {
      loading.value = false
    }
  }

  return {
    integrations,
    current,
    loading,
    error,
    testResult,
    fetchAll,
    fetchOne,
    saveConfig,
    testConnection,
  }
})
```

- [ ] **Step 2: Commit**

```bash
git add admin/src/modules/integrations/stores/integrationsStore.ts
git commit -m "feat(integrations): add integrationsStore"
```

---

## Task 4: IntegrationRow Component

**Files:**
- Create: `admin/src/modules/integrations/components/IntegrationRow.vue`

Single integration row inside a section card — logo, name, description, status badge, Configure link.

- [ ] **Step 1: Create the component**

```vue
<script setup lang="ts">
/**
 * Displays a single integration as a row inside a category section card.
 *
 * Shows logo color block, name, description, active/inactive badge, and Configure link.
 */
import { RouterLink } from 'vue-router'
import { INTEGRATION_META } from '../types/integration.meta'
import type { IntegrationDto } from '../types/integration.types'

/** Props for IntegrationRow. */
const props = defineProps<{
  /** The integration summary data from the store. */
  integration: IntegrationDto
}>()

/** Static metadata for this integration (color, slug). */
const meta = INTEGRATION_META[props.integration.slug]
</script>

<template>
  <div
    class="flex items-center gap-3 px-4 py-4 border-r border-gray-100 last:border-r-0"
    :class="{ 'opacity-65': !integration.isEnabled }"
  >
    <div
      class="w-9 h-9 rounded-lg flex-shrink-0"
      :class="meta?.color ?? 'bg-gray-400'"
    />

    <div class="flex-1 min-w-0">
      <div class="text-sm font-semibold text-gray-800 truncate">{{ integration.name }}</div>
      <div class="text-xs text-gray-500 truncate">{{ integration.description }}</div>
    </div>

    <div class="flex flex-col items-end gap-1.5 flex-shrink-0">
      <span
        class="px-2 py-0.5 rounded-full text-xs font-semibold"
        :class="integration.isEnabled
          ? 'bg-green-100 text-green-700'
          : 'bg-gray-100 text-gray-400'"
      >
        {{ integration.isEnabled ? 'Active' : 'Inactive' }}
      </span>
      <RouterLink
        :to="`/integrations/${integration.slug}`"
        class="text-xs text-blue-500 hover:text-blue-700 transition"
      >
        Configure →
      </RouterLink>
    </div>
  </div>
</template>
```

- [ ] **Step 2: Commit**

```bash
git add admin/src/modules/integrations/components/IntegrationRow.vue
git commit -m "feat(integrations): add IntegrationRow component"
```

---

## Task 5: IntegrationSection Component

**Files:**
- Create: `admin/src/modules/integrations/components/IntegrationSection.vue`

One category section card — header with icon + name + active count, then a 3-column grid of `IntegrationRow` items.

- [ ] **Step 1: Create the component**

```vue
<script setup lang="ts">
/**
 * Renders a category section card containing all integrations for that category.
 *
 * Header shows icon, category label, and count of active integrations.
 * Body is a 3-column grid of IntegrationRow components.
 */
import { computed } from 'vue'
import IntegrationRow from './IntegrationRow.vue'
import type { IntegrationDto } from '../types/integration.types'

/** Props for IntegrationSection. */
const props = defineProps<{
  /** Emoji icon for the category. */
  icon: string
  /** Human-readable category label. */
  label: string
  /** Integrations that belong to this category. */
  integrations: IntegrationDto[]
}>()

/** Number of enabled integrations in this section. */
const activeCount = computed(() =>
  props.integrations.filter(i => i.isEnabled).length
)
</script>

<template>
  <div class="bg-white rounded-xl border border-gray-200 overflow-hidden">
    <div class="flex items-center gap-2.5 px-4 py-3.5 border-b border-gray-100">
      <span class="text-base">{{ icon }}</span>
      <span class="font-bold text-sm text-gray-800">{{ label }}</span>
      <span class="ml-auto text-xs text-gray-400">{{ activeCount }} active</span>
    </div>

    <div class="grid grid-cols-3 divide-x divide-gray-100">
      <IntegrationRow
        v-for="integration in integrations"
        :key="integration.slug"
        :integration="integration"
      />
    </div>
  </div>
</template>
```

- [ ] **Step 2: Commit**

```bash
git add admin/src/modules/integrations/components/IntegrationSection.vue
git commit -m "feat(integrations): add IntegrationSection component"
```

---

## Task 6: IntegrationStatusSidebar Component

**Files:**
- Create: `admin/src/modules/integrations/components/IntegrationStatusSidebar.vue`

Right sidebar on the detail page — shows connection status, last tested time, and optional hint.

- [ ] **Step 1: Create the component**

```vue
<script setup lang="ts">
/**
 * Right sidebar for the integration detail page.
 *
 * Displays last connection test status and an optional contextual hint
 * (e.g. webhook URL instructions for Stripe).
 */
import type { IntegrationTestResult } from '../types/integration.types'

/** Props for IntegrationStatusSidebar. */
const props = defineProps<{
  /** ISO 8601 timestamp of last successful test, or null if never tested. */
  lastTestedAt: string | null
  /** Result of the most recent in-session test, or null. */
  testResult: IntegrationTestResult | null
  /** Optional hint text shown in a callout box. */
  hint?: string
}>()

/**
 * Formats lastTestedAt into a human-readable relative string.
 *
 * @returns Formatted string like "2 hours ago" or "Never".
 */
function formatLastTested(): string {
  if (!props.lastTestedAt) return 'Never'
  const diff = Date.now() - new Date(props.lastTestedAt).getTime()
  const minutes = Math.floor(diff / 60000)
  if (minutes < 60) return `${minutes} minute${minutes !== 1 ? 's' : ''} ago`
  const hours = Math.floor(minutes / 60)
  if (hours < 24) return `${hours} hour${hours !== 1 ? 's' : ''} ago`
  return `${Math.floor(hours / 24)} day(s) ago`
}
</script>

<template>
  <div class="flex flex-col gap-3">
    <!-- Status card -->
    <div class="bg-white rounded-xl border border-gray-200 p-4">
      <div class="font-bold text-xs text-gray-700 mb-3">Status</div>

      <!-- In-session test result -->
      <template v-if="testResult">
        <div class="flex items-center gap-2 mb-1">
          <div
            class="w-2 h-2 rounded-full flex-shrink-0"
            :class="testResult.success ? 'bg-green-500' : 'bg-red-500'"
          />
          <span class="text-xs text-gray-700">{{ testResult.message }}</span>
        </div>
      </template>

      <!-- Persisted last-tested info -->
      <template v-else>
        <div class="flex items-center gap-2 mb-1">
          <div
            class="w-2 h-2 rounded-full flex-shrink-0"
            :class="lastTestedAt ? 'bg-green-500' : 'bg-gray-300'"
          />
          <span class="text-xs text-gray-700">
            {{ lastTestedAt ? 'Connection: OK' : 'Not tested yet' }}
          </span>
        </div>
        <div class="text-xs text-gray-400">Last tested: {{ formatLastTested() }}</div>
      </template>
    </div>

    <!-- Hint callout -->
    <div v-if="hint" class="bg-yellow-50 border border-yellow-200 rounded-xl p-4">
      <div class="text-xs font-bold text-amber-800 mb-2">⚠️ Setup Note</div>
      <div class="text-xs text-amber-700 leading-relaxed">{{ hint }}</div>
    </div>
  </div>
</template>
```

- [ ] **Step 2: Commit**

```bash
git add admin/src/modules/integrations/components/IntegrationStatusSidebar.vue
git commit -m "feat(integrations): add IntegrationStatusSidebar component"
```

---

## Task 7: IntegrationConfigForm Component

**Files:**
- Create: `admin/src/modules/integrations/components/IntegrationConfigForm.vue`

Dynamic form rendered from `INTEGRATION_META[slug].fields`. Handles text, password, select, textarea inputs.

- [ ] **Step 1: Create the component**

```vue
<script setup lang="ts">
/**
 * Dynamic config form for a single integration.
 *
 * Renders fields defined in INTEGRATION_META[slug].fields.
 * Emits save and test events to the parent (IntegrationDetailView).
 */
import { ref, watch } from 'vue'
import { INTEGRATION_META } from '../types/integration.meta'
import type { IntegrationDetailDto, IntegrationConfigPayload } from '../types/integration.types'

/** Props for IntegrationConfigForm. */
const props = defineProps<{
  /** Full integration detail loaded from the store. */
  integration: IntegrationDetailDto
  /** True while a save or test request is in flight. */
  loading: boolean
}>()

/** Emits for IntegrationConfigForm. */
const emit = defineEmits<{
  /** Emitted when the admin clicks Save Changes. */
  save: [payload: IntegrationConfigPayload]
  /** Emitted when the admin clicks Test Connection. */
  test: []
}>()

/** Static metadata for this integration's fields. */
const meta = INTEGRATION_META[props.integration.slug]

/** Local copy of config values the admin is editing. */
const localConfig = ref<Record<string, string>>({ ...props.integration.config })

/** Local toggle for enabled/disabled state. */
const isEnabled = ref(props.integration.isEnabled)

/** Re-sync local state when a new integration is loaded. */
watch(
  () => props.integration,
  (next) => {
    localConfig.value = { ...next.config }
    isEnabled.value = next.isEnabled
  }
)

/**
 * Builds and emits the save payload.
 */
function handleSave(): void {
  emit('save', { isEnabled: isEnabled.value, config: { ...localConfig.value } })
}
</script>

<template>
  <div class="bg-white rounded-xl border border-gray-200 p-5">
    <!-- Header -->
    <div class="flex items-center gap-3 mb-5 pb-4 border-b border-gray-100">
      <div
        class="w-11 h-11 rounded-xl flex-shrink-0"
        :class="meta?.color ?? 'bg-gray-400'"
      />
      <div class="flex-1">
        <div class="font-bold text-base text-gray-800">{{ integration.name }}</div>
        <div class="text-xs text-gray-500">{{ integration.description }}</div>
      </div>

      <!-- Enable toggle -->
      <label class="flex items-center gap-2 cursor-pointer">
        <button
          type="button"
          class="relative w-9 h-5 rounded-full transition-colors"
          :class="isEnabled ? 'bg-green-500' : 'bg-gray-300'"
          @click="isEnabled = !isEnabled"
        >
          <span
            class="absolute top-0.5 w-4 h-4 bg-white rounded-full shadow transition-transform"
            :class="isEnabled ? 'translate-x-4' : 'translate-x-0.5'"
          />
        </button>
        <span
          class="text-xs font-semibold"
          :class="isEnabled ? 'text-green-600' : 'text-gray-400'"
        >
          {{ isEnabled ? 'Enabled' : 'Disabled' }}
        </span>
      </label>
    </div>

    <!-- Dynamic fields grid -->
    <div class="grid grid-cols-2 gap-4 mb-5">
      <div
        v-for="field in meta?.fields ?? []"
        :key="field.key"
        :class="field.type === 'textarea' ? 'col-span-2' : ''"
      >
        <label class="block text-xs font-semibold text-gray-600 mb-1">{{ field.label }}</label>

        <textarea
          v-if="field.type === 'textarea'"
          v-model="localConfig[field.key]"
          rows="3"
          class="w-full bg-gray-50 border border-gray-200 rounded-lg px-3 py-2 text-sm text-gray-700 focus:outline-none focus:ring-2 focus:ring-blue-200"
        />

        <select
          v-else-if="field.type === 'select'"
          v-model="localConfig[field.key]"
          class="w-full bg-gray-50 border border-gray-200 rounded-lg px-3 py-2 text-sm text-gray-700 focus:outline-none focus:ring-2 focus:ring-blue-200"
        >
          <option v-for="opt in field.options" :key="opt" :value="opt">{{ opt }}</option>
        </select>

        <input
          v-else
          v-model="localConfig[field.key]"
          :type="field.type"
          class="w-full bg-gray-50 border border-gray-200 rounded-lg px-3 py-2 text-sm text-gray-700 font-mono focus:outline-none focus:ring-2 focus:ring-blue-200"
        />
      </div>
    </div>

    <!-- Actions -->
    <div class="flex gap-2">
      <button
        type="button"
        class="bg-blue-700 text-white rounded-lg px-5 py-2 text-sm font-semibold hover:bg-blue-800 transition disabled:opacity-50"
        :disabled="loading"
        @click="handleSave"
      >
        Save Changes
      </button>
      <button
        type="button"
        class="bg-gray-100 text-gray-700 rounded-lg px-5 py-2 text-sm font-semibold hover:bg-gray-200 transition disabled:opacity-50"
        :disabled="loading"
        @click="emit('test')"
      >
        Test Connection
      </button>
    </div>
  </div>
</template>
```

- [ ] **Step 2: Commit**

```bash
git add admin/src/modules/integrations/components/IntegrationConfigForm.vue
git commit -m "feat(integrations): add IntegrationConfigForm component"
```

---

## Task 8: IntegrationsView (Main Page)

**Files:**
- Create: `admin/src/modules/integrations/views/IntegrationsView.vue`

- [ ] **Step 1: Create the view**

```vue
<script setup lang="ts">
/**
 * Main Apps & Integrations page.
 *
 * Renders all integration categories as section cards.
 * Email and Fraud Protection sections are shown side-by-side.
 */
import { computed, onMounted } from 'vue'
import { useIntegrationsStore } from '../stores/integrationsStore'
import IntegrationSection from '../components/IntegrationSection.vue'
import { INTEGRATION_CATEGORIES } from '../types/integration.meta'

const store = useIntegrationsStore()

onMounted(store.fetchAll)

/**
 * Returns integrations filtered by category key.
 *
 * @param category - Category key to filter by.
 * @returns Filtered array of IntegrationDto.
 */
function byCategory(category: string) {
  return store.integrations.filter(i => i.category === category)
}

/** Categories rendered as full-width sections (1 per row). */
const fullWidthCategories = computed(() =>
  INTEGRATION_CATEGORIES.filter(c => !['email', 'fraud'].includes(c.key))
)

/** Categories rendered side-by-side in a 2-column grid. */
const halfWidthCategories = computed(() =>
  INTEGRATION_CATEGORIES.filter(c => ['email', 'fraud'].includes(c.key))
)
</script>

<template>
  <div>
    <div class="mb-6">
      <h1 class="text-2xl font-bold text-gray-800">Apps & Integrations</h1>
      <p class="text-sm text-gray-500 mt-1">Manage your third-party service connections</p>
    </div>

    <div v-if="store.loading" class="text-gray-400 text-sm">Loading...</div>
    <div v-else-if="store.error" class="text-red-600 text-sm">{{ store.error }}</div>

    <template v-else>
      <!-- Full-width sections -->
      <div class="flex flex-col gap-4 mb-4">
        <IntegrationSection
          v-for="cat in fullWidthCategories"
          :key="cat.key"
          :icon="cat.icon"
          :label="cat.label"
          :integrations="byCategory(cat.key)"
        />
      </div>

      <!-- Side-by-side sections (email + fraud) -->
      <div class="grid grid-cols-2 gap-4">
        <IntegrationSection
          v-for="cat in halfWidthCategories"
          :key="cat.key"
          :icon="cat.icon"
          :label="cat.label"
          :integrations="byCategory(cat.key)"
        />
      </div>
    </template>
  </div>
</template>
```

- [ ] **Step 2: Commit**

```bash
git add admin/src/modules/integrations/views/IntegrationsView.vue
git commit -m "feat(integrations): add IntegrationsView main page"
```

---

## Task 9: IntegrationDetailView (Config Page)

**Files:**
- Create: `admin/src/modules/integrations/views/IntegrationDetailView.vue`

- [ ] **Step 1: Create the view**

```vue
<script setup lang="ts">
/**
 * Detail/config page for a single integration.
 *
 * Route: /integrations/:slug
 * Layout: breadcrumb + 2/3 config form + 1/3 status sidebar.
 */
import { onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useIntegrationsStore } from '../stores/integrationsStore'
import IntegrationConfigForm from '../components/IntegrationConfigForm.vue'
import IntegrationStatusSidebar from '../components/IntegrationStatusSidebar.vue'
import { INTEGRATION_META } from '../types/integration.meta'
import type { IntegrationConfigPayload } from '../types/integration.types'

const route = useRoute()
const store = useIntegrationsStore()

/** Slug from the current route (e.g. "stripe"). */
const slug = route.params.slug as string

onMounted(() => store.fetchOne(slug))

/** Static metadata hint for this integration. */
const hint = INTEGRATION_META[slug]?.hint

/**
 * Handles save event from IntegrationConfigForm.
 *
 * @param payload - Updated config and enabled state.
 */
async function handleSave(payload: IntegrationConfigPayload): Promise<void> {
  await store.saveConfig(slug, payload)
}

/**
 * Handles test event from IntegrationConfigForm.
 */
async function handleTest(): Promise<void> {
  await store.testConnection(slug)
}
</script>

<template>
  <div>
    <!-- Breadcrumb -->
    <div class="text-xs text-gray-500 mb-4">
      <RouterLink to="/integrations" class="text-blue-500 hover:text-blue-700">
        Apps & Integrations
      </RouterLink>
      <span class="mx-2">›</span>
      <span class="text-gray-700 font-semibold">{{ store.current?.name ?? slug }}</span>
    </div>

    <div v-if="store.loading && !store.current" class="text-gray-400 text-sm">Loading...</div>
    <div v-else-if="store.error" class="text-red-600 text-sm">{{ store.error }}</div>

    <div v-else-if="store.current" class="grid grid-cols-3 gap-4">
      <!-- Config form (2/3 width) -->
      <div class="col-span-2">
        <IntegrationConfigForm
          :integration="store.current"
          :loading="store.loading"
          @save="handleSave"
          @test="handleTest"
        />
      </div>

      <!-- Status sidebar (1/3 width) -->
      <div>
        <IntegrationStatusSidebar
          :last-tested-at="store.current.lastTestedAt"
          :test-result="store.testResult"
          :hint="hint"
        />
      </div>
    </div>
  </div>
</template>
```

- [ ] **Step 2: Commit**

```bash
git add admin/src/modules/integrations/views/IntegrationDetailView.vue
git commit -m "feat(integrations): add IntegrationDetailView config page"
```

---

## Task 10: Router + Sidebar

**Files:**
- Modify: `admin/src/router/index.ts`
- Modify: `admin/src/components/layout/AppSidebar.vue`

- [ ] **Step 1: Add routes to `admin/src/router/index.ts`**

After the `support/:id` route, add:

```typescript
{ path: 'integrations', component: () => import('../modules/integrations/views/IntegrationsView.vue') },
{ path: 'integrations/:slug', component: () => import('../modules/integrations/views/IntegrationDetailView.vue') },
```

- [ ] **Step 2: Add sidebar entry to `admin/src/components/layout/AppSidebar.vue`**

In the `navItems` array, after the `support` entry and before `settings`:

```typescript
{ to: '/integrations', label: 'Apps & Integrations' },
```

- [ ] **Step 3: Commit**

```bash
git add admin/src/router/index.ts admin/src/components/layout/AppSidebar.vue
git commit -m "feat(integrations): wire up router routes and sidebar nav"
```

---

## Task 11: Manual Smoke Test

No automated tests exist in this admin project (no test runner configured). Manual verification steps:

- [ ] **Step 1: Start the dev server**

```bash
cd admin
yarn dev
```

Expected: server starts on `http://localhost:5173` (or next available port).

- [ ] **Step 2: Verify sidebar**

Navigate to `http://localhost:5173`. Confirm "Apps & Integrations" appears in the sidebar between Support and Settings.

- [ ] **Step 3: Verify main page layout**

Click "Apps & Integrations". Confirm:
- 3 full-width section cards (Payment Gateways, Domain Registrars, Hosting / Provisioning)
- 2 side-by-side cards (Email / SMTP, Fraud Protection)
- Loading state shows while API call is in flight
- Error state shows if backend is not running

- [ ] **Step 4: Verify detail page**

Click "Configure →" on any row. Confirm:
- Breadcrumb shows "Apps & Integrations › [Name]"
- Config form renders correct fields for that integration's slug
- Enable/Disable toggle works
- Status sidebar renders (with "Never" if not tested)
- Hint callout shows for integrations that have one (e.g. stripe)

- [ ] **Step 5: Final commit**

```bash
git add -A
git commit -m "feat(integrations): complete Apps & Integrations module"
```
