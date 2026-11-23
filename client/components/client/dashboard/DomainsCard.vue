<template>
  <UiCard data-testid="dashboard-domains">
    <UiCardHeader :title="$t('client.dashboard.myDomains')">
      <UiButton size="sm" variant="subtle" :to="CLIENT_DOMAINS_PATH" data-testid="dashboard-domains-all">
        {{ $t('client.dashboard.viewAll') }}
        <ArrowRight :size="12" :stroke-width="2" class="ml-1.5" />
      </UiButton>
    </UiCardHeader>

    <UiStack v-if="store.domainsLoading" gap="sm">
      <UiSkeleton v-for="i in 2" :key="i" shape="block" class="h-12" />
    </UiStack>

    <UiEmptyState v-else-if="!store.domains.length" :icon="Globe" :title="$t('client.dashboard.noDomains')" class="!py-6" data-testid="dashboard-domains-empty">
      <UiButton size="sm" variant="subtle" :to="SITE_DOMAINS_PATH" data-testid="dashboard-domains-search">{{ $t('client.domains.searchDomains') }}</UiButton>
    </UiEmptyState>

    <UiStack v-else gap="sm">
      <ClientDashboardListRow
        v-for="domain in store.domains.slice(0, 4)"
        :key="domain.id"
        :to="clientDomainPath(domain.id)"
        :testid="`dashboard-domain-${domain.id}`"
      >
        <UiStack direction="row" align="center" gap="sm" class="min-w-0">
          <Globe :size="16" :stroke-width="2" class="flex-shrink-0 text-primary-600 dark:text-primary-400" />
          <UiText :as="UiTextTag.Span" size="sm" weight="medium" truncate>{{ domain.name }}</UiText>
        </UiStack>
        <UiStack direction="row" align="center" gap="xs" class="flex-shrink-0">
          <UiText :as="UiTextTag.Span" size="xs" tone="subtle">{{ domainDateLabel(domain) }}</UiText>
          <ClientStatusBadge :status="domain.status" />
        </UiStack>
      </ClientDashboardListRow>
    </UiStack>
  </UiCard>
</template>

<script setup lang="ts">
/**
 * The dashboard's "My Domains" card: the first four domains with their most relevant date and
 * status, each opening the domain.
 */
import { CLIENT_DOMAINS_PATH, clientDomainPath } from '~/constants/clientRoutes'
import { ArrowRight, Globe } from 'lucide-vue-next'
import { useClientStore } from '~/stores/client'
import type { ClientDomain } from '~/types/clientdomain'
import { EMPTY_DATE, formatDate } from '~/utils/formatDate'
import { SITE_DOMAINS_PATH } from '~/constants/siteRoutes'
import { UiTextTag } from '~/utils/ui/enums'

/** Translator for the strings this file builds itself. */
const { t } = useI18n()
/** Client store: the signed-in person and their services, invoices, domains and tickets. */
const store = useClientStore()

/**
 * The one date worth showing for a domain in the summary list, with its own label.
 *
 * Expiry first, then next-due, then registration: the row has space for one, and that is the
 * order in which a customer needs it. `formatDate` treats both zero-date sentinels as absent,
 * so a field that carries one falls through to the next candidate here.
 *
 * @param domain - The domain row being rendered.
 * @returns The labelled date, or the em dash when the domain carries none.
 */
const domainDateLabel = (domain: ClientDomain): string => {
  const candidates: Array<[string, string | null | undefined]> = [
    ['client.dashboard.exp', domain.expiresAt],
    ['client.dashboard.due', domain.nextDueDate],
    ['client.dashboard.reg', domain.registeredAt]
  ]

  for (const [key, value] of candidates) {
    const formatted = formatDate(value)
    if (formatted !== EMPTY_DATE) {
      return `${t(key)} ${formatted}`
    }
  }

  return EMPTY_DATE
}
</script>
