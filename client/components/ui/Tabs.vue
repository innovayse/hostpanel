<template>
  <div class="w-full">
    <div class="border-b border-gray-200 dark:border-white/10">
      <nav class="-mb-px flex space-x-8" :aria-label="ariaLabel">
        <button
          v-for="tab in tabs"
          :key="tab.value"
          type="button"
          :class="getTabClasses(tab.value)"
          @click="selectTab(tab.value)"
        >
          <Icon v-if="tab.icon" :name="tab.icon" class="mr-2" />
          {{ tab.label }}
          <UiBadge v-if="tab.badge" :variant="modelValue === tab.value ? 'primary' : 'default'" size="sm" class="ml-2">
            {{ tab.badge }}
          </UiBadge>
        </button>
      </nav>
    </div>
    <div v-if="$slots.default" class="mt-6">
      <slot :active-tab="modelValue" />
    </div>
  </div>
</template>

<script setup lang="ts">
interface Tab {
  value: string
  label: string
  icon?: string
  badge?: string | number
}

interface Props {
  tabs: Tab[]
  modelValue: string
  ariaLabel?: string
}

const props = withDefaults(defineProps<Props>(), { ariaLabel: 'Tabs' })

const emit = defineEmits<{ 'update:modelValue': [value: string] }>()

const selectTab = (value: string) => emit('update:modelValue', value)

const getTabClasses = (value: string) => {
  const base = 'inline-flex items-center whitespace-nowrap border-b-2 py-4 px-1 text-sm font-medium transition-colors'
  return props.modelValue === value
    ? `${base} border-primary-500 text-primary-500 dark:text-primary-400`
    : `${base} border-transparent text-gray-500 dark:text-gray-400 hover:border-gray-300 dark:hover:border-white/20 hover:text-gray-900 dark:hover:text-white`
}
</script>
