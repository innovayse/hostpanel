<template>
  <UiStack
    v-if="servicesNeedingSetup.length > 0 || unpaidInvoices.length > 0 || expiringDomains.length > 0"
    gap="md"
    data-testid="dashboard-alerts"
  >
    <!-- Services requiring setup -->
    <UiAlert v-if="servicesNeedingSetup.length > 0" variant="info" :title="setupTitle" data-testid="dashboard-alert-setup">
      {{ $t('client.dashboard.setupAlertSubtitle') }}
      <template #action>
        <UiButton size="sm" variant="subtle" :to="CLIENT_SERVICES_PATH" data-testid="dashboard-alert-setup-action">
          <ArrowRight :size="13" :stroke-width="2" class="mr-1" />
          {{ $t('client.dashboard.setupAlertAction') }}
        </UiButton>
      </template>
    </UiAlert>

    <!-- Unpaid invoices -->
    <UiAlert v-if="unpaidInvoices.length > 0" :title="unpaidTitle" data-testid="dashboard-alert-unpaid">
      {{ $t('client.dashboard.unpaidSubtitle') }}
      <template #action>
        <UiButton size="sm" variant="subtle" :to="CLIENT_INVOICES_PATH" data-testid="dashboard-alert-unpaid-action">
          <ArrowRight :size="13" :stroke-width="2" class="mr-1" />
          {{ $t('common.view') }}
        </UiButton>
      </template>
    </UiAlert>

    <!-- Domains expiring soon -->
    <UiCard
      v-if="!domainsLoading && expiringDomains.length > 0"
      class="border-l-4 !border-l-yellow-400 dark:!border-l-yellow-500"
      data-testid="dashboard-alert-expiring"
    >
      <UiCardHeader :title="$t('client.dashboard.expiringSoonTitle')" :icon="Globe">
        <UiButton size="sm" variant="subtle" :to="CLIENT_DOMAINS_PATH" data-testid="dashboard-alert-expiring-action">
          <RefreshCw :size="12" :stroke-width="2" class="mr-1" />
          {{ $t('client.dashboard.expiringSoonRenew') }}
        </UiButton>
      </UiCardHeader>
      <UiText size="sm" tone="muted">
        {{ expiringDomains.length === 1
          ? $t('client.dashboard.expiringSoonSingle')
          : $t('client.dashboard.expiringSoonMultiple', { count: expiringDomains.length }) }}
      </UiText>
    </UiCard>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * The attention banners under the dashboard counters: services waiting for setup, unpaid
 * invoices and domains close to expiry. Renders nothing when there is nothing to flag.
 */
import { CLIENT_DOMAINS_PATH, CLIENT_INVOICES_PATH, CLIENT_SERVICES_PATH } from '~/constants/clientRoutes'
import { ArrowRight, Globe, RefreshCw } from 'lucide-vue-next'
import { useClientStore } from '~/stores/client'
import { useDashboardSummary } from '~/composables/useDashboardSummary'

/** The lists behind the three banners, derived from the client store. */
const { servicesNeedingSetup, unpaidInvoices, expiringDomains } = useDashboardSummary()

/** Translator for the strings this file builds itself. */
const { t } = useI18n()
/** Client store: the signed-in person and their services, invoices, domains and tickets. */
const store = useClientStore()

/** Whether the domains are still loading; the expiry card waits for them. */
const domainsLoading = computed(() => store.domainsLoading)

/** Title of the setup banner, singular or plural. */
const setupTitle = computed(() => {
  const count = servicesNeedingSetup.value.length
  return count === 1
    ? t('client.dashboard.setupAlertSingle')
    : t('client.dashboard.setupAlertMultiple', { count })
})

/** Title of the unpaid banner, singular or plural. */
const unpaidTitle = computed(() => {
  const count = unpaidInvoices.value.length
  return count === 1
    ? t('client.dashboard.unpaidSingle')
    : t('client.dashboard.unpaidMultiple', { count })
})
</script>
