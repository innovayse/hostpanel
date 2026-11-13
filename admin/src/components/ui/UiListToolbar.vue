<script setup lang="ts" generic="K extends string">
/**
 * The toolbar every list shares: a search box, the "Filters" button that opens the filter fields (the default
 * slot) and, under them, the chips of the filters that are applied.
 *
 * Each part appears only when the screen uses it: the search box when `v-model:search` is bound, the Filters
 * button when the default slot holds fields. A list with only a status filter, or only a search, shows just that.
 *
 * It holds no state of its own. The search text is the screen's, and the filter draft, the applied filters and
 * the chips come from the screen's `useFilterQuery`; this component only lays them out and passes the reader's
 * choices on. It is the one place the arrangement is written, so lists cannot drift apart. While `loading` it
 * keeps the same layout with the search box and the Filters button disabled.
 */
import { useI18n } from 'vue-i18n'
import UiFilterChips from '@/components/ui/UiFilterChips.vue'
import UiFilterTrigger from '@/components/ui/UiFilterTrigger.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { Icon } from '@/lib/icons'
import type { UiFilterChip } from '@/utils/ui/filter'

const { t } = useI18n()

/** Props for UiListToolbar. */
const props = withDefaults(defineProps<{
  /** How many filters are applied; the Filters button shows it. */
  activeCount?: number
  /** The applied filters, one chip each. */
  chips?: UiFilterChip<K>[]
  /** Test id of the search box. */
  searchTestid?: string
  /** Placeholder of the search box (already translated); defaults to a translated "Search". */
  searchPlaceholder?: string
  /** Test id of the Filters button. */
  filterTestid?: string
  /** Keeps the layout and disables the search box and the Filters button while the list loads. */
  loading?: boolean
}>(), {
  activeCount: 0,
  filterTestid: 'filter-trigger',
  loading: false,
  /**
   * Applied-filter chips default to none.
   *
   * @returns A fresh empty array per instance.
   */
  chips: () => [],
})

/** The search text, two-way bound with `v-model:search`; the search box shows only when it is bound. */
const search = defineModel<string>('search')

const emit = defineEmits<{
  /** Emitted when the reader applies the filter draft. */
  apply: []
  /** Emitted when the reader resets the filter draft. */
  reset: []
  /** Emitted when the reader cancels the filter draft. */
  cancel: []
  /** Emitted with the key of the chip whose remove button was pressed. */
  remove: [key: K]
  /** Emitted when "Clear all" is pressed. */
  clear: []
}>()
</script>

<template>
  <UiStack gap="sm">
    <UiStack direction="row" gap="sm" align="center" wrap>
      <div v-if="search !== undefined" class="min-w-0 grow basis-56 sm:max-w-sm">
        <UiInput
          v-model="search"
          type="search"
          :placeholder="searchPlaceholder ?? t('shared.search')"
          :disabled="loading"
          :data-testid="searchTestid"
        >
          <template #icon>
            <UiIcon :markup="Icon.Search" size="sm" />
          </template>
        </UiInput>
      </div>
      <UiFilterTrigger
        v-if="$slots.default"
        :count="props.activeCount"
        :disabled="loading"
        :data-testid="filterTestid"
        @apply="emit('apply')"
        @reset="emit('reset')"
        @cancel="emit('cancel')"
      >
        <slot />
      </UiFilterTrigger>
    </UiStack>
    <UiFilterChips :chips="props.chips" @remove="emit('remove', $event)" @clear="emit('clear')" />
  </UiStack>
</template>
