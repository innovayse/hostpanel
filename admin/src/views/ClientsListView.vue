<script setup lang="ts">
/**
 * Clients list view: paginated table with advanced filter bar.
 */
import { onMounted, ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useClientsStore } from '@/stores/clients'
import type { ClientFilters } from '@/types/client'
import { ClientStatus } from '@/types/client'
import { clientStatusTone } from '@/lib/tones'
import { formatDate } from '@/lib/format'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiFilterCard from '@/components/ui/UiFilterCard.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiPagination from '@/components/ui/UiPagination.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiSkeleton from '@/components/ui/UiSkeleton.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import UiTooltip from '@/components/ui/UiTooltip.vue'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiAlignItems, UiJustify } from '@/utils/ui/layout'
import { UiSize } from '@/utils/ui/size'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'
import { Key } from '@/utils/ui/keys'
import { AppRoute, clientPath } from '@/lib/routes'

const { t } = useI18n()

/** Icon markup (shape tags) of the add button. */
const ADD_ICON = '<line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/>'

/** Icon markup (shape tags) of the search button. */
const SEARCH_ICON = '<circle cx="11" cy="11" r="8"/><line x1="21" y1="21" x2="16.65" y2="16.65"/>'

/** Router, used to open a client or the add form. */
const router = useRouter()

/** Clients store, the only caller of the client endpoints. */
const store = useClientsStore()

/** Name / company filter. */
const nameFilter = ref('')

/** Email filter. */
const emailFilter = ref('')

/** Phone filter. */
const phoneFilter = ref('')

/** Status filter; empty means any. */
const statusFilter = ref<ClientStatus | ''>('')

/** Status filter options. */
const statusOptions = computed(() => [
  { value: '', label: t('shared.any') },
  { value: ClientStatus.Active, label: t('shared.active') },
  { value: ClientStatus.Inactive, label: t('shared.inactive') },
  { value: ClientStatus.Suspended, label: t('clients.clientsList.suspended') },
  { value: ClientStatus.Closed, label: t('clients.clientsList.closed') },
])

/**
 * Builds the current filter object from form state.
 *
 * @returns The filters the store sends.
 */
const currentFilters = (): ClientFilters => ({
  search: nameFilter.value || undefined,
  email: emailFilter.value || undefined,
  phone: phoneFilter.value || undefined,
  status: statusFilter.value || undefined,
})

/** True when any filter field has a value. */
const hasFilters = computed(() =>
  !!(nameFilter.value || emailFilter.value || phoneFilter.value || statusFilter.value),
)

/** Number of active filters. */
const activeFilterCount = computed(() =>
  [nameFilter.value, emailFilter.value, phoneFilter.value, statusFilter.value].filter(Boolean).length,
)

/**
 * Applies filters and resets to page 1.
 *
 * @returns Nothing.
 */
const applyFilters = (): void => {
  store.page = 1
  store.fetchAll(currentFilters())
}

/**
 * Clears all filters and re-fetches.
 *
 * @returns Nothing.
 */
const clearFilters = (): void => {
  nameFilter.value = ''
  emailFilter.value = ''
  phoneFilter.value = ''
  statusFilter.value = ''
  store.page = 1
  store.fetchAll()
}

/**
 * Submits on Enter key in any input.
 *
 * @param e - The key event.
 * @returns Nothing.
 */
const onKeydown = (e: KeyboardEvent): void => {
  if (e.key === Key.Enter) {
    applyFilters()
  }
}

/**
 * Navigate to a page.
 *
 * @param p - Target page number.
 * @returns Nothing.
 */
const goToPage = (p: number): void => {
  store.page = p
  store.fetchAll(currentFilters())
}

/**
 * Opens a client's detail page.
 *
 * @param id - The client id.
 * @returns Nothing.
 */
const openClient = (id: number): void => {
  router.push(clientPath(id))
}

onMounted(() => store.fetchAll())
</script>

