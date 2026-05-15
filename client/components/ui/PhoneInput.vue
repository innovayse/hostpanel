<template>
  <div class="w-full">
    <label v-if="label" :for="id" class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
      {{ label }}
      <span v-if="required" class="text-primary-500">*</span>
    </label>

    <div class="relative phone-input-container" :class="{ 'has-error': error }">
      <ClientOnly>
        <vue-tel-input
          v-bind="bindProps"
          :model-value="modelValue"
          @on-input="handleTelInput"
        />
      </ClientOnly>
      
      <AlertCircle
        v-if="error"
        :size="20"
        :stroke-width="2"
        class="absolute right-3 top-1/2 -translate-y-1/2 text-red-500 pointer-events-none z-10"
      />
    </div>

    <p v-if="error" class="mt-2 text-sm text-red-500 dark:text-red-400">{{ error }}</p>
    <p v-else-if="hint" class="mt-2 text-sm text-gray-500">{{ hint }}</p>
  </div>
</template>

<script setup lang="ts">
import { VueTelInput } from 'vue-tel-input'
import 'vue-tel-input/vue-tel-input.css'
import { AlertCircle } from 'lucide-vue-next'

interface Props {
  id?: string
  label?: string
  modelValue?: string
  required?: boolean
  disabled?: boolean
  error?: string
  hint?: string
  size?: 'sm' | 'md' | 'lg'
}

const props = withDefaults(defineProps<Props>(), {
  size: 'md'
})

const emit = defineEmits(['update:modelValue', 'onInput'])

const handleTelInput = (number: string, object: any) => {
  const value = (object && object.number) ? object.number : number
  emit('update:modelValue', value)
  emit('onInput', object)
}

const bindProps = computed(() => ({
  mode: 'international' as 'international',
  disabled: props.disabled,
  placeholder: 'Enter phone number',
  required: props.required,
  dropdownOptions: {
    showSearchBox: true,
    showFlags: true,
    showDialCodeInList: true,
    showDialCodeInSelection: false
  },
  inputOptions: {
    id: props.id,
    showDialCode: true,
    placeholder: '00 000 000'
  },
  autoDefaultCountry: true,
  defaultCountry: 'AM' // Set default as Armenia
}))

</script>

<style>
.phone-input-container .vue-tel-input {
  @apply border rounded-lg transition-all duration-200 bg-white dark:bg-white/5 border-gray-300 dark:border-gray-700 !important;
  height: 44px; /* Default height */
}

.phone-input-container.has-error .vue-tel-input {
  @apply border-red-400 dark:border-red-500/50 bg-red-50 dark:bg-red-500/5 !important;
}

.phone-input-container .vue-tel-input:focus-within {
  @apply ring-2 ring-primary-500/50 border-primary-500 outline-none !important;
}

.phone-input-container .vti__input {
  @apply bg-transparent text-gray-900 dark:text-white placeholder:text-gray-400 dark:placeholder:text-gray-500 focus:outline-none !important;
}

.phone-input-container .vti__dropdown {
  @apply rounded-l-lg hover:bg-gray-50 dark:hover:bg-white/10 transition-colors !important;
}

.phone-input-container .vti__dropdown-list {
  @apply bg-[#1a1a24] border border-white/10 rounded-xl shadow-[0_-20px_50px_rgba(0,0,0,0.5)] z-[9999] overflow-hidden !important;
  width: 280px;
  top: auto !important;
  bottom: 100% !important;
  margin-bottom: 8px !important;
  margin-top: 0 !important;
}

.phone-input-container .vti__dropdown-item {
  @apply text-gray-700 dark:text-gray-300 py-2 !important;
}

.phone-input-container .vti__dropdown-item.highlighted {
  @apply bg-primary-500/10 text-primary-500 !important;
}

.phone-input-container .vti__selection {
  @apply text-gray-900 dark:text-white text-sm font-medium !important;
}

/* Size modifications */
.phone-input-container .vti__input {
  font-size: 14px;
}
</style>
