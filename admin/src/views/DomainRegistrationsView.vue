<script setup lang="ts">
/**
 * Domain registrations: paged table of every domain registration across clients.
 *
 * Shows ID, domain, client, registration period, registrar, price, next due date, expiry date and
 * status. The page lives in the address (`useFilterQuery`), so a reload or a shared link opens the same page.
 * Reads `stores/domains`; a refused load shows the API's sentence.
 */
import { computed, onMounted, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useFallbackLabels } from '@/composables/useFallbackLabels'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiPagination from '@/components/ui/UiPagination.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import { DOMAIN_STATUS_OPTIONS } from '@/lib/constants'
import { formatDate } from '@/lib/format'
import { Icon } from '@/lib/icons'
import { formatPrice } from '@/lib/money'
import { domainStatusTone, statusLabel } from '@/lib/tones'
import { useDomainsStore } from '@/stores/domains'
import { clientPath } from '@/lib/routes'
import { useFilterQuery } from '@/composables/useFilterQuery'
import { UiTableDensity } from '@/utils/ui/injection'

const { t } = useI18n()


/** Fallback text for fields the API leaves unresolved. */

const { yearsLabel } = useFallbackLabels()

/** Domains store: the page, its paging and the load state. */
const store = useDomainsStore()

/** Whether the current page has any domain to list. */
const hasRows = computed<boolean>(() => store.domains.length > 0)

/** The page of the list, kept in the address. */
const { page, setPage } = useFilterQuery([])

/**
 * Loads the page the address names.
 *
 * @returns True when loaded; false when refused (see `store.error`).
 */
const load = (): Promise<boolean> => store.fetchAll(page.value, '')

// A page chosen, or the back button, loads the page the address names.
watch(page, load)

onMounted(() => load())
</script>

<template>
  <UiStack>
    <UiPageHeader
      :title="t('clients.domainRegistrations.domainRegistrations')"
      :subtitle="t('clients.domainRegistrations.viewAndManageClientDomain')"
      data-testid="domain-registrations-header"
    />

    <UiErrorState
      v-if="store.error"
      :message="store.error"
      :loading="store.loading"
      testid="domain-registrations-error"
      @retry="load"
    />

    <UiCard v-else-if="hasRows || store.loading">
      <UiTable
        :density="UiTableDensity.Comfortable"
        :loading="store.loading && !hasRows"
        data-testid="domain-registrations-table"
      >
        <UiTableHead>
          <UiTableRow>
            <UiTableHeader>{{ t('shared.id') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.domain') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.clientName') }}</UiTableHeader>
            <UiTableHeader>{{ t('clients.domainRegistrations.regPeriod') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.registrar') }}</UiTableHeader>
            <UiTableHeader numeric>{{ t('shared.price') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.nextDueDate') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.expiryDate') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.status') }}</UiTableHeader>
          </UiTableRow>
        </UiTableHead>
        <UiTableBody>
          <UiTableRow
            v-for="domain in store.domains"
            :key="domain.id"
            hoverable
            bordered
            :data-testid="`domain-registrations-row-${domain.id}`"
          >
            <UiTableCell muted>{{ domain.id }}</UiTableCell>
            <UiTableCell>{{ domain.name }}</UiTableCell>
            <UiTableCell>
              <UiLink
                variant="link"
                :to="clientPath(domain.clientId)"
                :data-testid="`domain-registrations-client-${domain.id}`"
              >
                {{ domain.clientName }}
              </UiLink>
            </UiTableCell>
            <UiTableCell>{{ yearsLabel(domain.regPeriodYears) }}</UiTableCell>
            <UiTableCell>{{ domain.registrar ?? '—' }}</UiTableCell>
            <UiTableCell numeric>{{ formatPrice(domain.recurringAmount, domain.priceCurrency) }}</UiTableCell>
            <UiTableCell muted>{{ formatDate(domain.nextDueDate) }}</UiTableCell>
            <UiTableCell muted>{{ formatDate(domain.expiresAt) }}</UiTableCell>
            <UiTableCell>
              <UiBadge :tone="domainStatusTone(domain.status)" small>
                {{ statusLabel(DOMAIN_STATUS_OPTIONS, domain.status) }}
              </UiBadge>
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
            testid="domain-registrations-pagination"
            @update:page="setPage"
          />
        </UiStack>
      </template>
    </UiCard>

    <UiCard v-else>
      <UiEmptyState
        :title="t('clients.domainRegistrations.noDomainRegistrationsFound')"
        data-testid="domain-registrations-empty"
      >
        <template #icon>
          <UiIcon :markup="Icon.Globe" size="lg" />
        </template>
      </UiEmptyState>
    </UiCard>
  </UiStack>
</template>