<template>
  <UiStack>
    <UiPageHeader :title="t('shared.clients')" :subtitle="t('clients.clientsList.manageYourClientAccounts')">
      <template #actions>
        <UiLink
          :to="AppRoute.ClientsAdd"
          :variant="UiButtonVariant.Primary"
          data-testid="clients-list-add"
        >
          <UiIcon :markup="ADD_ICON" :size="UiSize.Sm" />
          {{ t('clients.clientsList.addClient') }}
        </UiLink>
      </template>
    </UiPageHeader>

    <UiFilterCard>
      <template #fields>
        <UiGrid :columns="4" gap="sm" container class="items-end">
          <UiField :label="t('clients.clientsList.clientCompanyName')">
            <template #default="{ id }">
              <UiInput
                :id="id"
                v-model="nameFilter"
                type="text"
                :placeholder="t('clients.clientsList.searchByName')"
                data-testid="clients-list-filter-name"
                @keydown="onKeydown"
              />
            </template>
          </UiField>
          <UiField :label="t('shared.emailAddress')">
            <template #default="{ id }">
              <UiInput
                :id="id"
                v-model="emailFilter"
                type="text"
                :placeholder="t('clients.clientsList.searchByEmail')"
                data-testid="clients-list-filter-email"
                @keydown="onKeydown"
              />
            </template>
          </UiField>
          <UiField :label="t('shared.phoneNumber')">
            <template #default="{ id }">
              <UiInput
                :id="id"
                v-model="phoneFilter"
                type="text"
                :placeholder="t('clients.clientsList.searchByPhone')"
                data-testid="clients-list-filter-phone"
                @keydown="onKeydown"
              />
            </template>
          </UiField>
          <UiField :label="t('shared.status')">
            <UiSelect v-model="statusFilter" :options="statusOptions" :placeholder="t('shared.any')" data-testid="clients-list-filter-status" />
          </UiField>
        </UiGrid>
      </template>
      <template #actions>
        <UiStack direction="row" :justify="UiJustify.End" :align="UiAlignItems.Center" gap="sm">
          <UiButton
            v-if="hasFilters"
            :variant="UiButtonVariant.Ghost"
            data-testid="clients-list-clear"
            @click="clearFilters"
          >
            {{ t('shared.clear') }}
          </UiButton>
          <UiButton
            :variant="UiButtonVariant.Primary"
            data-testid="clients-list-search"
            @click="applyFilters"
          >
            <UiIcon :markup="SEARCH_ICON" :size="UiSize.Sm" />
            {{ t('shared.search2') }}
            <UiBadge v-if="activeFilterCount > 0" :tone="UiTone.Neutral" small>{{ activeFilterCount }}</UiBadge>
          </UiButton>
        </UiStack>
      </template>
    </UiFilterCard>

    <UiStack v-if="store.loading && store.clients.length === 0" data-testid="clients-list-loading">
      <UiSkeleton :lines="3" />
      <UiText :tone="UiTextTone.Secondary">{{ t('clients.clientsList.loadingClients') }}</UiText>
    </UiStack>

    <UiErrorState
      v-else-if="store.error"
      :message="store.error"
      :loading="store.loading"
      testid="clients-list-error"
      @retry="store.fetchAll()"
    />

    <UiCard v-else-if="store.clients.length > 0">
      <UiTable density="comfortable" data-testid="clients-list-table">
        <UiTableHead>
          <UiTableRow>
            <UiTableHeader>{{ t('shared.id') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.name') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.email') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.company') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.status') }}</UiTableHeader>
            <UiTableHeader>{{ t('clients.clientsList.2fa') }}</UiTableHeader>
            <UiTableHeader>{{ t('clients.clientsList.joined') }}</UiTableHeader>
          </UiTableRow>
        </UiTableHead>
        <UiTableBody divided>
          <UiTableRow
            v-for="client in store.clients"
            :key="client.id"
            hoverable
            class="cursor-pointer"
            data-testid="client-row"
            @click="openClient(client.id)"
          >
            <UiTableCell muted>{{ client.id }}</UiTableCell>
            <UiTableCell>
              <UiStack direction="row" :align="UiAlignItems.Center" gap="sm">
                <UiBadge :tone="UiTone.Primary" small>
                  {{ client.firstName.charAt(0) }}{{ client.lastName.charAt(0) }}
                </UiBadge>
                {{ client.firstName }} {{ client.lastName }}
              </UiStack>
            </UiTableCell>
            <UiTableCell muted>{{ client.email }}</UiTableCell>
            <UiTableCell muted>{{ client.companyName || '—' }}</UiTableCell>
            <UiTableCell>
              <UiBadge v-if="client.isUserDeleted" :tone="UiTone.Danger" small>
                {{ t('clients.clientsList.userDeleted') }}
              </UiBadge>
              <UiBadge v-else :tone="clientStatusTone(client.status)" small>{{ client.status }}</UiBadge>
            </UiTableCell>
            <UiTableCell>
              <UiTooltip
                :text="client.twoFactorEnabled
                  ? t('clients.clientsList.twoFactorAuthenticationEnabled')
                  : t('clients.clientsList.twoFactorAuthenticationDisabled')"
              >
                <UiBadge :tone="client.twoFactorEnabled ? UiTone.Success : UiTone.Neutral" small>
                  {{ client.twoFactorEnabled ? t('clients.clientsList.on') : t('clients.clientsList.off') }}
                </UiBadge>
              </UiTooltip>
            </UiTableCell>
            <UiTableCell muted>{{ formatDate(client.createdAt) }}</UiTableCell>
          </UiTableRow>
        </UiTableBody>
      </UiTable>
      <UiStack
        v-if="store.totalCount > store.pageSize"
        direction="row"
        :justify="UiJustify.Between"
        :align="UiAlignItems.Center"
      >
        <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">
          {{ t('clients.clientsList.totalcountClientValue', { totalCount: store.totalCount }) }}
        </UiText>
        <UiPagination
          :page="store.page"
          :total="store.totalCount"
          :page-size="store.pageSize"
          testid="clients-list-pagination"
          @update:page="goToPage"
        />
      </UiStack>
    </UiCard>

    <UiCard v-else>
      <UiEmptyState
        :title="hasFilters ? t('clients.clientsList.noClientsMatchYourFilters') : t('clients.clientsList.noClientsFound')"
      >
        <template v-if="hasFilters" #actions>
          <UiButton
            :variant="UiButtonVariant.Link"
            data-testid="clients-list-clear-empty"
            @click="clearFilters"
          >
            {{ t('clients.clientsList.clearFilters') }}
          </UiButton>
        </template>
      </UiEmptyState>
    </UiCard>
  </UiStack>
</template>
