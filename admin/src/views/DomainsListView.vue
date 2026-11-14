<script setup lang="ts">
/**
 * Domains list view: paged, searchable table of all domain registrations.
 *
 * The search text and the page live in the address (`useFilterQuery`), so a reload or a shared link opens the same
 * list; the domains endpoint takes no other filter. Shows domain name, client, status, expiry date, renewal price and auto-renew flag. A domain
 * name opens its detail page under the owning client. Reads `stores/domains`; a refused load
 * shows `store.error`.
 */
import { computed, onMounted, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiListToolbar from '@/components/ui/UiListToolbar.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiPagination from '@/components/ui/UiPagination.vue'
import UiRowActions from '@/components/ui/UiRowActions.vue'
import type { UiMenuAction } from '@/utils/ui/menu'
import UiStack from '@/components/ui/UiStack.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import { Icon } from '@/lib/icons'
import { DOMAIN_STATUS_OPTIONS } from '@/lib/constants'
import { formatDate } from '@/lib/format'
import { formatPrice } from '@/lib/money'
import { domainStatusTone, statusLabel } from '@/lib/tones'
import { useDomainsStore } from '@/stores/domains'
import type { DomainRegistration } from '@/types/domain'
import { UiTone } from '@/utils/ui/tone'
import { UiTableDensity } from '@/utils/ui/injection'
import { useFilterQuery } from '@/composables/useFilterQuery'
import { ClientSection, clientItemPath, clientPath } from '@/lib/routes'

const { t } = useI18n()

const store = useDomainsStore()

/** The search text and the page of the list, kept in the address. */
const { search, appliedSearch, page, setPage } = useFilterQuery([])

/** Whether the current page has any domain to list. */
const hasRows = computed<boolean>(() => store.domains.length > 0)

/**
 * Loads the page and the search the address holds.
 *
 * @returns True when loaded; false when refused (see `store.error`).
 */
const load = (): Promise<boolean> => store.fetchAll(page.value, appliedSearch.value)

/**
 * The row actions of a domain: its one action opens the domain under its client as a real link.
 *
 * @param domain - The domain of the row.
 * @returns The action list passed to UiRowActions.
 */
const domainActions = (domain: DomainRegistration): UiMenuAction[] => [
  {
    label: t('shared.view'),
    testid: `domains-view-${domain.id}`,
    to: clientItemPath(domain.clientId, ClientSection.Domains, domain.id),
  },
]

// A search, a page or the back button loads the list the address names.
watch([appliedSearch, page], load)

onMounted(() => load())
</script>

<template>
  <UiStack>
    <UiPageHeader
      :title="t('shared.domains')"
      :subtitle="t('domains.domainsList.manageAllRegisteredDomainNames')"
      data-testid="domains-header"
    />

    <UiListToolbar
      v-model:search="search"
      search-testid="domains-search"
      :search-placeholder="t('domains.domainsList.searchDomains')"
    />

    <UiErrorState
      v-if="store.error"
      :message="store.error"
      :loading="store.loading"
      testid="domains-error"
      @retry="load"
    />

    <UiCard v-else-if="hasRows || store.loading">
      <UiTable
        :density="UiTableDensity.Comfortable"
        :loading="store.loading && !hasRows"
        data-testid="domains-table"
      >
        <UiTableHead>
          <UiTableRow>
            <UiTableHeader card-role="title">{{ t('shared.domainName') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.client') }}</UiTableHeader>
            <UiTableHeader card-role="badge">{{ t('shared.status') }}</UiTableHeader>
            <UiTableHeader>{{ t('domains.domainsList.expiresAt') }}</UiTableHeader>
            <UiTableHeader numeric>{{ t('domains.domainsList.renewalPrice') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.autoRenew') }}</UiTableHeader>
            <UiTableHeader actions>{{ t('shared.actions') }}</UiTableHeader>
          </UiTableRow>
        </UiTableHead>
        <UiTableBody>
          <UiTableRow
            v-for="domain in store.domains"
            :key="domain.id"
            hoverable
            bordered
            :data-testid="`domains-row-${domain.id}`"
          >
            <UiTableCell>
              <UiLink
                variant="link"
                :to="clientItemPath(domain.clientId, ClientSection.Domains, domain.id)"
                :data-testid="`domains-name-${domain.id}`"
              >
                {{ domain.name }}
              </UiLink>
            </UiTableCell>
            <UiTableCell>
              <UiLink variant="link" :to="clientPath(domain.clientId)" :data-testid="`domains-client-${domain.id}`">
                {{ domain.clientName }}
              </UiLink>
            </UiTableCell>
            <UiTableCell>
              <UiBadge :tone="domainStatusTone(domain.status)" small>
                {{ statusLabel(DOMAIN_STATUS_OPTIONS, domain.status) }}
              </UiBadge>
            </UiTableCell>
            <UiTableCell muted>{{ formatDate(domain.expiresAt) }}</UiTableCell>
            <UiTableCell numeric>{{ formatPrice(domain.recurringAmount, domain.priceCurrency) }}</UiTableCell>
            <UiTableCell>
              <UiBadge :tone="domain.autoRenew ? UiTone.Success : UiTone.Neutral" small>
                {{ domain.autoRenew ? t('shared.yes') : t('shared.no') }}
              </UiBadge>
            </UiTableCell>
            <UiTableCell actions>
              <UiRowActions :actions="domainActions(domain)" :testid="`domains-actions-${domain.id}`" />
            </UiTableCell>
          </UiTableRow>
        </UiTableBody>
      </UiTable>

      <template #footer>
        <UiStack direction="row" justify="between" align="center" wrap>
          <UiText tone="muted" size="xs">
            {{ t('shared.totalcountDomainValue', { totalCount: store.totalCount }) }}
          </UiText>
          <UiPagination
            :page="page"
            :total="store.totalCount"
            :page-size="store.pageSize"
            testid="domains-pagination"
            @update:page="setPage"
          />
        </UiStack>
      </template>
    </UiCard>

    <UiCard v-else>
      <UiEmptyState
        :title="appliedSearch ? t('domains.domainsList.noDomainsMatchYourSearch') : t('domains.domainsList.noDomainsFound')"
        :description="appliedSearch ? t('domains.domainsList.tryAdjustingYourSearchTerm') : undefined"
        data-testid="domains-empty"
      >
        <template #icon>
          <UiIcon :markup="Icon.Globe" size="lg" />
        </template>
      </UiEmptyState>
    </UiCard>
  </UiStack>
</template>
