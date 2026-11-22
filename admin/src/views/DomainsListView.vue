<script setup lang="ts">
/**
 * Domains list view: a search box, the filter dialog with its removable chips, and a paged table of every domain
 * registration across clients.
 *
 * The search text, the status and registrar filters and the page live in the address (`useFilterQuery`), so a reload,
 * a shared link and the back button restore the same list, and the API applies them before it pages. Shows domain
 * name, client, registrar, registration period, status, renewal dates, renewal price and the auto-renew flag. A domain
 * name opens its detail page under the owning client. Reads `stores/domains`; a refused load shows `store.error`.
 */
import { computed, onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import { useFallbackLabels } from '@/composables/useFallbackLabels'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiListToolbar from '@/components/ui/UiListToolbar.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiPagination from '@/components/ui/UiPagination.vue'
import UiRowActions from '@/components/ui/UiRowActions.vue'
import type { UiMenuAction } from '@/utils/ui/menu'
import UiResultCount from '@/components/ui/UiResultCount.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusIcon from '@/components/ui/UiStatusIcon.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiSchemaTableHead from '@/components/ui/UiSchemaTableHead.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import schema from '@/views/DomainsListView.schema'
import { Icon } from '@/lib/icons'
import { useRegistrarModuleOptions } from '@/composables/useRegistrarModuleOptions'
import { DOMAIN_STATUS_OPTIONS } from '@/lib/constants'
import { EMPTY_VALUE, formatDate } from '@/lib/format'
import { formatPrice } from '@/lib/money'
import { domainStatusTone, statusLabel } from '@/lib/tones'
import { useDomainsStore } from '@/stores/domains'
import { DomainStatus } from '@/types/domain'
import type { DomainListFilters, DomainRegistration } from '@/types/domain'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiTableDensity } from '@/utils/ui/injection'
import { useListQuery } from '@/composables/useListQuery'
import { ClientSection, clientItemPath, clientPath } from '@/lib/routes'
import { schemaHeader } from '@/lib/schemaHeader'
import { UiTextSize } from '@/utils/ui/text'

const { t } = useI18n()

/** The filter fields the dialog edits; each is one query key and one `DomainListFilters` member. */
const FilterKey = {
  Status: 'status',
  RegistrarModule: 'registrarModule',
} as const

/** Domains store, the only caller of the domain endpoints. */
const store = useDomainsStore()

/** Fallback wording for a registration period the API leaves unknown. */
const { yearsLabel } = useFallbackLabels()

/** The filters, the search text and the page of the list, kept in the address. */
const { applied, draft, search, appliedSearch, page, activeCount, apply, reset, cancel, remove, clearAll, setPage, narrowed, loadOnChange, enumOf, chipsOf }
  = useListQuery(Object.values(FilterKey))

/** Status filter options; the empty value means any. */
const statusOptions = computed(() => [{ value: '', label: t('shared.any') }, ...DOMAIN_STATUS_OPTIONS])

/** The installed registrar plugins a domain can be filtered by, and the name of a stored module. */
const { options: registrarModuleOptions, load: loadRegistrars, labelOf: registrarLabel } = useRegistrarModuleOptions()

/** Registrar filter options; the empty value means any. */
const registrarOptions = computed(() => [{ value: '', label: t('shared.any') }, ...registrarModuleOptions.value])

/** The status in the address as a domain status, undefined when none is set or the value is not one. */
const statusOfAddress = enumOf(FilterKey.Status, DomainStatus)

/** The registrar in the address (a plugin id), undefined when none is set. */
const registrarOfAddress = computed<string | undefined>(() => applied.value[FilterKey.RegistrarModule] || undefined)

/** What the address narrows the list by, as the store sends it to the API. */
const filters = computed<DomainListFilters>(() => ({
  search: appliedSearch.value,
  status: statusOfAddress.value,
  registrarModule: registrarOfAddress.value,
}))

/** One chip per applied filter, with the value as the operator reads it. */
const chips = chipsOf(() => [
  {
    key: FilterKey.Status,
    label: t('shared.status'),
    value: statusOfAddress.value === undefined ? undefined : statusLabel(DOMAIN_STATUS_OPTIONS, statusOfAddress.value),
  },
  {
    key: FilterKey.RegistrarModule,
    label: t('shared.registrar'),
    value: registrarOfAddress.value === undefined ? undefined : registrarLabel(registrarOfAddress.value),
  },
])

/**
 * Loads the page the address names with the filters and search it holds.
 *
 * @returns True when loaded; false when refused (see `store.error`).
 */
