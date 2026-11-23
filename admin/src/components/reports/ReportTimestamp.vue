<script setup lang="ts">
/**
 * Shows "Report Generated on DD/MM/YYYY HH:MM".
 *
 * Captures the time the report appeared on screen; it does not tick.
 */
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import UiText from '@/components/ui/UiText.vue'
import { formatDateTimeCompact } from '@/lib/format'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'

const { t } = useI18n()

/** The captured time, empty until mounted. */
const timestamp = ref('')

onMounted(() => {
  const now = new Date()
  timestamp.value = formatDateTimeCompact(now.toISOString())
})
</script>

<template>
  <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Xs" class="text-right" data-testid="report-timestamp">
    {{ t('reports.reportTimestamp.reportGeneratedOnTimestamp', { timestamp }) }}
  </UiText>
</template>
