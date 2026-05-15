<template>
  <component
    :is="tag"
    v-bind="tagAttrs"
    :class="buttonClasses"
  >
    <Loader2 v-if="loading" :size="16" :stroke-width="2" class="animate-spin mr-2" />
    <Icon v-else-if="icon" :name="icon" class="mr-2" />
    <slot />
  </component>
</template>

<script setup lang="ts">
import { Loader2 } from 'lucide-vue-next'

interface Props {
  variant?: 'primary' | 'secondary' | 'outline' | 'ghost' | 'subtle' | 'danger'
  size?: 'sm' | 'md' | 'lg'
  type?: 'button' | 'submit' | 'reset'
  disabled?: boolean
  loading?: boolean
  icon?: string
  fullWidth?: boolean
  /** Internal route — renders as NuxtLink */
  to?: string
  /** External URL — renders as <a> */
  href?: string
  target?: string
  rel?: string
}

const props = withDefaults(defineProps<Props>(), {
  variant: 'primary',
  size: 'md',
  type: 'button',
  disabled: false,
  loading: false,
  fullWidth: false
})

const tag = computed(() => {
  if (props.to) return resolveComponent('NuxtLink')
  if (props.href) return 'a'
  return 'button'
})

const tagAttrs = computed(() => {
  if (props.to)   return { to: props.to }
  if (props.href) return { href: props.href, target: props.target, rel: props.rel }
  return { type: props.type, disabled: props.disabled || props.loading }
})

// No defineEmits needed — @click falls through naturally via inheritAttrs to the root element

const buttonClasses = computed(() => {
  const base = 'inline-flex items-center justify-center font-medium rounded-lg transition-all duration-200 focus:outline-none focus:ring-2 focus:ring-offset-2 disabled:opacity-50 disabled:cursor-not-allowed'

  const variants = {
    primary:   'bg-primary-600 text-white hover:bg-primary-700 active:bg-primary-800 focus:ring-primary-500',
    secondary: 'bg-gray-200 text-gray-800 hover:bg-gray-300 active:bg-gray-400 focus:ring-gray-500',
    outline:   'border-2 border-primary-600 text-primary-600 hover:bg-primary-500/10 active:bg-primary-500/20 focus:ring-primary-500',
    ghost:     'text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-white/10 active:bg-gray-200 dark:active:bg-white/[0.15] focus:ring-gray-500',
    subtle:    'bg-gray-100 dark:bg-white/5 border border-gray-200 dark:border-white/10 text-gray-600 dark:text-gray-300 hover:bg-gray-200 dark:hover:bg-white/10 active:bg-gray-300 dark:active:bg-white/[0.15] focus:ring-gray-400 dark:focus:ring-gray-600',
    danger:    'bg-red-600 text-white hover:bg-red-700 active:bg-red-800 focus:ring-red-500'
  }

  const sizes = {
    sm: 'px-3 py-1.5 text-sm',
    md: 'px-4 py-2 text-base',
    lg: 'px-6 py-3 text-lg'
  }

  const width = props.fullWidth ? 'w-full' : ''
  return `${base} ${variants[props.variant]} ${sizes[props.size]} ${width}`
})
</script>
