<script setup lang="ts">
/**
 * Manage Affiliates: the affiliate programme overview.
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
    :title="t(navTitleKey(AppRoute.ClientsAffiliates))"
    :subtitle="t('clients.manageAffiliates.viewAndManageTheAffiliate')"
    :heading="t('clients.manageAffiliates.comingSoon')"
    :description="t('clients.manageAffiliates.affiliateManagementWillBeAvailable')"
    :icon="Icon.Users"
  />
</template>
