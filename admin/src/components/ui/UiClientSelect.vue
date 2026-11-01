<script setup lang="ts">
/**
 * Searchable client picker.
 *
 * A text field that filters the given clients by name, email or id and drops down a list to pick
 * from. The model is the chosen client's id as a string, empty when the field is cleared.
 */
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useI18n } from 'vue-i18n'
import UiPanelTransition from '@/components/ui/UiPanelTransition.vue'

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
   * Callers hold this as `number | null`; `String(null)` simply matches no client, which is
   * how the placeholder is shown.
   */
  modelValue: string | number | null
  /** Clients to choose from. */
  clients: Client[]
  /** Hint shown while the field is empty. */
  placeholder?: string
}>()

const emit = defineEmits<{
  /** Emitted with the chosen client's id, or an empty string when the field is cleared. */
  'update:modelValue': [value: string]
}>()

/** Whether the dropdown list is open. */
const isOpen = ref(false)

/** Text typed into the field. */
const searchQuery = ref('')

/** Root element, used for outside-click detection. */
const inputEl = ref<HTMLDivElement | null>(null)

/** The text input, focused when the picker opens. */
const inputField = ref<HTMLInputElement | null>(null)

/** The client the model points at, if it is in the list. */
const selectedClient = computed<Client | undefined>(() => {
  const id = String(props.modelValue)
  return props.clients.find(c => String(c.id) === id)
})

/** Text shown in the field: the search term while open, otherwise the chosen name. */
const displayValue = computed<string>(() => {
  if (isOpen.value) {
    return searchQuery.value
  }
  return selectedClient.value?.name ?? ''
})

/** Clients matching the search term by name, email or id. */
const filteredClients = computed<Client[]>(() => {
  if (!searchQuery.value) {
    return props.clients
  }
  const query = searchQuery.value.toLowerCase()
  return props.clients.filter(client =>
    client.name.toLowerCase().includes(query)
    || client.email?.toLowerCase().includes(query)
    || client.id.toString().includes(query),
  )
})

/**
 * Picks a client and closes the list.
 *
 * @param client - The chosen client.
 */
const selectClient = (client: Client): void => {
  emit('update:modelValue', String(client.id))
  isOpen.value = false
  searchQuery.value = ''
}

/**
 * Tracks typing; clears the selection when the field is emptied.
 *
 * @param e - The input event.
 */
const handleInputChange = (e: Event): void => {
  const value = (e.target as HTMLInputElement).value
  searchQuery.value = value
  if (!value && props.modelValue) {
    emit('update:modelValue', '')
  }
}

/** Opens the list; with a chosen client and an empty search it pre-fills the name. */
const handleFocus = (): void => {
  isOpen.value = true
  if (selectedClient.value && !searchQuery.value) {
    searchQuery.value = selectedClient.value.name
  }
  inputField.value?.focus()
}

/**
 * Closes the list on a click outside the component.
 *
 * @param e - The click event.
 */
const handleClickOutside = (e: MouseEvent): void => {
  if (inputEl.value && !inputEl.value.contains(e.target as Node)) {
    isOpen.value = false
  }
}

onMounted(() => {
  document.addEventListener('click', handleClickOutside)
})

onUnmounted(() => {
  document.removeEventListener('click', handleClickOutside)
})
</script>

<template>
  <div ref="inputEl" class="relative">
    <!-- Search input -->
    <input
      ref="inputField"
      :value="displayValue"
      type="text"
      :placeholder="placeholder || t('shared.selectAClient')"
      class="w-full px-3 py-2 text-[0.82rem] text-text-primary bg-surface-field border border-border rounded-[9px] placeholder:text-text-muted outline-none focus:border-primary-500/50 focus:ring-2 focus:ring-primary-500/10 transition-colors"
      @input="handleInputChange"
      @click="handleFocus"
      @focus="handleFocus"
    >

    <!-- Dropdown list -->
    <UiPanelTransition>
      <div
        v-if="isOpen"
        class="absolute z-50 top-full left-0 right-0 mt-2 bg-surface-card border border-border rounded-[9px] shadow-lg overflow-hidden"
      >
        <div class="max-h-64 overflow-y-auto">
          <div v-if="filteredClients.length === 0" class="p-4 text-center text-text-secondary text-[0.82rem]">
            {{ t('ui.uiClientSelect.noClientsFound') }}
          </div>
          <div
            v-for="client in filteredClients"
            :key="client.id"
            :data-testid="`client-select-option-${client.id}`"
            class="px-4 py-3 cursor-pointer transition-colors border-b border-border last:border-0 hover:bg-surface-hover"
            :class="String(modelValue) === String(client.id) ? 'bg-primary-500/10 border-l-2 border-l-primary-500' : ''"
            @click="selectClient(client)"
          >
            <div v-if="client.status" class="text-[0.68rem] font-semibold text-text-secondary mb-1">
              {{ client.status }}
            </div>
            <div class="text-[0.82rem] font-medium text-text-primary">
              {{ client.name }} - #{{ client.id }}
            </div>
            <div v-if="client.email" class="text-[0.75rem] text-text-secondary mt-0.5">
              {{ client.email }}
            </div>
          </div>
        </div>
      </div>
    </UiPanelTransition>
  </div>
</template>
