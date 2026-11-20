<script setup lang="ts">
/**
 * Advanced filter rows (up to a maximum, five by default).
 *
 * Each row is a field, a condition (exact / containing) and a value. Emits the rows that are
 * complete (field and value set) whenever any of them changes. `defaultField` starts the first
 * row on that field instead of "None". `initial` starts the rows from filters already applied, for a screen that mounts the
 * editor again (a phone's filter sheet closes and opens) and must show what is in force.
 */
import { ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import UiButton from '@/components/ui/UiButton.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiLabel from '@/components/ui/UiLabel.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import { Icon } from '@/lib/icons'
import { nextRowKey } from '@/lib/rowKey'
import { UiFilterCondition } from '@/utils/ui/filter'
import { UiActionKind } from '@/utils/ui/actions'

const { t } = useI18n()

/** One filter row. */
export interface FilterRow {
  /** Key of the field being filtered; empty while unchosen. */
  field: string
  /** How the value is matched. */
  condition: UiFilterCondition
  /** Value to match. */
  value: string
}

/** A filter row as the editor holds it: the row plus a stable key for list rendering. */
interface FilterDraft extends FilterRow {
  /** Stable key of the row; never emitted. */
  key: number
}

/** One field the filters can target. */
export interface FieldOption {
  /** Field key sent to the screen. */
  value: string
  /** Caption shown in the dropdown. */
  label: string
}

/** Props for UiAdvancedFilters. */
const props = defineProps<{
  /** Available fields for the field dropdown. */
  fields: FieldOption[]
  /** Maximum number of filter rows. */
  max?: number
  /** Key of the field the first row starts on; "None" when left out. */
  defaultField?: string
  /** Filters already in force; the rows start from them. */
  initial?: readonly FilterRow[]
}>()

const emit = defineEmits<{
  /** Emitted with the complete rows on every change. */
  'update:filters': [filters: FilterRow[]]
}>()

/** Row limit, fixed when the component is created. */
const maxFilters = props.max ?? 5

/**
 * An empty row with a fresh key.
 *
 * @param field - Key of the field the row starts on; none by default.
 * @returns The empty row.
 */
const blankFilter = (field = ''): FilterDraft => ({ key: nextRowKey(), field, condition: UiFilterCondition.Exact, value: '' })

/** The rows being edited; there is always at least one, the first on the default field unless filters are already in force. */
const filters = ref<FilterDraft[]>(
  props.initial !== undefined && props.initial.length > 0
    ? props.initial.map((row) => ({ ...row, key: nextRowKey() }))
    : [blankFilter(props.defaultField)],
)

/** Options of the field dropdown: an empty "none" entry plus the caller's fields. */
const fieldOptions = [{ value: '', label: t('shared.none') }, ...props.fields]

/** Options of the condition dropdown. */
const conditionOptions = [
  { value: UiFilterCondition.Exact, label: t('ui.uiAdvancedFilters.exactMatch') },
  { value: UiFilterCondition.Contains, label: t('ui.uiAdvancedFilters.containing') },
]

/** Appends an empty row while below the limit. */
const addFilter = (): void => {
  if (filters.value.length < maxFilters) {
    filters.value.push(blankFilter())
  }
}

/**
 * Removes a row, keeping one empty row when the last is removed.
 *
 * @param i - Index of the row to remove.
 */
const removeFilter = (i: number): void => {
  filters.value.splice(i, 1)
  if (filters.value.length === 0) {
    filters.value.push(blankFilter())
  }
}

watch(filters, (val) => {
  emit('update:filters', val.filter(f => f.field && f.value).map(({ field, condition, value }) => ({ field, condition, value })))
}, { deep: true })
</script>

<template>
  <div class="space-y-3">
    <div
      v-for="(f, i) in filters"
      :key="f.key"
      class="bg-surface-field border border-border rounded-control p-3"
    >
      <!-- Header: label + remove -->
      <div class="flex items-center justify-between mb-2">
        <UiLabel variant="field" as="span">{{ t('ui.uiAdvancedFilters.filterValue', { value: i + 1 }) }}</UiLabel>
        <UiIconButton
          v-if="filters.length > 1"
          :label="t('shared.delete')"
          size="sm"
          :data-testid="`advanced-filters-remove-${i}`"
          @click="removeFilter(i)"
        >
          <UiIcon :markup="Icon.Close" size="sm" />
        </UiIconButton>
      </div>

      <!-- Controls: one column on a phone, field + condition + value side by side from sm up -->
      <div class="grid grid-cols-1 gap-3 sm:grid-cols-3">
        <UiSelect v-model="f.field" :options="fieldOptions" :placeholder="t('ui.uiAdvancedFilters.selectField')" />
        <UiSelect v-model="f.condition" :options="conditionOptions" :placeholder="t('ui.uiAdvancedFilters.exactMatch')" />
        <UiInput
          v-model="f.value"
          :placeholder="t('ui.uiAdvancedFilters.enterValue')"
          :data-testid="`advanced-filters-value-${i}`"
        />
      </div>
    </div>

    <!-- Add filter -->
    <UiButton
      v-if="filters.length < maxFilters"
      :action="UiActionKind.Add"
      variant="link"
      size="sm"
      data-testid="advanced-filters-add"
      @click="addFilter"
    >
      {{ t('ui.uiAdvancedFilters.addFilter') }}
    </UiButton>
  </div>
</template>
