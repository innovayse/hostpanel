<template>
  <UiCard data-testid="dashboard-register-domain">
    <UiCardHeader :title="$t('client.dashboard.registerDomain')" />
    <UiInput
      v-model="domainQuery"
      :placeholder="$t('client.dashboard.domainPlaceholder')"
      testid="dashboard-domain-query"
      @keydown.enter="registerDomain"
    />
    <UiStack direction="row" gap="xs" class="mt-3">
      <UiButton full-width data-testid="dashboard-domain-register" @click="registerDomain">
        {{ $t('client.dashboard.registerBtn') }}
      </UiButton>
      <UiButton variant="subtle" full-width data-testid="dashboard-domain-transfer" @click="transferDomain">
        {{ $t('client.dashboard.transferBtn') }}
      </UiButton>
    </UiStack>
  </UiCard>
</template>

<script setup lang="ts">
/**
 * The dashboard's "Register a New Domain" card: a name field with Register and Transfer buttons
 * that carry the typed name on to the domain search or the billing portal's transfer form.
 */

import { DOMAIN_SEARCH_QUERY_PARAM } from '~/constants/domainSearchQuery'
import { SITE_DOMAINS_PATH } from '~/constants/siteRoutes'

/** Runtime configuration: carries the billing portal's address. */
const config = useRuntimeConfig()

/** Base URL of the legacy billing portal, used for transfers; empty when not configured. */
const billingPortalUrl = config.public.billingPortalUrl as string | undefined

/** The domain name as typed. */
const domainQuery = ref('')

/**
 * Opens the public domain search, prefilled with the typed name.
 *
 * @returns Nothing.
 */
const registerDomain = (): void => {
  const query = domainQuery.value.trim()
  navigateTo(query ? `${SITE_DOMAINS_PATH}?${DOMAIN_SEARCH_QUERY_PARAM}=${encodeURIComponent(query)}` : SITE_DOMAINS_PATH)
}

/**
 * Starts a transfer in the billing portal for the typed name, or opens the domain search when
 * nothing was typed or no portal is configured.
 *
 * @returns Nothing.
 */
const transferDomain = (): void => {
  const query = domainQuery.value.trim()
  if (query && billingPortalUrl) {
    window.open(`${billingPortalUrl}/cart.php?a=add&domain=transfer&query=${encodeURIComponent(query)}`, '_blank')
  } else {
    navigateTo(SITE_DOMAINS_PATH)
  }
}
</script>
