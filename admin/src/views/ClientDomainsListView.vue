<script setup lang="ts">
/**
 * Client domains list: shows all domain registrations belonging to the current client.
 * Navigates to the domain detail page when a row is clicked. The page lives in the address (`useFilterQuery`),
 * so a reload or a shared link opens the same page.
 */
import { ref, computed, onMounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useDomainsStore } from '@/stores/domains'
import { DOMAIN_STATUS_OPTIONS } from '@/lib/constants'
import { domainStatusTone, statusLabel } from '@/lib/tones'
import { formatDate, EMPTY_VALUE } from '@/lib/format'
import type { DomainRegistration } from '@/types/domain'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiDataTable from '@/components/ui/UiDataTable.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiSchemaTableHead from '@/components/ui/UiSchemaTableHead.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import schema from '@/views/ClientDomainsListView.schema'
import UiStatusBadge from '@/components/ui/UiStatusBadge.vue'
import { DEFAULT_PAGE_SIZE } from '@/lib/limits'
import { ClientSection, clientItemPath } from '@/lib/routes'
import { useFilterQuery } from '@/composables/useFilterQuery'
import { UiTableDensity } from '@/utils/ui/injection'

const { t } = useI18n()

/** Current route, source of the client id. */
const route = useRoute()

/** Router, used to open a domain. */
const router = useRouter()

/** Domains store, the only caller of the domain endpoints. */
const store = useDomainsStore()

/** Client ID from route params. */
const clientId = computed(() => route.params.id as string)

/** Loaded domains list for the current page. */
const domains = ref<DomainRegistration[]>([])

/** True while a request is in flight (owned by the store). */
const loading = computed(() => store.loading)

/** True while the first load is in flight and there are no rows yet; the table then draws its placeholder rows. */
const initialLoading = computed(() => loading.value && domains.value.length === 0)

/** Error message, null when no error (owned by the store). */
const error = computed(() => store.error)

/** The page of the list, kept in the address. */
const { page, setPage } = useFilterQuery([])

/** Items per page. */
const pageSize = ref(DEFAULT_PAGE_SIZE)

/** Total number of matching items across all pages. */
const totalCount = ref(0)

/**
 * Fetches paginated domains for this client from the backend.
 *
 * @returns Promise that resolves when data is loaded.
 */
const fetchDomains = async (): Promise<void> => {
  const result = await store.fetchClientPage(clientId.value, page.value, pageSize.value)
  if (result) {
    domains.value = result.items
    totalCount.value = result.totalCount
  }
}

/**
 * Navigates to the domain detail page for the given domain.
 *
 * @param domainId - The ID of the domain to view.
 * @returns Nothing.
 */
const viewDomain = (domainId: number): void => {
  router.push(clientItemPath(clientId.value, ClientSection.Domains, domainId))
}

// A page chosen, or the back button, loads the page the address names.
watch(page, fetchDomains)

onMounted(() => fetchDomains())
</script>

<template>
  <UiStack>
    <UiErrorState
      v-if="error"
      :message="error"
      :loading="loading"
      testid="client-domains-error"
      @retry="fetchDomains()"
    />

    <UiDataTable
      v-else
      :density="UiTableDensity.Comfortable"
      :loading="initialLoading"
      :empty="!initialLoading && domains.length === 0"
      :empty-text="t('clients.clientDomainsList.noDomainsFoundForThis')"
      :page="page"
      :total="totalCount"
      :page-size="pageSize"
      pagination-testid="client-domains-pagination"
      data-testid="client-domains-table"
      @update:page="setPage"
    >
      <UiSchemaTableHead :columns="schema.table.columns" />
      <UiTableBody>
        <UiTableRow
          v-for="domain in domains"
          :key="domain.id"
          hoverable
          clickable
          :data-testid="`client-domains-row-${domain.id}`"
          @click="viewDomain(domain.id)"
        >
          <UiTableCell muted>{{ domain.id }}</UiTableCell>
          <UiTableCell>{{ domain.name }}</UiTableCell>
          <UiTableCell muted>{{ domain.registrar ?? EMPTY_VALUE }}</UiTableCell>
          <UiTableCell>
            <UiBadge :tone="domainStatusTone(domain.status)" small>{{ statusLabel(DOMAIN_STATUS_OPTIONS, domain.status) }}</UiBadge>
          </UiTableCell>
          <UiTableCell muted>{{ formatDate(domain.expiresAt) }}</UiTableCell>
          <UiTableCell>
            <UiStatusBadge
              :state="domain.autoRenew"
              :on-label="t('shared.yes')"
              :off-label="t('shared.no')"
            />
          </UiTableCell>
        </UiTableRow>
      </UiTableBody>
    </UiDataTable>
  </UiStack>
</template>
