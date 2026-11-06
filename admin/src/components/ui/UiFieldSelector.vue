<script setup lang="ts">
/**
 * Dropdown of checkboxes for choosing visible fields (columns).
 *
 * The compact trigger shows how many fields are selected. The parent owns the selection: this
 * component only reports `toggle`, `selectAll` and `clearAll`.
 */
import { ref, computed, onMounted, onUnmounted } from 'vue'
import UiTransition from '@/components/ui/UiTransition.vue'
import { UiTransitionKind } from '@/utils/ui/transition'
import { useI18n } from 'vue-i18n'
import UiCheckbox from '@/components/ui/UiCheckbox.vue'

const { t } = useI18n()

/** Props for UiFieldSelector. */
const props = defineProps<{
  /**
   * All available fields.
   *
   * `readonly` because this component only reads the list: every caller declares its
   * columns with `as const`, and a mutable annotation rejected those literals outright.
   */
  fields: readonly { key: string; label: string }[]
  /** Currently selected field keys. */
  selected: Set<string>
}>()

const emit = defineEmits<{
  /** Emitted with a field's key when its checkbox is toggled. */
  toggle: [key: string]
  /** Emitted when "Select all" is pressed. */
  selectAll: []
  /** Emitted when "Clear all" is pressed. */
  clearAll: []
}>()

/** Whether the dropdown is open. */
const open = ref(false)

/** Root element, used for outside-click detection. */
const root = ref<HTMLElement | null>(null)

/** Number of selected fields. */
const selectedCount = computed<number>(() => props.selected.size)

/** Number of available fields. */
const totalCount = computed<number>(() => props.fields.length)

/**
 * Closes the dropdown when the pointer goes down outside the component.
 *
 * @param e - The mouse event.
 */
const onOutsideClick = (e: MouseEvent): void => {
  if (root.value && !root.value.contains(e.target as Node)) {
    open.value = false
  }
}

onMounted(() => document.addEventListener('mousedown', onOutsideClick))
onUnmounted(() => document.removeEventListener('mousedown', onOutsideClick))
</script>

<template>
  <div ref="root" class="relative">
    <!-- Trigger -->
    <button
      type="button"
      data-testid="field-selector-trigger"
      :aria-expanded="open"
      class="w-full flex items-center gap-2 bg-surface-field border border-border rounded-[10px] px-3 py-2 text-[0.82rem] text-text-secondary hover:text-text-primary hover:border-text-secondary outline-none focus-visible:border-primary-500/50 focus-visible:ring-2 focus-visible:ring-primary-500/30 transition-colors"
      @click="open = !open"
    >
      <svg class="w-4 h-4 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
        <rect x="3" y="3" width="7" height="7" /><rect x="14" y="3" width="7" height="7" /><rect x="3" y="14" width="7" height="7" /><rect x="14" y="14" width="7" height="7" />
      </svg>
      <span class="flex-1 text-left">{{ t('shared.fieldsToInclude') }}</span>
      <span v-if="selectedCount > 0" class="text-[0.68rem] bg-primary-500/20 text-primary-600 light:text-primary-700 px-1.5 py-0.5 rounded-full font-medium shrink-0">{{ selectedCount }}/{{ totalCount }}</span>
      <svg class="w-3.5 h-3.5 text-text-secondary shrink-0 transition-transform duration-150 ml-auto" :class="open ? 'rotate-180' : ''" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><polyline points="6 9 12 15 18 9" /></svg>
    </button>

    <!-- Dropdown -->
    <UiTransition :kind="UiTransitionKind.Dropdown">
      <div v-if="open" class="absolute z-50 mt-1.5 left-0 bg-surface-card border border-border rounded-[10px] shadow-xl w-[320px] overflow-hidden">
        <!-- Select all / clear all -->
        <div class="flex items-center justify-between px-3 py-2 border-b border-border bg-surface-field">
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-secondary">{{ t('ui.uiFieldSelector.fields') }}</span>
          <div class="flex gap-2">
            <button type="button" data-testid="field-selector-select-all" class="text-[0.72rem] text-primary-600 light:text-primary-700 hover:text-primary-500 rounded outline-none focus-visible:ring-2 focus-visible:ring-primary-500/50 transition-colors" @click="emit('selectAll')">
              {{ t('shared.selectAll') }}
            </button>
            <span class="text-border-strong">|</span>
            <button type="button" data-testid="field-selector-clear-all" class="text-[0.72rem] text-text-secondary hover:text-text-primary rounded outline-none focus-visible:ring-2 focus-visible:ring-primary-500/50 transition-colors" @click="emit('clearAll')">
              {{ t('ui.uiFieldSelector.clearAll') }}
            </button>
          </div>
        </div>

        <!-- Checkbox grid -->
        <div class="p-3 overflow-y-auto max-h-[260px]">
          <div class="grid grid-cols-2 gap-x-4 gap-y-1.5">
            <label
              v-for="field in fields"
              :key="field.key"
              class="flex items-center gap-1.5 text-[0.78rem] text-text-secondary cursor-pointer select-none hover:text-text-primary transition-colors py-0.5"
            >
              <UiCheckbox
                :model-value="selected.has(field.key)"
                :data-testid="`field-selector-field-${field.key}`"
                @update:model-value="emit('toggle', field.key)"
              />
              {{ field.label }}
            </label>
          </div>
        </div>

        <!-- Apply -->
        <div class="px-3 pb-3 pt-1 border-t border-border flex justify-end">
          <button
            type="button"
            data-testid="field-selector-apply"
            class="px-3 py-1 gradient-brand text-white text-[0.72rem] font-semibold rounded-[7px] outline-none transition-opacity hover:opacity-90 focus-visible:ring-2 focus-visible:ring-primary-500/50"
            @click="open = false"
          >
            {{ t('shared.apply') }}
          </button>
        </div>
      </div>
    </UiTransition>
  </div>
</template>
