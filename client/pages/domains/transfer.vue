<template>
  <DomainsTransfer v-model:domain="transferQuery" v-model:epp="transferEpp" @submit="goTransfer" />
</template>

<script setup lang="ts">
/**
 * Public domain-transfer page.
 *
 * Collects the domain name and the authorization code and hands the visitor on to the signed-in
 * transfer flow with both in the query string; `DomainsTransfer` only renders the form.
 */
import { CLIENT_DOMAIN_TRANSFER_PATH } from '~/constants/clientRoutes'
import { detailTitle } from '~/utils/seoTitle'

const { t } = useI18n()

/** Site name the operator set; the brand is the fallback. */
const { name: siteName } = useSiteIdentity()
/** Prefixes a path with the active language. */
const localePath = useLocalePath()
/** The current route. */
const route = useRoute()

/** The domain name to transfer, pre-filled from `?domain=`. */
const transferQuery = ref(typeof route.query.domain === 'string' ? route.query.domain : '')

/** The authorization code, pre-filled from `?eppcode=`. */
const transferEpp = ref(typeof route.query.eppcode === 'string' ? route.query.eppcode : '')

/** Sends the visitor to the client-area transfer flow with the typed values; does nothing without a domain. */
const goTransfer = (): void => {
  const domain = transferQuery.value.trim()
  if (!domain) {
    return
  }
  const epp = transferEpp.value.trim()
  const params = new URLSearchParams({ domain })
  if (epp) {
    params.set('eppcode', epp)
  }
  navigateTo(localePath(`${CLIENT_DOMAIN_TRANSFER_PATH}?${params.toString()}`))
}

useSeo({
  title: detailTitle(`${t('domains.tabTransfer')}: ${t('domains.card.name')}`, siteName.value),
  description: t('domains.promoTransferDesc'),
  path: '/domains/transfer',
})
</script>
