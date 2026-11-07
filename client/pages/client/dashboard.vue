<template>
  <UiStack gap="lg" data-testid="client-dashboard">
    <!-- Page header. The test id sits on the header so the e2e flow can wait for the page. -->
    <UiPageHeader
      :title="$t('client.dashboard.title')"
      :description="$t('client.dashboard.subtitle')"
      class="!mb-0"
      data-testid="dashboard-title"
    >
      <template #actions>
        <UiButton size="sm" variant="subtle" :loading="refreshing" data-testid="dashboard-refresh" @click="refresh">
          <RefreshCw v-if="!refreshing" :size="13" :stroke-width="2" class="mr-1.5" />
          {{ $t('client.dashboard.refresh') }}
        </UiButton>
      </template>
    </UiPageHeader>

    <!-- Not a customer account: a staff identity signed into the client portal. The API
         said so with a code, so this is one explanation with somewhere to go, not four red
         alerts about a failure that never happened. -->
    <ClientNoProfileNotice v-if="store.clientProfileMissing" />

    <!-- Whatever the API said about the sections that did not load. Shown above the counts,
         because the counts below are zeros in exactly this case and reading them as "nothing
         here yet" is the misunderstanding this banner exists to prevent. -->
    <UiAlert v-if="loadErrors.length" variant="error" data-testid="dashboard-load-errors">
      <UiText v-for="message in loadErrors" :key="message" as="p" size="sm">{{ message }}</UiText>
    </UiAlert>

    <ClientDashboardStatGrid :loading="statsLoading" />

    <ClientDashboardAlerts />

    <ClientDashboardCreditCard v-if="store.mayDo(Permission.ViewPayInvoices)" />

    <UiGrid cols="2" gap="lg">
      <ClientDashboardServicesCard v-if="store.mayDo(Permission.ViewProductsServices)" />
      <ClientDashboardInvoicesCard v-if="store.mayDo(Permission.ViewPayInvoices)" />
    </UiGrid>

    <UiGrid cols="2" gap="lg">
      <ClientDashboardDomainsCard v-if="store.mayDo(Permission.ViewDomains)" />
      <ClientDashboardTicketsCard v-if="store.mayDo(Permission.ViewOpenSupportTickets)" />
    </UiGrid>

    <UiGrid cols="2" gap="lg">
      <ClientDashboardNewsCard />
      <ClientDashboardRegisterDomainCard v-if="store.mayDo(Permission.PlaceNewOrders)" />
    </UiGrid>

    <!-- Cards plugins add; read after the page mounts, nothing at all without plugins. -->
    <PluginSlot :slot-id="PluginSlotId.DashboardCards" />
  </UiStack>
</template>

<script setup lang="ts">
/**
 * Client dashboard: the account at a glance, with counters, attention banners, the credit balance, and cards for
 * services, invoices, domains, tickets, news and a quick domain search, then the cards plugins add.
 *
 * The page loads the data through the stores and lays the cards out; each card reads its own
 * slice from the store, and the derived lists live in {@link useDashboardSummary}.
 */
import { RefreshCw } from 'lucide-vue-next'
import { Permission } from '~/types/permission'
import { useClientStore } from '~/stores/client'
import { useContentStore } from '~/stores/content'
import { PluginSlotId } from '~/types/pluginslotid'

definePageMeta({ layout: 'client', middleware: 'client-auth' })

/** Client store: the signed-in person and their services, invoices, domains and tickets. */
const store = useClientStore()
/** Content store: the announcements the dashboard shows. */
const contentStore = useContentStore()

await useAsyncData('client-dashboard', () => store.fetchAll())
await useAsyncData('client-dashboard-news', () => contentStore.loadRecentNews())

/** Counter loading flag and the de-duplicated load errors, derived from the store. */
const { statsLoading, loadErrors } = useDashboardSummary()

/** True while a manual refresh is in flight. */
const refreshing = ref(false)

/**
 * Reloads every dashboard section, bypassing the store's cache.
 *
 * @returns Nothing.
 */
const refresh = async (): Promise<void> => {
  refreshing.value = true
  try {
    await store.fetchAll(true)
  } finally {
    refreshing.value = false
  }
}
</script>
