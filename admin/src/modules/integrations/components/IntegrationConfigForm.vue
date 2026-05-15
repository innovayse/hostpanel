<script setup lang="ts">
/**
 * Dynamic config form for a single integration.
 *
 * Renders fields defined in INTEGRATION_META[slug].fields.
 * Emits save and test events to the parent.
 */
import { ref, watch } from 'vue'
import { INTEGRATION_META } from '../types/integration.meta'
import type { IntegrationDetailDto, IntegrationConfigPayload, IntegrationSlug } from '../types/integration.types'

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
const meta = INTEGRATION_META[props.integration.slug as IntegrationSlug]

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
  },
  { deep: true }
)

/**
 * Builds and emits the save payload.
 */
function handleSave(): void {
  emit('save', { isEnabled: isEnabled.value, config: { ...localConfig.value } })
}
</script>

<template>
  <div class="bg-surface-card border border-border rounded-2xl overflow-hidden">

    <!-- Card header: logo + name + toggle -->
    <div class="flex items-center gap-3.5 px-5 py-4 border-b border-border">
      <div
        class="w-10 h-10 rounded-xl shrink-0"
        :class="meta?.color ?? 'bg-text-muted'"
      />
      <div class="flex-1 min-w-0">
        <div class="font-display font-semibold text-[0.95rem] text-text-primary">{{ integration.name }}</div>
        <div class="text-[0.76rem] text-text-muted truncate">{{ integration.description }}</div>
      </div>

      <!-- Enable/disable toggle -->
      <label class="flex items-center gap-2.5 cursor-pointer shrink-0">
        <button
          type="button"
          class="relative w-9 h-5 rounded-full transition-colors duration-200 focus:outline-none"
          :class="isEnabled ? 'bg-primary-500' : 'bg-border'"
          @click="isEnabled = !isEnabled"
        >
          <span
            class="absolute top-0.5 w-4 h-4 bg-white rounded-full shadow transition-transform duration-200"
            :class="isEnabled ? 'translate-x-4' : 'translate-x-0.5'"
          />
        </button>
        <span
          class="text-[0.75rem] font-semibold"
          :class="isEnabled ? 'text-status-green' : 'text-text-muted'"
        >
          {{ isEnabled ? 'Enabled' : 'Disabled' }}
        </span>
      </label>
    </div>

    <!-- Dynamic fields -->
    <div class="p-5">
      <div class="grid grid-cols-1 sm:grid-cols-2 gap-4 mb-5">
        <div
          v-for="field in props.integration.fieldDefinitions ?? []"
          :key="field.key"
          :class="field.type === 'textarea' ? 'sm:col-span-2' : ''"
        >
          <label class="block text-[0.72rem] font-semibold uppercase tracking-[0.06em] text-text-muted mb-1.5">
            {{ field.label }}
            <span v-if="field.required" class="text-status-red ml-0.5">*</span>
          </label>

          <textarea
            v-if="field.type === 'textarea'"
            v-model="localConfig[field.key]"
            rows="3"
            class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-sm text-text-primary placeholder:text-text-muted outline-none transition-all duration-150 focus:border-primary-500/50 focus:bg-primary-500/[0.03] focus:ring-2 focus:ring-primary-500/10 resize-none"
          />

          <select
            v-else-if="field.type === 'select'"
            v-model="localConfig[field.key]"
            class="w-full bg-surface-elevated border border-border rounded-[10px] px-3 py-2.5 text-sm text-text-primary outline-none transition-all duration-150 focus:border-primary-500/50 focus:ring-2 focus:ring-primary-500/10 appearance-none cursor-pointer"
          >
            <option v-for="opt in field.options" :key="opt" :value="opt" class="bg-surface-elevated">{{ opt }}</option>
          </select>

          <input
            v-else
            v-model="localConfig[field.key]"
            :type="field.type"
            class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-sm text-text-primary font-mono placeholder:text-text-muted outline-none transition-all duration-150 focus:border-primary-500/50 focus:bg-primary-500/[0.03] focus:ring-2 focus:ring-primary-500/10"
          />
        </div>
      </div>

      <!-- Actions -->
      <div class="flex items-center gap-2.5 pt-1 border-t border-border">
        <button
          type="button"
          class="gradient-brand text-white rounded-[10px] px-5 py-2 text-[0.85rem] font-semibold transition-all duration-150 hover:-translate-y-px disabled:opacity-50 disabled:translate-y-0 disabled:cursor-not-allowed"
          style="box-shadow: 0 3px 14px rgba(14,165,233,0.2);"
          :disabled="loading"
          @click="handleSave"
        >
          {{ loading ? 'Saving…' : 'Save Changes' }}
        </button>
        <button
          type="button"
          class="bg-white/[0.05] border border-border text-text-secondary rounded-[10px] px-5 py-2 text-[0.85rem] font-semibold transition-all duration-150 hover:text-text-primary hover:border-primary-500/30 hover:bg-primary-500/[0.05] disabled:opacity-50 disabled:cursor-not-allowed"
          :disabled="loading"
          @click="emit('test')"
        >
          Test Connection
        </button>
      </div>
    </div>

  </div>
</template>
