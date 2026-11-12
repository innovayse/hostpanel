<script setup lang="ts">
/**
 * Picker of a client group, shared by the clients list filter, the add client form and the client
 * profile. It reads the groups from the client groups store (loaded when it mounts) and offers the
 * caller's own "no group" row first, since what that row means differs per screen (Any, Default
 * group, No group). `data-testid` and classes land on the select.
 */
import { computed, onMounted } from 'vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import { useClientGroupsStore } from '@/stores/clientGroups'

/** Props for ClientGroupSelect. */
const props = defineProps<{
  /** The chosen group id; null for the empty row (two-way bound with `v-model`). */
  modelValue: number | null
  /** Label of the empty row, already translated. */
  emptyLabel: string
  /** Text shown in the trigger while nothing is selected. */
  placeholder?: string
  /** Id of the trigger, so a `UiField` caption can point at it. */
  id?: string
}>()

const emit = defineEmits<{
  /** Emitted with the picked group id, or null for the empty row. */
  'update:modelValue': [value: number | null]
}>()

/** Client groups store, the only source of the options. */
const store = useClientGroupsStore()

/** The empty row followed by one row per group. */
const options = computed<{ value: number | null; label: string }[]>(() => [
  { value: null, label: props.emptyLabel },
  ...store.groups.map((group) => ({ value: group.id, label: group.name })),
])

onMounted(() => store.fetchAll())
</script>

<template>
  <UiSelect
    :id="id"
    :model-value="modelValue"
    :options="options"
    :placeholder="placeholder ?? emptyLabel"
    @update:model-value="emit('update:modelValue', $event)"
  />
</template>
