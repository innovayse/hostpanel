<script setup lang="ts" generic="K extends string">
/**
 * The active filters of a list, each as a removable chip ("Status: Active" with a remove button), and a
 * "Clear all" link once there is more than one. Renders nothing while no filter is active.
 *
 * The chip only reports what the reader asked for (`remove` with the chip's key, `clear`); the screen clears the
 * filter, which changes the chips. Pair it with `UiFilterTrigger`, which opens the filters themselves.
 */
import { useI18n } from 'vue-i18n'
import UiButton from '@/components/ui/UiButton.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import { Icon } from '@/lib/icons'
import type { UiFilterChip } from '@/utils/ui/filter'

const { t } = useI18n()

/** Props for UiFilterChips. */
defineProps<{
  /** The active filters, one chip each, in the order they are shown. */
  chips: UiFilterChip<K>[]
}>()

const emit = defineEmits<{
  /** Emitted with the key of the chip whose remove button was pressed. */
  remove: [key: K]
  /** Emitted when "Clear all" is pressed. */
  clear: []
}>()
</script>

<template>
  <ul
    v-if="chips.length > 0"
    :aria-label="t('ui.uiFilterChips.label')"
    class="flex flex-wrap items-center gap-2"
    data-testid="filter-chips"
  >
    <li v-for="chip in chips" :key="chip.key" class="max-w-full">
      <span
        class="inline-flex max-w-full items-center gap-1 rounded-pill border border-border bg-surface-field py-0.5 pl-3 pr-1 text-[0.75rem] text-text-secondary"
        :data-testid="`filter-chip-${chip.key}`"
      >
        <span class="truncate">
          <span class="font-medium text-text-primary">{{ chip.label }}:</span>
          {{ chip.value }}
        </span>
        <UiIconButton
          :label="t('ui.uiFilterChips.remove', { label: chip.label, value: chip.value })"
          size="sm"
          :data-testid="`filter-chip-remove-${chip.key}`"
          @click="emit('remove', chip.key)"
        >
          <UiIcon :markup="Icon.Close" size="sm" />
        </UiIconButton>
      </span>
    </li>
    <li v-if="chips.length > 1">
      <UiButton variant="link" size="sm" data-testid="filter-chips-clear" @click="emit('clear')">
        {{ t('ui.uiFilterChips.clearAll') }}
      </UiButton>
    </li>
  </ul>
</template>
