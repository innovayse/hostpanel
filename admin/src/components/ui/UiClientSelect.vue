<script setup lang="ts">
/**
 * Searchable client picker: a `UiSelect` over clients.
 *
 * The list filters by name, email or id; each row shows the account status, the name with the id
 * and the email. The model is the chosen client's id as a string, empty when nothing is chosen (the
 * first row clears the choice). With `remote` the owner looks clients up by id or name as the reader
 * types (`search`) instead of handing over every client.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiSelect from '@/components/ui/UiSelect.vue'
import type { UiSelectOption } from '@/utils/ui/select'

const { t } = useI18n()

/** One client the picker can offer. */
interface Client {
  /** Client id. */
  id: number
  /** Display name. */
  name: string
  /** Contact email, searchable. */
  email?: string
  /** Account status caption shown above the name. */
  status?: string
}

/** Props for UiClientSelect. */
const props = defineProps<{
  /**
   * Id of the selected client, or `null` when nothing is selected.
   *
   * Callers hold this as `number | null`; `null` and an unknown id both show the placeholder.
   */
  modelValue: string | number | null
  /** Clients to choose from. */
  clients: Client[]
  /** Hint shown while nothing is chosen; also the text of the row that clears the choice. */
  placeholder?: string
  /** The owner looks clients up as the reader types: the list is not filtered here and `search` is emitted. */
  remote?: boolean
  /** The owner is fetching clients. */
  loading?: boolean
  /** Name of the chosen client while it is not in `clients` (an id the owner has not loaded). */
  selectedName?: string
}>()

const emit = defineEmits<{
  /** Emitted with the chosen client's id, or an empty string when the choice is cleared. */
  'update:modelValue': [value: string]
  /** Emitted by a `remote` picker with the term to look up. */
  search: [term: string]
}>()

/** The hint text. */
const hint = computed<string>(() => props.placeholder || t('shared.selectAClient'))

/** The model as the string the options carry. */
const value = computed<string>(() => (props.modelValue === null ? '' : String(props.modelValue)))

/** The clients as listbox rows, behind the dim row that clears the choice. */
const options = computed<UiSelectOption<string>[]>(() => [
  { value: '', label: hint.value, muted: true },
  ...props.clients.map(client => ({
    value: String(client.id),
    label: client.name,
    displayLabel: `${client.name} - #${client.id}`,
    caption: client.status,
    description: client.email,
    keywords: String(client.id),
  })),
])

/**
 * Passes the chosen id up.
 *
 * @param id - The id of the picked row, empty for the clearing row.
 */
const pick = (id: string): void => {
  emit('update:modelValue', id)
}
</script>

<template>
  <UiSelect
    :model-value="value"
    :options="options"
    :placeholder="hint"
    :empty-label="t('ui.uiClientSelect.noClientsFound')"
    :value-label="selectedName"
    :remote="remote"
    :loading="loading"
    searchable
    @update:model-value="pick"
    @search="emit('search', $event)"
  />
</template>
