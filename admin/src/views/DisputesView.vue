<script setup lang="ts">
/**
 * Disputes: payment disputes raised by cardholders.
 *
 * The feature is not built yet, so the screen says so instead of showing records that do not exist.
 */
import { useI18n } from 'vue-i18n'
import { navTitleKey } from '@/lib/navigation'
import { AppRoute } from '@/lib/routes'
import ComingSoonPage from '@/components/layout/ComingSoonPage.vue'
import { Icon } from '@/lib/icons'

const { t } = useI18n()
</script>

<template>
  <ComingSoonPage
    :title="t(navTitleKey(AppRoute.BillingDisputes))"
    :subtitle="t('billing.disputes.aDisputeOccursWhenA')"
    :heading="t('billing.disputes.comingSoon')"
    :description="t('billing.disputes.comingSoonDescription')"
    :icon="Icon.Alert"
  />
</template>
