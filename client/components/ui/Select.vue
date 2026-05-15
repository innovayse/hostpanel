<template>
  <div class="w-full">
    <!-- Label -->
    <label v-if="label" :for="id" class="block text-sm font-medium text-gray-300 mb-2">
      {{ label }}
      <span v-if="required" class="text-primary-400">*</span>
    </label>

    <!-- Custom dropdown -->
    <div ref="wrapperRef" class="relative">
      <!-- Trigger button -->
      <button
        :id="id"
        type="button"
        :disabled="disabled"
        :class="triggerClasses"
        @click="toggle"
        @keydown.escape="close"
        @keydown.enter.prevent="toggle"
        @keydown.space.prevent="toggle"
        @keydown.arrow-down.prevent="open"
        @keydown.arrow-up.prevent="open"
      >
        <Icon v-if="icon" :name="icon" class="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 pointer-events-none z-10" />
        <span :class="selectedOption ? 'text-gray-900 dark:text-white' : 'text-gray-400 dark:text-gray-500'">
          {{ selectedOption?.label ?? placeholder ?? $t('ui.selectPlaceholder') }}
        </span>
        <ChevronDown
          :size="18"
          :stroke-width="2"
          class="absolute right-3 top-1/2 -translate-y-1/2 text-gray-400 transition-transform duration-200"
          :class="isOpen ? 'rotate-180' : ''"
        />
      </button>

      <!-- Dropdown list -->
      <Transition
        enter-active-class="transition duration-150 ease-out"
        enter-from-class="opacity-0 translate-y-1 scale-95"
        enter-to-class="opacity-100 translate-y-0 scale-100"
        leave-active-class="transition duration-100 ease-in"
        leave-from-class="opacity-100 translate-y-0 scale-100"
        leave-to-class="opacity-0 translate-y-1 scale-95"
      >
        <ul
          v-if="isOpen"
          role="listbox"
          class="absolute z-50 mt-1.5 w-full rounded-xl border border-gray-200 dark:border-white/15 bg-white dark:bg-[#0d0d14] shadow-xl shadow-black/10 dark:shadow-black/40 overflow-hidden py-1"
        >
          <li
            v-for="option in options"
            :key="option.value"
            role="option"
            :aria-selected="modelValue === option.value"
            class="flex items-center gap-2 px-4 py-2.5 text-sm cursor-pointer transition-colors duration-150 select-none"
            :class="modelValue === option.value
              ? 'bg-primary-50 dark:bg-primary-500/20 text-primary-600 dark:text-primary-300'
              : 'text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-white/10 hover:text-gray-900 dark:hover:text-white'"
            @click="select(option)"
          >
            <Check
              v-if="modelValue === option.value"
              :size="14"
              :stroke-width="2.5"
              class="text-primary-400 flex-shrink-0"
            />
            <span :class="modelValue !== option.value ? 'ml-[22px]' : ''">{{ option.label }}</span>
          </li>
          <li v-if="!options.length" class="px-4 py-2.5 text-sm text-gray-500 text-center">
            {{ $t('ui.noOptions') }}
          </li>
        </ul>
      </Transition>
    </div>

    <p v-if="error" class="mt-2 text-sm text-red-400">{{ error }}</p>
    <p v-else-if="hint" class="mt-2 text-sm text-gray-500">{{ hint }}</p>
  </div>
</template>

<script setup lang="ts">
import { ChevronDown, Check } from 'lucide-vue-next'
import { onClickOutside } from '@vueuse/core'

interface Option {
  label: string
  value: string | number
}

interface Props {
  id?: string
  label?: string
  modelValue?: string | number
  placeholder?: string
  options: Option[]
  required?: boolean
  disabled?: boolean
  error?: string
  hint?: string
  icon?: string
  size?: 'sm' | 'md' | 'lg'
}

const props = withDefaults(defineProps<Props>(), { size: 'md' })

const emit = defineEmits<{
  'update:modelValue': [value: string | number]
}>()

const isOpen = ref(false)
const wrapperRef = ref<HTMLElement | null>(null)

onClickOutside(wrapperRef, () => close())

const selectedOption = computed(() =>
  props.options.find(o => o.value === props.modelValue) ?? null
)

function toggle() {
  if (props.disabled) return
  isOpen.value = !isOpen.value
}
function open() {
  if (!props.disabled) isOpen.value = true
}
function close() {
  isOpen.value = false
}
function select(option: Option) {
  emit('update:modelValue', option.value)
  close()
}

const triggerClasses = computed(() => {
  const base = 'relative block w-full text-left border rounded-lg transition-all duration-200 focus:outline-none focus:ring-2 cursor-pointer pr-10'

  const sizes: Record<string, string> = {
    sm: 'px-3 py-1.5 text-sm',
    md: 'px-4 py-2.5 text-base',
    lg: 'px-5 py-3 text-lg'
  }

  const iconPadding = props.icon ? 'pl-10' : ''

  const state = props.error
    ? 'border-red-400 dark:border-red-500/50 focus:border-red-500 focus:ring-red-500/50 bg-red-50 dark:bg-red-500/5'
    : isOpen.value
      ? 'border-primary-500 ring-2 ring-primary-500/50 bg-white dark:bg-white/5'
      : 'border-gray-300 dark:border-white/10 hover:border-gray-400 dark:hover:border-white/20 bg-white dark:bg-white/5 focus:border-primary-500 focus:ring-primary-500/50'

  const disabledClass = props.disabled ? 'cursor-not-allowed opacity-60' : ''

  return `${base} ${sizes[props.size]} ${iconPadding} ${state} ${disabledClass}`
})
</script>
