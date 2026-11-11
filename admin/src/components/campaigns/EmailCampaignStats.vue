<script setup lang="ts">
/**
 * The delivery counters of a campaign as a row of tiles: recipients in total, still pending,
 * sent, failed and unsubscribed. The failed tile reads in the danger tone while anything failed.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiStatTile from '@/components/ui/UiStatTile.vue'
import type { EmailCampaignStats } from '@/types/emailcampaign'
import { UiTone } from '@/utils/ui/tone'

const { t } = useI18n()

/** Props for EmailCampaignStats. */
const props = defineProps<{
  /** The counters to show. */
  stats: EmailCampaignStats
}>()

/** The failed tile's tone: danger once a mail failed, neutral otherwise. */
const failedTone = computed<UiTone>(() => (props.stats.failed > 0 ? UiTone.Danger : UiTone.Neutral))
</script>

<template>
  <UiGrid :columns="3" gap="md" data-testid="email-campaign-stats">
    <UiStatTile :label="t('emailCampaigns.stats.total')" :value="stats.total" data-testid="email-campaign-stat-total" />
    <UiStatTile :label="t('emailCampaigns.stats.pending')" :value="stats.pending" data-testid="email-campaign-stat-pending" />
    <UiStatTile :label="t('emailCampaigns.stats.sent')" :value="stats.sent" :tone="UiTone.Success" data-testid="email-campaign-stat-sent" />
    <UiStatTile :label="t('emailCampaigns.stats.failed')" :value="stats.failed" :tone="failedTone" data-testid="email-campaign-stat-failed" />
    <UiStatTile :label="t('emailCampaigns.stats.unsubscribed')" :value="stats.unsubscribed" data-testid="email-campaign-stat-unsubscribed" />
  </UiGrid>
</template>