const load = (): Promise<boolean> => store.fetchAll(page.value, filters.value)

// Every change of a filter, the search or the page loads the list the address names.
loadOnChange(load)

// The registrar filter lists the installed registrar plugins.
onMounted(loadRegistrars)

/** Whether the current page has any domain to list. */
const hasRows = computed<boolean>(() => store.domains.length > 0)

/** True while the first load is in flight and there are no rows yet; the toolbar and the table then keep their layout. */
const initialLoading = computed<boolean>(() => store.loading && !hasRows.value)

/**
 * The row actions of a domain: its one action opens the domain under its client as a real link.
 *
 * @param domain - The domain of the row.
 * @returns The action list passed to UiRowActions.
 */
const domainActions = (domain: DomainRegistration): UiMenuAction[] => [
  {
    label: t('shared.view'),
    icon: Icon.Eye,
    testid: `domains-view-${domain.id}`,
    to: clientItemPath(domain.clientId, ClientSection.Domains, domain.id),
  },
]
</script>

<template>
  <UiStack>
    <UiPageHeader
      v-bind="schemaHeader(schema, t)"
      data-testid="domains-header"
    />

    <UiListToolbar
      v-model:search="search"
      search-testid="domains-search"
      :search-placeholder="t('domains.domainsList.searchDomains')"
      :active-count="activeCount"
      :chips="chips"
      :loading="initialLoading"
      @apply="apply"
      @reset="reset"
      @cancel="cancel"
      @remove="remove"
      @clear="clearAll"
    >
      <UiStack>
        <UiField :label="t('shared.status')">
          <template #default="{ id }">
            <UiSelect
              :id="id"
              v-model="draft.status"
              :options="statusOptions"
              :placeholder="t('shared.any')"
              data-testid="domains-filter-status"
            />
          </template>
        </UiField>
        <UiField :label="t('shared.registrar')">
          <template #default="{ id }">
            <UiSelect
              :id="id"
              v-model="draft.registrarModule"
              :options="registrarOptions"
              :placeholder="t('shared.any')"
              data-testid="domains-filter-registrar"
            />
          </template>
        </UiField>
      </UiStack>
    </UiListToolbar>

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
        :loading="initialLoading"
        data-testid="domains-table"
      >
        <UiSchemaTableHead :columns="schema.table.columns" />
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
            <UiTableCell muted hide-below="lg">{{ domain.registrar ?? EMPTY_VALUE }}</UiTableCell>
            <UiTableCell muted hide-below="xl">{{ yearsLabel(domain.regPeriodYears) }}</UiTableCell>
            <UiTableCell>
              <UiBadge :tone="domainStatusTone(domain.status)" small>
                {{ statusLabel(DOMAIN_STATUS_OPTIONS, domain.status) }}
              </UiBadge>
            </UiTableCell>
            <UiTableCell muted hide-below="xl">{{ formatDate(domain.nextDueDate) }}</UiTableCell>
            <UiTableCell muted>{{ formatDate(domain.expiresAt) }}</UiTableCell>
            <UiTableCell numeric>{{ formatPrice(domain.recurringAmount, domain.priceCurrency) }}</UiTableCell>
            <UiTableCell>
              <UiStatusIcon
                :state="domain.autoRenew"
                :on-icon="Icon.CheckCircle"
                :off-icon="Icon.Close"
                :on-label="t('shared.yes')"
                :off-label="t('shared.no')"
              />
            </UiTableCell>
            <UiTableCell actions>
              <UiRowActions :actions="domainActions(domain)" :testid="`domains-actions-${domain.id}`" />
            </UiTableCell>
          </UiTableRow>
        </UiTableBody>
      </UiTable>

      <template #footer>
        <UiStack direction="row" justify="between" align="center" wrap>
          <UiResultCount :loading="initialLoading" :size="UiTextSize.Xs">
            {{ t('shared.totalcountDomainValue', { totalCount: store.totalCount }) }}
          </UiResultCount>
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
        :title="narrowed ? t('domains.domainsList.noDomainsMatchYourFilters') : t('domains.domainsList.noDomainsFound')"
        data-testid="domains-empty"
      >
        <template #icon>
          <UiIcon :markup="Icon.Globe" size="lg" />
        </template>
        <template v-if="activeCount > 0" #actions>
          <UiButton :variant="UiButtonVariant.Link" data-testid="domains-clear-empty" @click="clearAll">
            {{ t('domains.domainsList.clearFilters') }}
          </UiButton>
        </template>
      </UiEmptyState>
    </UiCard>
  </UiStack>
</template>
