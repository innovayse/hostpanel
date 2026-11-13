<script setup lang="ts">
/**
 * Badge showing a ticket's priority, coloured by the priority and labelled in the operator's
 * language. Shared by the tickets list and the ticket detail page, beside the status badge.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiBadge from '@/components/ui/UiBadge.vue'
import { ticketPriorityTone } from '@/lib/tones'
import type { TicketPriority } from '@/types/ticket'

/** Props for SupportTicketPriorityBadge. */
const props = defineProps<{
  /** The priority to show. */
  priority: TicketPriority
}>()

const { t } = useI18n()

/** Translated label of the priority. */
const label = computed<string>(() => t(`ticketPriority.${props.priority}`))
</script>

<template>
  <UiBadge :tone="ticketPriorityTone[priority]">{{ label }}</UiBadge>
</template>
