<template>
  <div class="w-full">
    <div class="border-b border-gray-200 dark:border-white/10">
      <nav class="-mb-px flex space-x-8 overflow-x-auto" role="tablist" :aria-label="ariaLabel ?? $t('ui.tabs')">
        <button
          v-for="tab in tabs"
          :key="tab.value"
          type="button"
          role="tab"
          :aria-selected="modelValue === tab.value"
          :data-testid="`ui-tab-${tab.value}`"
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
/**
 * UiTabs: a tab bar with an optional panel slot that receives the active tab value. The bar scrolls sideways when
 * the tabs do not fit the width, so none is cut off on a phone.
 */

/** One tab. */
interface Tab {
  /** Value emitted and compared against `modelValue`. */
  value: string
  /** Text shown. */
  label: string
  /** Name of a leading icon (Nuxt Icon). */
  icon?: string
  /** Count or short text shown in a badge. */
  badge?: string | number
}

/** Props of UiTabs. */
interface Props {
  /** The tabs. */
  tabs: Tab[]
  /** Active tab value (v-model). */
  modelValue: string
  /** Accessible name of the tab list; defaults to the translated "Tabs". */
  ariaLabel?: string
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  ariaLabel: undefined,
})

/** Emits the component raises. */
const emit = defineEmits<{
  /** The reader chose another tab. */
  'update:modelValue': [value: string]
}>()

/**
 * Emits the chosen tab.
 *
 * @param value Value of the clicked tab.
 */
const selectTab = (value: string): void => {
  emit('update:modelValue', value)
}

/**
 * Class list of a tab button.
 *
 * @param value Value of the tab.
 * @returns Active or idle classes.
 */
const getTabClasses = (value: string): string => {
  const base = 'ui-tab inline-flex shrink-0 items-center whitespace-nowrap border-b-2 py-4 px-1 text-sm font-medium transition-colors rounded-t-md focus:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-primary-500'
  return props.modelValue === value
    ? `${base} ui-tab-active border-primary-500 text-primary-600 dark:text-primary-400`
    : `${base} border-transparent text-gray-500 dark:text-gray-400 hover:border-gray-300 dark:hover:border-white/20 hover:text-gray-900 dark:hover:text-white`
}
</script>
