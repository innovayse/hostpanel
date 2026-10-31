<template>
  <UiStack gap="lg" class="max-w-xl w-full mx-auto py-10 min-h-[60dvh] justify-center">
    <ClientServicesSetupStatePanel
      v-if="pending"
      loading
      :title="$t('client.setup.loading')"
      :description="$t('client.setup.pleaseWait')"
    />

    <ClientServicesSetupStatePanel
      v-else-if="error || !service"
      :icon="AlertCircle"
      tone="danger"
      :title="$t('client.setup.errorTitle')"
      :description="$t('client.setup.errorDesc')"
    >
      <UiButton :to="CLIENT_SERVICES_PATH" variant="subtle" data-testid="setup-back-to-services">{{ $t('client.setup.backToServices') }}</UiButton>
    </ClientServicesSetupStatePanel>

    <ClientServicesSetupStatePanel
      v-else-if="isAlreadySetUp"
      :icon="CheckCircle"
      tone="success"
      :title="$t('client.setup.alreadyActive')"
      :description="$t('client.setup.alreadyActiveDesc')"
    >
      <UiButton :to="clientServicePath(service.id)" data-testid="setup-go-manage">{{ $t('client.setup.goToManage') }}</UiButton>
    </ClientServicesSetupStatePanel>

    <ClientServicesSetupWizard v-else :service="service" :service-id="serviceId" />
  </UiStack>
</template>

<script setup lang="ts">
/**
 * Hosting setup page: loads the service and shows the setup wizard, or says why there is nothing
 * to set up (the service could not be read, or it is already configured). The service is read
 * through the service management store.
 *
 * The form keeps a short column (a width cap, an allowed exception: form fields are intrinsically
 * narrow, so this is not page layout; the page width itself comes from the shared container).
 */
import { CLIENT_SERVICES_PATH, clientServicePath } from '~/constants/clientRoutes'
import { AlertCircle, CheckCircle } from 'lucide-vue-next'
import { useServiceManagementStore } from '~/stores/servicemanagement'
import { ServiceStatus } from '~/types/servicestatus'

definePageMeta({ layout: 'client', middleware: 'client-auth' })

/** Translates a key into the active locale. */
const { t } = useI18n()

/** Site name the operator set; the brand is the fallback. */
const { name: siteName } = useSiteIdentity()
/** The current route; its params name the record shown. */
const route = useRoute()

/** The id of the service in the route. */
const serviceId = route.params.id as string

/** The service management store; reset so a previous visit's refusal does not linger. */
const mgmt = useServiceManagementStore()
mgmt.reset()
/** Loading flag of the data this page reads. */
const { pending } = await useAsyncData(`client-service-setup-${serviceId}`, () => mgmt.loadService(serviceId))

/** The service being set up; null until loaded or after a failed read. */
const service = computed(() => mgmt.service)

/** The sentence for a failed read. */
const error = computed(() => mgmt.serviceError)

/** Whether the service is active and already has a control-panel account and a domain. */
const isAlreadySetUp = computed(() => service.value?.status === ServiceStatus.Active && Boolean(service.value.username) && Boolean(service.value.domain))

// A digital service is delivered by its plugin and has no wizard: send it to its own page.
watch(service, (current) => {
  if (!pending.value && current?.isDigital) {
    navigateTo(clientServicePath(serviceId), { replace: true })
  }
}, { immediate: true })

useHead({ title: computed(() => `${t('client.setup.pageTitle')} — ${siteName.value}`) })
</script>
