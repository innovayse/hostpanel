<script setup lang="ts">
/**
 * Reusable advanced filter rows (up to 5).
 * Each row: label on top, field + condition + value below.
 * Emits the active filters array on any change.
 */
import { ref, watch } from 'vue'
import AppSelect from './AppSelect.vue'

export interface FilterRow {
  field: string
  condition: 'exact' | 'contains'
  value: string
}

export interface FieldOption {
  value: string
  label: string
}

const props = defineProps<{
  /** Available fields for the field dropdown. */
  fields: FieldOption[]
  /** Maximum number of filter rows. */
  max?: number
}>()

const emit = defineEmits<{
  'update:filters': [filters: FilterRow[]]
}>()

const maxFilters = props.max ?? 5

const filters = ref<FilterRow[]>([
  { field: '', condition: 'exact', value: '' },
])

const fieldOptions = [{ value: '', label: 'None' }, ...props.fields]

const conditionOptions = [
  { value: 'exact', label: 'Exact Match' },
  { value: 'contains', label: 'Containing' },
]

function addFilter() {
  if (filters.value.length < maxFilters) {
    filters.value.push({ field: '', condition: 'exact', value: '' })
  }
}

function removeFilter(i: number) {
  filters.value.splice(i, 1)
  if (filters.value.length === 0) {
    filters.value.push({ field: '', condition: 'exact', value: '' })
  }
}

watch(filters, (val) => {
  emit('update:filters', val.filter(f => f.field && f.value))
}, { deep: true })
</script>

<template>
  <div class="space-y-3">
    <div
      v-for="(f, i) in filters"
      :key="i"
      class="bg-white/[0.02] border border-border/50 rounded-xl p-3"
    >
      <!-- Header: label + remove -->
      <div class="flex items-center justify-between mb-2">
        <span class="text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Filter {{ i + 1 }}</span>
        <button
          v-if="filters.length > 1"
          type="button"
          class="w-6 h-6 flex items-center justify-center text-text-muted hover:text-status-red transition-colors rounded-md hover:bg-white/[0.04]"
          @click="removeFilter(i)"
        >
          <svg class="w-3 h-3" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><line x1="18" y1="6" x2="6" y2="18" /><line x1="6" y1="6" x2="18" y2="18" /></svg>
        </button>
      </div>

      <!-- Controls: field + condition + value in even grid -->
      <div class="grid grid-cols-3 gap-3">
        <div>
          <AppSelect v-model="f.field" :options="fieldOptions" placeholder="Select field..." />
        </div>
        <div>
          <AppSelect v-model="f.condition" :options="conditionOptions" />
        </div>
        <div>
          <input
            v-model="f.value"
            type="text"
            placeholder="Enter value..."
            class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2 text-[0.82rem] text-text-primary placeholder-text-muted focus:outline-none focus:border-primary-500/50 transition-colors"
          />
        </div>
      </div>
    </div>

    <!-- Add filter button -->
    <button
      v-if="filters.length < maxFilters"
      type="button"
      class="flex items-center gap-1.5 text-[0.78rem] text-primary-400 hover:text-primary-300 transition-colors px-1"
      @click="addFilter"
    >
      <svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="12" y1="5" x2="12" y2="19" /><line x1="5" y1="12" x2="19" y2="12" /></svg>
      Add Filter
    </button>
  </div>
</template>
