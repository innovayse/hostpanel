<template>
  <span :class="badgeClasses">
    <Icon v-if="icon" :name="icon" class="mr-1" />
    <slot />
  </span>
</template>

<script setup lang="ts">
/**
 * Badge component for labels, tags, and status indicators
 */

interface Props {
  /** Color variant */
  variant?: 'default' | 'primary' | 'secondary' | 'success' | 'warning' | 'danger' | 'info'
  /** Size variant */
  size?: 'sm' | 'md' | 'lg'
  /** Icon name to display before text */
  icon?: string
  /** Rounded style */
  rounded?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  variant: 'default',
  size: 'md',
  rounded: false
})

/** Computed classes for badge */
const badgeClasses = computed(() => {
  const base = 'inline-flex items-center font-medium'

  const variants = {
    default: 'bg-gray-100 text-gray-800',
    primary: 'bg-primary-100 text-primary-800',
    secondary: 'bg-secondary-100 text-secondary-800',
    success: 'bg-green-100 text-green-800',
    warning: 'bg-yellow-100 text-yellow-800',
    danger: 'bg-red-100 text-red-800',
    info: 'bg-blue-100 text-blue-800'
  }

  const sizes = {
    sm: 'px-2 py-0.5 text-xs',
    md: 'px-2.5 py-1 text-sm',
    lg: 'px-3 py-1.5 text-base'
  }

  const rounded = props.rounded ? 'rounded-full' : 'rounded'

  return `${base} ${variants[props.variant]} ${sizes[props.size]} ${rounded}`
})
</script>
