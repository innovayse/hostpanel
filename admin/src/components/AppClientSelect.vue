<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'

interface Client {
  id: number
  name: string
  email?: string
  status?: string
}

const props = defineProps<{
  modelValue: string | number
  clients: Client[]
  placeholder?: string
}>()

const emit = defineEmits<{
  'update:modelValue': [value: string]
}>()

const isOpen = ref(false)
const searchQuery = ref('')
const inputEl = ref<HTMLDivElement | null>(null)
const inputField = ref<HTMLInputElement | null>(null)

const displayValue = computed(() => {
  if (isOpen.value) {
    return searchQuery.value
  }
  return selectedClient.value?.name ?? ''
})

const filteredClients = computed(() => {
  if (!searchQuery.value) return props.clients
  const query = searchQuery.value.toLowerCase()
  return props.clients.filter(client =>
    client.name.toLowerCase().includes(query) ||
    client.email?.toLowerCase().includes(query) ||
    client.id.toString().includes(query)
  )
})

const selectedClient = computed(() => {
  const id = String(props.modelValue)
  return props.clients.find(c => String(c.id) === id)
})

function selectClient(client: Client) {
  emit('update:modelValue', String(client.id))
  isOpen.value = false
  searchQuery.value = ''
}

function handleInputChange(e: Event) {
  const value = (e.target as HTMLInputElement).value
  searchQuery.value = value

  // Clear selection when field becomes empty
  if (!value && props.modelValue) {
    emit('update:modelValue', '')
  }
}

function handleFocus() {
  isOpen.value = true
  // If there's a selected client and search is empty, fill it with client name
  if (selectedClient.value && !searchQuery.value) {
    searchQuery.value = selectedClient.value.name
  }
  inputField.value?.focus()
}

function handleClickOutside(e: MouseEvent) {
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
  <div class="relative" ref="inputEl">
    <!-- Search Input -->
    <input
      ref="inputField"
      :value="displayValue"
      @input="handleInputChange"
      type="text"
      :placeholder="placeholder || 'Select a client...'"
      @click="handleFocus"
      @focus="handleFocus"
      class="w-full px-3 py-2 text-[0.82rem] text-text-primary bg-white/[0.05] border border-border rounded-[9px] placeholder:text-text-muted focus:outline-none focus:border-primary-500/40 transition-colors"
    />

    <!-- Dropdown Menu -->
    <div
      v-if="isOpen"
      class="absolute z-50 top-full left-0 right-0 mt-2 bg-surface-card border border-border rounded-[9px] shadow-lg overflow-hidden"
    >

      <!-- Clients List -->
      <div class="max-h-64 overflow-y-auto">
        <div v-if="filteredClients.length === 0" class="p-4 text-center text-text-muted text-[0.82rem]">
          No clients found
        </div>
        <div
          v-for="client in filteredClients"
          :key="client.id"
          @click="selectClient(client)"
          :class="[
            'px-4 py-3 cursor-pointer transition-colors border-b border-border last:border-0 hover:bg-white/[0.05]',
            String(modelValue) === String(client.id) ? 'bg-primary-500/10 border-l-2 border-l-primary-500' : ''
          ]"
        >
          <div class="flex items-start justify-between">
            <div class="flex-1">
              <div v-if="client.status" class="text-[0.68rem] font-semibold text-text-muted mb-1">{{ client.status }}</div>
              <div class="text-[0.82rem] font-medium text-text-primary">
                {{ client.name }} - #{{ client.id }}
              </div>
              <div v-if="client.email" class="text-[0.75rem] text-text-muted mt-0.5">
                {{ client.email }}
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
