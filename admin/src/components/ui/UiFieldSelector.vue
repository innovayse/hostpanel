<script setup lang="ts">
/**
 * Dropdown of checkboxes for choosing visible fields (columns).
 *
 * The compact trigger shows, on one line, how many fields are selected (the caption while none is). The parent owns the selection: this
 * component only reports `toggle`, `selectAll` and `clearAll`. The panel is drawn in the page body (a teleport) and
 * placed under the trigger with fixed positioning, so no card around the trigger can paint over it; on a phone (below
 * `sm`) it is a bottom sheet ({@link UiFloatingSurface}).
 */
import { computed, useId } from 'vue'
import { useI18n } from 'vue-i18n'
import UiCheckbox from '@/components/ui/UiCheckbox.vue'
import UiFloatingSurface from '@/components/ui/UiFloatingSurface.vue'
import UiLabel from '@/components/ui/UiLabel.vue'
import { useDropdown } from '@/composables/useDropdown'
import { fieldSizeClasses } from '@/utils/ui/size'
import { fieldBaseClasses } from '@/utils/ui/input'

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

/** Open flag, outside-pointer and Escape handling shared with the other floating lists. */
const { open, root, trigger, panel, toggle: togglePanel, hide, onEscape } = useDropdown()

/** Free room below the trigger, in pixels, under which the panel opens above it. */
const PANEL_MIN_ROOM_PX = 360

/** Gap between the trigger and the panel, in pixels. */
const PANEL_GAP_PX = 6

/** Width of the popover in pixels (its class is `w-[320px]`). */
const PANEL_WIDTH_PX = 320

/** Shape of the popover: the kit's card look at a fixed width (the place and the surface are the floating surface's). */
const panelClasses = 'rounded-[10px] w-[320px] overflow-hidden'

/** Trigger classes: the kit's md field with a quiet label that brightens on hover. */
const triggerClasses = `w-full flex items-center gap-2 ${fieldBaseClasses} ${fieldSizeClasses.md} text-text-secondary hover:text-text-primary hover:border-text-secondary`

/** Id of the panel, named by the trigger's `aria-controls`. */
const panelId = useId()

/** Number of selected fields. */
const selectedCount = computed<number>(() => props.selected.size)

/** Number of available fields. */
const totalCount = computed<number>(() => props.fields.length)

/** Text of the trigger: the count of chosen fields, or the caption while none is chosen; one line, cut with an ellipsis. */
const summary = computed<string>(() =>
  selectedCount.value > 0
    ? t('ui.uiFieldSelector.selectedCount', { count: selectedCount.value, total: totalCount.value })
    : t('shared.fieldsToInclude'),
)
</script>

<template>
  <div ref="root" class="relative" @keydown="onEscape">
    <!-- Trigger -->
    <button
      ref="trigger"
      type="button"
      data-testid="field-selector-trigger"
      aria-haspopup="true"
      :aria-expanded="open"
      :aria-controls="open ? panelId : undefined"
      :class="triggerClasses"
      @click="togglePanel"
    >
      <svg class="w-4 h-4 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
        <rect x="3" y="3" width="7" height="7" /><rect x="14" y="3" width="7" height="7" /><rect x="3" y="14" width="7" height="7" /><rect x="14" y="14" width="7" height="7" />
      </svg>
      <span class="flex-1 min-w-0 truncate whitespace-nowrap text-left" data-testid="field-selector-summary">{{ summary }}</span>
      <svg class="w-3.5 h-3.5 text-text-secondary shrink-0 transition-transform duration-150 ml-auto" :class="open ? 'rotate-180' : ''" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><polyline points="6 9 12 15 18 9" /></svg>
    </button>

    <!-- The panel: a popover in the page body (no card around the trigger can paint over it) or a bottom sheet on a phone -->
    <UiFloatingSurface
      :open="open"
      :anchor="trigger"
      :title="t('shared.fieldsToInclude')"
      :min-room="PANEL_MIN_ROOM_PX"
      :gap="PANEL_GAP_PX"
      :keep-inside-width="PANEL_WIDTH_PX"
      :popover-class="panelClasses"
      :id="panelId"
      role="group"
      :aria-label="t('shared.fieldsToInclude')"
      @close="hide(true)"
      @panel="panel = $event"
      @keydown="onEscape"
    >
      <!-- Select all / clear all -->
      <div class="flex items-center justify-between px-3 py-2 border-b border-border bg-surface-field">
        <UiLabel variant="field" as="span">{{ t('ui.uiFieldSelector.fields') }}</UiLabel>
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
          class="h-8 px-3.5 gradient-brand text-text-on-primary text-[0.8125rem] font-semibold rounded-[9px] outline-none transition-opacity hover:opacity-90 focus-visible:ring-2 focus-visible:ring-text-accent"
          @click="hide(true)"
        >
          {{ t('shared.apply') }}
        </button>
      </div>
    </UiFloatingSurface>
  </div>
</template>
