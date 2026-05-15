<template>
  <div class="relative w-full" v-click-outside="closeDropdown">
    <!-- Label -->
    <label v-if="label" class="block text-sm font-medium text-gray-300 mb-2">
      {{ label }}
      <span v-if="required" class="text-primary-400">*</span>
    </label>

    <!-- Dropdown trigger -->
    <button
      type="button"
      :class="triggerClasses"
      @click="toggleDropdown"
    >
      <!-- Icon -->
      <Icon
        v-if="icon"
        :name="icon"
        class="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400"
      />

      <!-- Selected value or placeholder -->
      <span v-if="selectedOption" class="block truncate text-left">
        {{ selectedOption.label }}
      </span>
      <span v-else class="block truncate text-left text-gray-500">
        {{ placeholder }}
      </span>

      <!-- Dropdown arrow -->
      <ChevronDown
        :size="20"
        :stroke-width="2"
        class="absolute right-3 top-1/2 -translate-y-1/2 text-gray-400 transition-transform duration-200"
        :class="{ 'rotate-180': isOpen }"
      />
    </button>

    <!-- Error message -->
    <p v-if="error" class="mt-2 text-sm text-red-400">
      {{ error }}
    </p>

    <!-- Hint text -->
    <p v-else-if="hint" class="mt-2 text-sm text-gray-500">
      {{ hint }}
    </p>

    <!-- Dropdown menu -->
    <Transition name="dropdown">
      <div
        v-show="isOpen"
        class="absolute z-50 mt-2 w-full rounded-xl bg-[#1a1a1f] border-2 border-gray-700 shadow-2xl shadow-black/50 overflow-hidden backdrop-blur-xl"
      >
        <div class="max-h-60 overflow-y-auto custom-scrollbar">
          <button
            v-for="option in options"
            :key="option.value"
            type="button"
            :class="[
              'w-full px-4 py-3 text-left text-sm transition-all duration-200 flex items-center justify-between group',
              modelValue === option.value
                ? 'bg-gradient-to-r from-primary-500 to-secondary-500 text-white font-semibold'
                : 'text-gray-300 hover:bg-white/10 hover:text-white'
            ]"
            @click="selectOption(option)"
          >
            <span>{{ option.label }}</span>
            <Check
              v-if="modelValue === option.value"
              :size="18"
              :stroke-width="2"
            />
          </button>
        </div>
      </div>
    </Transition>
  </div>
</template>

<script setup lang="ts">
/**
 * Custom Dropdown component with full style control
 * Better than native select for dark theme
 */

import { ChevronDown, Check } from 'lucide-vue-next'

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

const props = withDefaults(defineProps<Props>(), {
  size: 'md',
  placeholder: 'Select an option'
})

const emit = defineEmits<{
  'update:modelValue': [value: string | number]
}>()

const isOpen = ref(false)

const selectedOption = computed(() => {
  return props.options.find(opt => opt.value === props.modelValue)
})

const toggleDropdown = () => {
  if (!props.disabled) {
    isOpen.value = !isOpen.value
  }
}

const closeDropdown = () => {
  isOpen.value = false
}

const selectOption = (option: Option) => {
  emit('update:modelValue', option.value)
  closeDropdown()
}

const triggerClasses = computed(() => {
  const base = 'relative w-full border-2 rounded-lg transition-all duration-200 focus:outline-none focus:ring-2 text-white text-left'

  const sizes = {
    sm: 'px-3 py-1.5 text-sm',
    md: 'px-4 py-2.5 text-base',
    lg: 'px-5 py-3 text-lg'
  }

  const iconPadding = props.icon ? 'pl-10' : ''
  const rightPadding = 'pr-10'

  const state = props.error
    ? 'border-red-500/50 focus:border-red-500 focus:ring-red-500/50 bg-red-500/5'
    : isOpen.value
      ? 'border-primary-500 ring-2 ring-primary-500/50 bg-white/10'
      : 'border-gray-700 focus:border-primary-500 focus:ring-primary-500/50 bg-white/5 hover:border-primary-500/50'

  const disabled = props.disabled ? 'bg-gray-800/50 cursor-not-allowed opacity-60' : 'cursor-pointer'

  return `${base} ${sizes[props.size]} ${iconPadding} ${rightPadding} ${state} ${disabled}`
})

// Click outside directive
interface ClickOutsideElement extends HTMLElement {
  clickOutsideEvent?: (event: Event) => void
}

const vClickOutside = {
  mounted(el: ClickOutsideElement, binding: any) {
    el.clickOutsideEvent = (event: Event) => {
      if (!(el === event.target || el.contains(event.target as Node))) {
        binding.value()
      }
    }
    document.addEventListener('click', el.clickOutsideEvent)
  },
  unmounted(el: ClickOutsideElement) {
    if (el.clickOutsideEvent) {
      document.removeEventListener('click', el.clickOutsideEvent)
    }
  }
}
</script>

<style scoped>
/* Dropdown animation */
.dropdown-enter-active,
.dropdown-leave-active {
  transition: all 0.2s ease;
}

.dropdown-enter-from {
  opacity: 0;
  transform: translateY(-10px);
}

.dropdown-leave-to {
  opacity: 0;
  transform: translateY(-5px);
}

/* Custom scrollbar */
.custom-scrollbar::-webkit-scrollbar {
  width: 8px;
}

.custom-scrollbar::-webkit-scrollbar-track {
  background: rgba(255, 255, 255, 0.05);
  border-radius: 4px;
}

.custom-scrollbar::-webkit-scrollbar-thumb {
  background: linear-gradient(180deg, #0ea5e9, #a855f7);
  border-radius: 4px;
}

.custom-scrollbar::-webkit-scrollbar-thumb:hover {
  background: linear-gradient(180deg, #38bdf8, #c084fc);
}
</style>
