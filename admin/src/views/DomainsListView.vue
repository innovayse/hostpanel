<script setup lang="ts">
/**
 * Domains list view: paged, searchable table of all domain registrations.
 *
 * Shows domain name, client, status, expiry date, renewal price and auto-renew flag. A domain
 * name opens its detail page under the owning client. Reads `stores/domains`; a refused load
 * shows `store.error`.
 */
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiPagination from '@/components/ui/UiPagination.vue'
import UiRowActions, { type UiRowAction } from '@/components/ui/UiRowActions.vue'
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
import { SEARCH_DEBOUNCE_MS } from '@/lib/limits'
import { ClientSection, clientItemPath, clientPath } from '@/lib/routes'

const { t } = useI18n()

const store = useDomainsStore()

/** Debounce timer handle for the search input. */
let searchTimer: ReturnType<typeof setTimeout> | null = null

/** Local search model bound to the input. */
const searchInput = ref('')

/** Whether the current page has any domain to list. */
const hasRows = computed<boolean>(() => store.domains.length > 0)

/**
 * Moves to a page of the list and loads it.
 *
 * @param page - Target page, 1-based.
 */
const goToPage = (page: number): void => {
  store.page = page
  void store.fetchAll()
}

/** Sends the typed search to the store after the pause, restarting from page 1. */
const handleSearch = (): void => {
  if (searchTimer) {
    clearTimeout(searchTimer)
  }
  searchTimer = setTimeout(() => {
    store.search = searchInput.value
    store.page = 1
    void store.fetchAll()
  }, SEARCH_DEBOUNCE_MS)
}

/**
 * The row actions of a domain: its one action opens the domain under its client as a real link.
 *
 * @param domain - The domain of the row.
 * @returns The action list passed to UiRowActions.
 */
const domainActions = (domain: DomainRegistration): UiRowAction[] => [
  {
    label: t('shared.view'),
    testid: `domains-view-${domain.id}`,
    to: clientItemPath(domain.clientId, ClientSection.Domains, domain.id),
  },
]

watch(searchInput, handleSearch)

onMounted(() => store.fetchAll())

// A pending search must not fire after the screen is gone.
onUnmounted(() => {
  if (searchTimer) {
    clearTimeout(searchTimer)
  }
})
</script>

<template>
  <UiStack>
    <UiPageHeader
      :title="t('shared.domains')"
      :subtitle="t('domains.domainsList.manageAllRegisteredDomainNames')"
      data-testid="domains-header"
    />

    <UiInput
      v-model="searchInput"
      type="text"
      class="max-w-sm"
      :placeholder="t('domains.domainsList.searchDomains')"
      data-testid="domains-search"
    >
      <template #icon>
        <UiIcon :markup="Icon.Search" />
      </template>
    </UiInput>

    <UiErrorState
      v-if="store.error"
      :message="store.error"
      :loading="store.loading"
      testid="domains-error"
      @retry="store.fetchAll()"
    />

    <UiCard v-else-if="hasRows || store.loading">
      <UiTable density="comfortable" :loading="store.loading && !hasRows" :colspan="7" data-testid="domains-table">
        <UiTableHead>
          <UiTableRow>
            <UiTableHeader>{{ t('shared.domainName') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.client') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.status') }}</UiTableHeader>
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

      <UiStack direction="row" justify="between" align="center" class="p-3">
        <UiText tone="muted" size="xs">
          {{ t('shared.totalcountDomainValue', { totalCount: store.totalCount }) }}
        </UiText>
        <UiPagination
          :page="store.page"
          :total="store.totalCount"
          :page-size="store.pageSize"
          testid="domains-pagination"
          @update:page="goToPage"
        />
      </UiStack>
    </UiCard>

    <UiCard v-else>
      <UiEmptyState
        :title="searchInput ? t('domains.domainsList.noDomainsMatchYourSearch') : t('domains.domainsList.noDomainsFound')"
        :description="searchInput ? t('domains.domainsList.tryAdjustingYourSearchTerm') : undefined"
        data-testid="domains-empty"
      >
        <template #icon>
          <UiIcon :markup="Icon.Globe" size="lg" />
        </template>
      </UiEmptyState>
    </UiCard>
  </UiStack>
</template>
