<template>
  <div class="flex items-start gap-3 p-4 rounded-xl border" :class="styles.wrap">
    <component :is="icon" :size="iconSize" :stroke-width="2" class="flex-shrink-0 mt-0.5" :class="styles.icon" />
    <div class="flex-1 min-w-0">
      <p v-if="title" class="font-semibold text-sm" :class="styles.title">{{ title }}</p>
      <div class="text-sm" :class="[styles.body, title ? 'mt-0.5' : '']">
        <slot />
      </div>
    </div>
    <slot name="action" />
  </div>
</template>

<script setup lang="ts">
import { AlertTriangle, AlertCircle, Info, CheckCircle } from 'lucide-vue-next'

interface Props {
  variant?: 'warning' | 'error' | 'info' | 'success'
  title?: string
  iconSize?: number
}

const props = withDefaults(defineProps<Props>(), {
  variant: 'warning',
  iconSize: 18,
})

const variantMap = {
  warning: {
    wrap:  'bg-yellow-50 dark:bg-yellow-500/10 border-yellow-300 dark:border-yellow-500/30',
    icon:  'text-yellow-500 dark:text-yellow-400',
    title: 'text-yellow-800 dark:text-yellow-300',
    body:  'text-yellow-700 dark:text-yellow-400/80',
  },
  error: {
    wrap:  'bg-red-50 dark:bg-red-500/10 border-red-200 dark:border-red-500/20',
    icon:  'text-red-500 dark:text-red-400',
    title: 'text-red-700 dark:text-red-400',
    body:  'text-red-600 dark:text-red-400/80',
  },
  info: {
    wrap:  'bg-blue-50 dark:bg-blue-500/10 border-blue-200 dark:border-blue-500/20',
    icon:  'text-blue-500 dark:text-blue-400',
    title: 'text-blue-700 dark:text-blue-300',
    body:  'text-blue-600 dark:text-blue-400/80',
  },
  success: {
    wrap:  'bg-green-50 dark:bg-green-500/10 border-green-200 dark:border-green-500/20',
    icon:  'text-green-500 dark:text-green-400',
    title: 'text-green-700 dark:text-green-300',
    body:  'text-green-600 dark:text-green-400/80',
  },
}

const iconMap = {
  warning: AlertTriangle,
  error:   AlertTriangle,
  info:    Info,
  success: CheckCircle,
}

const styles = computed(() => variantMap[props.variant])
const icon   = computed(() => iconMap[props.variant])
</script>
