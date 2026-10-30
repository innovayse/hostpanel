<template>
  <UiStack gap="lg">
    <UiPageHeader :title="$t('client.services.title')" :description="$t('client.services.subtitle')" :icon="Server" class="!mb-0" />

    <UiGrid v-if="store.servicesLoading" cols="2">
      <UiSkeleton v-for="i in 4" :key="i" shape="block" class="h-36" />
    </UiGrid>

    <!-- Not a customer account. Checked before the failure branch: this is a state the
         API reported, not a fault, and it carries its own explanation and a way out. -->
    <ClientNoProfileNotice v-else-if="store.clientProfileMissing" />

    <!-- Failed. Checked before the empty state, which is otherwise what a failure renders as:
         the list is empty either way, and only this branch knows which of the two it is. -->
    <UiAlert v-else-if="store.servicesError" variant="error" data-testid="services-error">{{ store.servicesError }}</UiAlert>

    <UiEmptyState v-else-if="!store.services.length" :icon="Server" :title="$t('client.services.empty')">
      <UiButton to="/hosting" data-testid="services-browse-hosting">{{ $t('client.services.browseHosting') }}</UiButton>
    </UiEmptyState>

    <template v-else>
      <UiAlert v-if="servicesNeedingSetup.length > 0" variant="info" :title="setupAlertTitle" data-testid="services-setup-alert">
        {{ $t('client.dashboard.setupAlertSubtitle') }}
      </UiAlert>

      <UiGrid cols="2" data-testid="services-list">
        <ClientServicesServiceCard
          v-for="service in store.services"
          :key="service.id"
          :service="service"
          :currency="clientCurrency"
        />
      </UiGrid>
    </template>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * Service list: one card per hosting service with its status, next due date, amount and disk
 * usage, plus a banner when some services still need setup. Loading, missing-profile and failure
 * states come from the client store.
 */
import { Server } from 'lucide-vue-next'
import { useClientStore } from '~/stores/client'
import { serviceAwaitsSetup } from '~/utils/serviceSetup'

definePageMeta({ layout: 'client', middleware: 'client-auth' })

/** Translates a key into the active locale. */
const { t } = useI18n()

/** The client store that owns the service list and its loading and failure state. */
const store = useClientStore()

/**
 * What the client profile says these amounts are denominated in.
 *
 * The ISO 4217 code off the account (`ClientDto.Currency`). An account with no currency set
 * still passes nothing through rather than inventing one from the locale; `formatCurrency`
 * groups the digits either way.
 */
const clientCurrency = computed(() => ({ code: store.user?.currency }))

await useAsyncData('client-services', () => store.fetchServices(true))

/** The hosting services that have no control-panel account yet; a digital service has nothing to set up. */
const servicesNeedingSetup = computed(() => store.services.filter(serviceAwaitsSetup))

/** Headline of the setup banner, singular or plural by how many services wait. */
const setupAlertTitle = computed(() =>
  servicesNeedingSetup.value.length === 1
    ? t('client.dashboard.setupAlertSingle')
    : t('client.dashboard.setupAlertMultiple', { count: servicesNeedingSetup.value.length })
)
</script>
