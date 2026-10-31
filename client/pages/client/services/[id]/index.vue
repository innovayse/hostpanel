<template>
  <UiStack gap="lg">
    <UiBackLink :to="CLIENT_SERVICES_PATH" :label="$t('client.services.backTo')" testid="service-back" />

    <UiStack v-if="pending">
      <UiSkeleton shape="block" class="h-20" />
      <UiSkeleton shape="block" class="h-80" />
    </UiStack>

    <UiEmptyState v-else-if="notFound" :icon="AlertCircle" :title="$t('client.services.notFound')" data-testid="service-not-found" />

    <!-- Any other failed read: the API's sentence and a way to read again. -->
    <UiAlert v-else-if="error || !service" variant="error" data-testid="service-load-error">
      {{ error }}
      <template #action>
        <UiButton variant="outline" size="sm" class="shrink-0" data-testid="service-retry" @click="refreshService()">
          {{ $t('common.retry') }}
        </UiButton>
      </template>
    </UiAlert>

    <template v-else>
      <ClientServicesServiceHeader :service="service" :sso-loading="ssoLoading" @cpanel="loginToCpanel" />
      <ClientServicesServiceNotices :service="service" :service-id="serviceId" :cancel-status="cancelStatus" />

      <UiSplit ratio="end-wide" gap="lg">
        <template #start>
          <ClientServicesServiceSidebar
            v-model:active-tab="activeTab"
            :service="service"
            :service-id="serviceId"
            :sso-loading="ssoLoading"
            :sso-error="ssoError"
            @cpanel="loginToCpanel"
          />
        </template>

        <template #end>
          <UiStack v-if="activeTab === ServiceTab.Overview" data-testid="service-overview">
            <UiAlert
              v-if="service.status === ServiceStatus.Suspended && service.suspensionSource"
              variant="error"
              :icon-size="16"
              :live="false"
              :title="$t('client.services.suspendedReason')"
              data-testid="service-suspension-reason"
            >
              {{ $t(SUSPENSION_SOURCE_KEYS[service.suspensionSource]) }}
            </UiAlert>
            <ClientServicesServiceDetailsCard :service="service" :currency="clientCurrency" />
            <ClientServicesServiceServerCard :service="service" :hosting-info="hostingInfo" />
            <ClientServicesServiceQuickActions
              :has-server="Boolean(service.server)"
              :is-active="service.status === ServiceStatus.Active"
              :sso-loading="ssoLoading"
              @cpanel="loginToCpanel"
              @invoices="loadInvoices"
              @cancel="activeTab = ServiceTab.Cancel"
            />
            <ClientServicesServiceInvoicesCard v-if="store.mayDo(Permission.ViewPayInvoices)" />
          </UiStack>

          <ClientServicesServicePasswordForm v-else-if="activeTab === ServiceTab.Password" :service-id="serviceId" />
          <ClientServicesServiceCancelForm v-else-if="activeTab === ServiceTab.Cancel" :service-id="serviceId" />
          <ClientServicesServiceSshKeys v-else-if="activeTab === ServiceTab.Ssh" :service-id="serviceId" />
        </template>
      </UiSplit>
    </template>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * Service detail: header and notices, a side menu and the panel it selects (overview with
 * the suspension reason, details, server and usage, and invoices; change password; request cancellation). Every
 * in-flight flag and refusal sentence belongs to the service management store; the panels read
 * them there. A service that has no control-panel account yet is sent to its setup wizard.
 */
import { AlertCircle } from 'lucide-vue-next'
import { storeToRefs } from 'pinia'
import { CLIENT_SERVICES_PATH, clientServiceSetupPath } from '~/constants/clientRoutes'
import { serviceAwaitsSetup } from '~/utils/serviceSetup'
import { Permission } from '~/composables/usePermissions'
import { useClientStore } from '~/stores/client'
import { useServiceManagementStore } from '~/stores/servicemanagement'
import { ServiceStatus } from '~/types/servicestatus'
import { ServiceTab } from '~/types/servicetab'
import { SuspensionSource } from '~/types/suspensionsource'

