<template>
  <div :class="cardClasses">
    <slot name="header" />
    <h3
      v-if="title"
      class="text-sm font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wider mb-5"
    >
      {{ title }}
    </h3>
    <slot />
    <div v-if="$slots.footer" class="border-t border-gray-200 dark:border-white/10 pt-4 mt-4">
      <slot name="footer" />
    </div>
  </div>
</template>

<script setup lang="ts">
interface Props {
  title?: string
  padding?: 'none' | 'sm' | 'md' | 'lg' | 'xl'
  hover?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  padding: 'lg',
  hover: false
})

const cardClasses = computed(() => {
  const base = 'rounded-2xl bg-white dark:bg-white/5 border border-gray-200 dark:border-white/10'
  const paddings: Record<string, string> = {
    none: '',
    sm:   'p-4',
    md:   'p-5',
    lg:   'p-6',
    xl:   'p-8'
  }
  const hoverClass = props.hover
    ? 'transition-colors duration-200 hover:border-gray-300 dark:hover:border-white/20'
    : ''
  return [base, paddings[props.padding], hoverClass].filter(Boolean).join(' ')
})
</script>
