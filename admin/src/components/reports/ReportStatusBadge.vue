<script setup lang="ts">
/**
 * Badge for a status word a report endpoint sent (Active, Overdue and so on), toned by its meaning.
 */
import { computed } from 'vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import { reportStatusLabel, reportStatusTone } from '@/lib/tones'
import type { UiTone } from '@/utils/ui/tone'

/** Props for ReportStatusBadge. */
const props = defineProps<{
  /** The status word as the endpoint sent it. */
  status: string
}>()

/** Tone of the badge. */
const tone = computed<UiTone>(() => reportStatusTone(props.status))
</script>

<template>
  <UiBadge :tone="tone" small data-testid="report-status">
    {{ reportStatusLabel(status) }}
  </UiBadge>
</template>