definePageMeta({ layout: 'client', middleware: 'client-auth' })

/** The current route; its params name the record shown. */
const route = useRoute()
/** The client store: the signed-in client and the currency of the account. */
const store = useClientStore()

/** The id of the service in the route. */
const serviceId = route.params.id as string

/** The service management store; reset so a previous visit's success or error does not linger. */
const mgmt = useServiceManagementStore()
mgmt.reset()
/** Reactive state of the store this component renders. */
const { ssoLoading, ssoError } = storeToRefs(mgmt)

// The service, its hosting info and its cancellation status are read through the service store.
/** Loading flag of the service read, and the action that reads it again. */
const { pending, refresh: refreshService } = await useAsyncData(`client-service-${serviceId}`, () => mgmt.loadService(serviceId))

/** The service being shown; null until loaded or after a failed read. */
const service = computed(() => mgmt.service)

/** The sentence for a failed read. */
const error = computed(() => mgmt.serviceError)

/** Whether the read was refused because the client has no such service. */
const notFound = computed(() => mgmt.serviceNotFound)

await useAsyncData('client-user', () => store.fetchUser())

await useAsyncData(`client-service-hosting-${serviceId}`, () => mgmt.loadHostingInfo(serviceId))

/** The service's nameservers and certificate. */
const hostingInfo = computed(() => mgmt.hostingInfo)

await useAsyncData(`client-service-cancellation-${serviceId}`, () => mgmt.loadCancellationStatus(serviceId))

/** The sentence that names each suspension reason. */
const SUSPENSION_SOURCE_KEYS: Record<SuspensionSource, string> = {
  [SuspensionSource.Manual]: 'client.services.suspensionSources.manual',
  [SuspensionSource.Overdue]: 'client.services.suspensionSources.overdue',
  [SuspensionSource.DomainExpired]: 'client.services.suspensionSources.domainExpired',
  [SuspensionSource.Other]: 'client.services.suspensionSources.other',
}

/** Whether a cancellation is pending on the service, and of which type. */
const cancelStatus = computed(() => mgmt.cancelStatus)

/**
 * What the client profile says the service amounts are denominated in.
 *
 * The ISO 4217 code of the account (`ClientDto.Currency`). An account with no currency set still
 * passes nothing through: `formatCurrency` groups the digits and shows no symbol, which is the
 * honest rendering.
 */
const clientCurrency = computed(() => ({ code: store.user?.currency }))

/** The panel shown beside the side menu, a `ServiceTab` value (a string so the menu can v-model it). */
const activeTab = ref<string>(ServiceTab.Overview)

/**
 * Reads the invoices raised against this service. The store keeps the list, the count of
 * unattributable invoices and any failure, so the card never claims "no invoices" without
 * evidence.
 *
 * @returns Resolves when the read has ended; the result is rendered from the store state.
 */
const loadInvoices = (): Promise<void> => mgmt.loadInvoices(serviceId)

/**
 * Signs the customer in to the control panel in a new tab.
 *
 * When the automatic sign-in fails the store keeps the reason in `ssoError`, and the panel's own
 * login form is still opened so the reader is not stranded.
 *
 * @returns Resolves when the attempt has ended.
 */
const loginToCpanel = (): Promise<void> => mgmt.openControlPanel(serviceId, service.value?.server?.hostname)

// A hosting service without a control-panel account is not manageable yet: send it to the setup wizard (a digital one has none to set up).
watch(service, (current) => {
  if (!pending.value && current && current.status !== ServiceStatus.Terminated && serviceAwaitsSetup(current)) {
    navigateTo(clientServiceSetupPath(serviceId))
  }
}, { immediate: true })

// The related invoices load when the page opens, for the overview card.
onMounted(() => {
  // The related invoices are for a person who may see invoices; anyone else is not asked to load them.
  if (store.mayDo(Permission.ViewPayInvoices)) {
    return loadInvoices()
  }
})
</script>
