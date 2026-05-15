<template>
  <!-- Status badge used across the client area for services, domains, and invoices -->
  <span
    class="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-semibold border"
    :class="badgeClasses"
  >
    {{ label }}
  </span>
</template>

<script setup lang="ts">
interface Props {
  /** WHMCS status string: Active, Pending, Suspended, Terminated, Unpaid, Paid, Overdue, Cancelled, Open, Answered, Closed */
  status: string
}

const props = defineProps<Props>()

const CONFIG: Record<string, { classes: string; label?: string }> = {
  Active:      { classes: 'bg-green-500/15 text-green-700 dark:text-green-400 border-green-500/25' },
  Paid:        { classes: 'bg-green-500/15 text-green-700 dark:text-green-400 border-green-500/25' },
  Open:        { classes: 'bg-cyan-500/15 text-cyan-700 dark:text-cyan-400 border-cyan-500/25' },
  Answered:    { classes: 'bg-blue-500/15 text-blue-700 dark:text-blue-400 border-blue-500/25' },
  Pending:     { classes: 'bg-yellow-500/15 text-yellow-700 dark:text-yellow-400 border-yellow-500/25' },
  Unpaid:      { classes: 'bg-yellow-500/15 text-yellow-700 dark:text-yellow-400 border-yellow-500/25' },
  Overdue:     { classes: 'bg-red-500/15 text-red-700 dark:text-red-400 border-red-500/25' },
  Suspended:   { classes: 'bg-red-500/15 text-red-700 dark:text-red-400 border-red-500/25' },
  Terminated:  { classes: 'bg-gray-500/20 text-gray-600 dark:text-gray-400 border-gray-500/25', label: 'Ended' },
  Cancelled:   { classes: 'bg-gray-500/20 text-gray-600 dark:text-gray-400 border-gray-500/25' },
  Closed:      { classes: 'bg-gray-500/20 text-gray-600 dark:text-gray-400 border-gray-500/25' },
  Expired:     { classes: 'bg-red-500/15 text-red-700 dark:text-red-400 border-red-500/25' },
  Grace:       { classes: 'bg-orange-500/15 text-orange-700 dark:text-orange-400 border-orange-500/25', label: 'Grace Period' },
  Redemption:  { classes: 'bg-red-500/15 text-red-700 dark:text-red-400 border-red-500/25' }
}

const cfg = computed(() => CONFIG[props.status] ?? { classes: 'bg-gray-100 dark:bg-white/10 text-gray-600 dark:text-gray-400 border-gray-200 dark:border-white/15' })
const badgeClasses = computed(() => cfg.value.classes)
const label = computed(() => cfg.value.label ?? props.status)
</script>
