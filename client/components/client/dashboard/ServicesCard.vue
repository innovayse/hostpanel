<template>
  <UiCard data-testid="dashboard-services">
    <UiCardHeader :title="$t('client.dashboard.activeServices')">
      <UiButton size="sm" variant="subtle" :to="CLIENT_SERVICES_PATH" data-testid="dashboard-services-all">
        {{ $t('client.dashboard.myServices') }}
        <ArrowRight :size="12" :stroke-width="2" class="ml-1.5" />
      </UiButton>
    </UiCardHeader>

    <UiStack v-if="store.servicesLoading" gap="sm">
      <UiSkeleton v-for="i in 3" :key="i" shape="block" class="h-12" />
    </UiStack>

    <UiEmptyState v-else-if="!store.services.length" :icon="Server" :title="$t('client.dashboard.noActiveServices')" class="!py-6" data-testid="dashboard-services-empty">
      <UiButton size="sm" variant="subtle" to="/hosting" data-testid="dashboard-place-order">{{ $t('client.dashboard.placeOrder') }}</UiButton>
    </UiEmptyState>

    <UiStack v-else gap="sm">
      <ClientDashboardListRow
        v-for="service in store.services.slice(0, 4)"
        :key="service.id"
        :to="serviceAwaitsSetup(service) ? clientServiceSetupPath(service.id) : clientServicePath(service.id)"
        :testid="`dashboard-service-${service.id}`"
      >
        <UiStack direction="row" align="center" gap="sm" class="min-w-0">
          <Server :size="16" :stroke-width="2" class="flex-shrink-0 text-primary-600 dark:text-primary-400" />
          <div class="min-w-0">
            <UiText size="sm" weight="medium" truncate>{{ service.productName }}</UiText>
            <UiText size="xs" tone="subtle" truncate>{{ service.domain || $t('client.dashboard.noDomain') }}</UiText>
          </div>
        </UiStack>
        <UiStack direction="row" align="center" gap="sm">
          <UiIconButton
            v-if="canSignIn(service)"
            :icon="ssoLoading === service.id ? Loader : Monitor"
            :label="$t('client.services.actionLoginCpanel')"
            :title="$t('client.services.actionLoginCpanel')"
            variant="ghost"
            size="sm"
            :disabled="ssoLoading === service.id"
            :testid="`dashboard-service-cpanel-${service.id}`"
            @click.stop.prevent="loginToCpanel(service)"
          />
          <UiBadge v-if="serviceAwaitsSetup(service)" variant="info" size="sm">{{ $t('client.dashboard.needsSetup') }}</UiBadge>
          <ClientStatusBadge v-else :status="service.status" />
        </UiStack>
      </ClientDashboardListRow>
    </UiStack>
  </UiCard>
</template>

<script setup lang="ts">
/**
 * The dashboard's "Active Services" card: the first four services with their status, a
 * control-panel shortcut for active ones and a setup badge for those not yet provisioned.
 */
import { CLIENT_SERVICES_PATH, clientServicePath, clientServiceSetupPath } from '~/constants/clientRoutes'
import { ArrowRight, Loader, Monitor, Server } from 'lucide-vue-next'
import { Permission } from '~/composables/usePermissions'
import { useClientStore } from '~/stores/client'
import { serviceAwaitsSetup } from '~/utils/serviceSetup'
import { useServiceManagementStore } from '~/stores/servicemanagement'
import type { ClientService } from '~/types/clientservice'
import { canSignInToControlPanel } from '~/utils/controlPanel'

/** Client store: the signed-in person and their services, invoices, domains and tickets. */
const store = useClientStore()

/**
 * Whether the control-panel shortcut is offered for a service.
 *
 * @param service - The listed service.
 * @returns True for an active service on a known host when the person may sign in with single sign-on.
 */
const canSignIn = (service: ClientService): boolean =>
  canSignInToControlPanel(service, store.mayDo(Permission.AllowSingleSignOn))

/** Id of the service whose control-panel sign-in is under way, or null. */
const ssoLoading = ref<number | null>(null)

/**
 * Opens the service's control panel through single sign-on, falling back to the plain login page.
 *
 * @param service - The service whose control panel to open.
 * @returns Nothing.
 */
const loginToCpanel = async (service: ClientService): Promise<void> => {
  if (ssoLoading.value) {
    return
  }
  ssoLoading.value = service.id
  try {
    await useServiceManagementStore().openControlPanel(String(service.id), service.server?.hostname)
  } finally {
    ssoLoading.value = null
  }
}
</script>
