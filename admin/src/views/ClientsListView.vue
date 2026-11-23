<script setup lang="ts">
/**
 * Clients list view: a search box, the filter dialog with its removable chips, and a paged table.
 *
 * The filters, the search text and the page live in the address (`useFilterQuery`), so a reload, a shared link and
 * the back button restore the same list; the view loads whenever the address changes. The table scrolls inside its
 * card and the pager sits in the card's footer.
 */
import { computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useClientsStore } from '@/stores/clients'
import { useClientGroupsStore } from '@/stores/clientGroups'
import { useListQuery } from '@/composables/useListQuery'
import type { ClientFilters } from '@/types/client'
import { ClientStatus } from '@/types/client'
import { clientStatusTone } from '@/lib/tones'
import { formatDate, EMPTY_VALUE } from '@/lib/format'
import { Icon } from '@/lib/icons'
import { PAGE_SIZE_OPTIONS } from '@/lib/limits'
import ClientGroupBadge from '@/components/clients/ClientGroupBadge.vue'
import ClientGroupSelect from '@/components/clients/ClientGroupSelect.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiDataTable from '@/components/ui/UiDataTable.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiListToolbar from '@/components/ui/UiListToolbar.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiRowActions from '@/components/ui/UiRowActions.vue'
import type { UiMenuAction } from '@/utils/ui/menu'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusIcon from '@/components/ui/UiStatusIcon.vue'
import UiText from '@/components/ui/UiText.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiSchemaTableHead from '@/components/ui/UiSchemaTableHead.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import schema from '@/views/ClientsListView.schema'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiAlignItems, UiDirection, UiGap } from '@/utils/ui/layout'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'
import { AppRoute, clientPath } from '@/lib/routes'
import { schemaHeader } from '@/lib/schemaHeader'
import { UiTableDensity } from '@/utils/ui/injection'
import type { UiHeaderAction } from '@/utils/ui/actionBar'

const { t } = useI18n()

/** The header actions, each with its icon; on a phone only the primary one stays a button. */
const headerActions = computed<UiHeaderAction[]>(() => [
  { label: t('clients.clientsList.createClient'), icon: Icon.Add, variant: UiButtonVariant.Primary, testid: 'clients-list-add', to: AppRoute.ClientsAdd },
])

/** The filter fields the dialog edits; each is one query key and one `ClientFilters` member. */
const FilterKey = {
  Email: 'email',
  Phone: 'phone',
  Status: 'status',
  ClientGroupId: 'clientGroupId',
} as const

/** Router, used to open a client. */
const router = useRouter()

/** Clients store, the only caller of the client endpoints. */
const store = useClientsStore()

/** Client groups store, read to name the group of an applied group filter. */
const groupsStore = useClientGroupsStore()

/** Filters, search text and page kept in the address. */
const { applied, draft, search, appliedSearch, page, activeCount, apply, reset, cancel, remove, clearAll, setPage, narrowed, loadOnChange, idOf, chipsOf }
  = useListQuery(Object.values(FilterKey))

/** Status filter options; the empty value means any. */
const statusOptions = computed(() => [
  { value: '', label: t('shared.any') },
  { value: ClientStatus.Active, label: t('shared.active') },
  { value: ClientStatus.Inactive, label: t('shared.inactive') },
  { value: ClientStatus.Suspended, label: t('clients.clientsList.suspended') },
  { value: ClientStatus.Closed, label: t('clients.clientsList.closed') },
])

/** The group being chosen in the dialog as the picker's id; null while no group is chosen. */
const groupDraft = computed<number | null>({
  /**
   * Reads the draft's group id as the picker's id.
   *
   * @returns The group id, or null when no group is chosen.
   */
  get: () => (draft.value.clientGroupId === '' ? null : Number(draft.value.clientGroupId)),
  /**
   * Writes the picked group back into the draft as text.
   *
   * @param id - The picked group id, or null when it is cleared.
   */
  set: (id) => {
    draft.value.clientGroupId = id === null ? '' : String(id)
  },
})

/** The group in the address as an id, undefined when none is set or the value is not an id. */
const groupIdOfAddress = idOf(FilterKey.ClientGroupId)

/** True while the first load is in flight and there are no rows yet; the toolbar and the table then keep their layout with placeholders. */
const initialLoading = computed<boolean>(() => store.loading && store.clients.length === 0)

/**
 * Names the applied group filter for its chip.
 *
 * @returns The group's name, or its id while the groups are still loading.
 */
const groupName = computed<string>(() => {
  const id = applied.value.clientGroupId
  return groupsStore.groups.find(group => String(group.id) === id)?.name ?? id
})

/** One chip per applied filter, with the value as the operator reads it. */
const chips = chipsOf(() => [
  { key: FilterKey.Email, label: t('shared.emailAddress'), value: applied.value.email },
  { key: FilterKey.Phone, label: t('shared.phoneNumber'), value: applied.value.phone },
  {
    key: FilterKey.Status,
    label: t('shared.status'),
    value: statusOptions.value.find(option => option.value === applied.value.status)?.label ?? applied.value.status,
  },
  { key: FilterKey.ClientGroupId, label: t('clients.clientGroups.group'), value: groupName.value },
])

/**
 * Loads the page the address names with the filters and search it holds.
 *
 * @returns Nothing; the store holds the result.
 */
const load = (): Promise<void> => {
  const values = applied.value
  const filters: ClientFilters = {
    search: appliedSearch.value || undefined,
    email: values.email || undefined,
    phone: values.phone || undefined,
    status: (values.status as ClientStatus) || undefined,
    clientGroupId: groupIdOfAddress.value,
  }
  return store.fetchAll(filters, page.value)
}

/**
 * Changes the page size and returns to the first page.
 *
 * @param size - The rows per page the reader chose.
 * @returns Nothing; the list loads again.
 */
const changePageSize = (size: number): void => {
  store.pageSize = size
  if (page.value > 1) {
    void setPage(1)
  } else {
    void load()
  }
}

/**
 * Builds the actions of a row: opening the client, as a real link.
 *
 * @param id - The client id.
 * @returns The row's actions.
 */
const rowActions = (id: number): UiMenuAction[] => [
  { label: t('shared.view'), icon: Icon.Eye, to: clientPath(id), testid: `client-row-open-${id}` },
]

/**
 * Opens a client's detail page.
 *
 * @param id - The client id.
 * @returns Nothing.
 */
const openClient = (id: number): void => {
  router.push(clientPath(id))
}

// Every change of a filter, the search or the page loads the list the address names.
loadOnChange(load)

onMounted(() => {
  void groupsStore.fetchAll()
})
</script>

<template>
  <UiStack>
    <UiPageHeader :actions="headerActions" v-bind="schemaHeader(schema, t)">
    </UiPageHeader>

    <UiListToolbar
      v-model:search="search"
      search-testid="clients-list-filter-name"
      :search-placeholder="t('clients.clientsList.searchPlaceholder')"
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
        <UiField :label="t('shared.emailAddress')">
          <template #default="{ id }">
            <UiInput
              :id="id"
              v-model="draft.email"
              type="text"
              :placeholder="t('clients.clientsList.searchByEmail')"
              data-testid="clients-list-filter-email"
            />
          </template>
        </UiField>
        <UiField :label="t('shared.phoneNumber')">
          <template #default="{ id }">
            <UiInput
              :id="id"
              v-model="draft.phone"
              type="text"
              :placeholder="t('clients.clientsList.searchByPhone')"
              data-testid="clients-list-filter-phone"
            />
          </template>
        </UiField>
        <UiField :label="t('shared.status')">
          <template #default="{ id }">
            <UiSelect
              :id="id"
              v-model="draft.status"
              :options="statusOptions"
              :placeholder="t('shared.any')"
              data-testid="clients-list-filter-status"
            />
          </template>
        </UiField>
        <UiField :label="t('clients.clientGroups.group')">
          <template #default="{ id }">
            <ClientGroupSelect
              :id="id"
              v-model="groupDraft"
              :empty-label="t('shared.any')"
              data-testid="clients-list-filter-group"
            />
          </template>
        </UiField>
      </UiStack>
    </UiListToolbar>

    <UiErrorState
      v-if="store.error"
      :message="store.error"
      :loading="store.loading"
      testid="clients-list-error"
      @retry="load"
    />

    <UiDataTable
      v-else
      :density="UiTableDensity.Comfortable"
      :loading="initialLoading"
      :empty="!initialLoading && store.clients.length === 0"
      :page="page"
      :total="store.totalCount"
      :page-size="store.pageSize"
      :page-size-options="PAGE_SIZE_OPTIONS"
      pagination-testid="clients-list-pagination"
      data-testid="clients-list-table"
      @update:page="setPage"
      @update:page-size="changePageSize"
    >
      <UiSchemaTableHead :columns="schema.table.columns" />
      <UiTableBody>
        <UiTableRow
          v-for="client in store.clients"
          :key="client.id"
          clickable
          data-testid="client-row"
          @click="openClient(client.id)"
        >
          <UiTableCell muted hide-below="md">{{ client.id }}</UiTableCell>
          <UiTableCell>
            <UiStack :direction="UiDirection.Row" :align="UiAlignItems.Center" :gap="UiGap.Sm">
              <UiBadge :tone="UiTone.Primary" small class="shrink-0">
                {{ client.firstName.charAt(0) }}{{ client.lastName.charAt(0) }}
              </UiBadge>
              <UiStack :gap="UiGap.None" class="min-w-0">
                <UiText truncate>{{ client.firstName }} {{ client.lastName }}</UiText>
                <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Xs" truncate class="lg:hidden">{{ client.email }}</UiText>
              </UiStack>
            </UiStack>
          </UiTableCell>
          <UiTableCell muted hide-below="lg">{{ client.email }}</UiTableCell>
          <UiTableCell muted hide-below="xl">{{ client.companyName || EMPTY_VALUE }}</UiTableCell>
          <UiTableCell>
            <UiBadge v-if="client.isUserDeleted" :tone="UiTone.Danger" small>
              {{ t('clients.clientsList.userDeleted') }}
            </UiBadge>
            <UiBadge v-else :tone="clientStatusTone(client.status)" small>{{ client.status }}</UiBadge>
          </UiTableCell>
          <UiTableCell hide-below="xl">
            <ClientGroupBadge
              v-if="client.clientGroupName && client.clientGroupColor"
              :name="client.clientGroupName"
              :color="client.clientGroupColor"
            />
            <template v-else>{{ EMPTY_VALUE }}</template>
          </UiTableCell>
          <UiTableCell hide-below="xl">
            <UiStatusIcon
              :state="client.twoFactorEnabled"
              :on-icon="Icon.ShieldCheck"
              :off-icon="Icon.ShieldOff"
              :on-label="t('clients.clientsList.twoFactorAuthenticationEnabled')"
              :off-label="t('clients.clientsList.twoFactorAuthenticationDisabled')"
            />
          </UiTableCell>
          <UiTableCell muted hide-below="lg">{{ formatDate(client.createdAt) }}</UiTableCell>
          <UiTableCell actions @click.stop>
            <UiRowActions :actions="rowActions(client.id)" :testid="`client-row-actions-${client.id}`" />
          </UiTableCell>
        </UiTableRow>
      </UiTableBody>
      <template #empty>
        <UiEmptyState
          :title="narrowed ? t('clients.clientsList.noClientsMatchYourFilters') : t('clients.clientsList.noClientsFound')"
        >
          <template v-if="narrowed" #actions>
            <UiButton
              :variant="UiButtonVariant.Link"
              data-testid="clients-list-clear-empty"
              @click="clearAll"
            >
              {{ t('shared.clearFilters') }}
            </UiButton>
          </template>
        </UiEmptyState>
      </template>
    </UiDataTable>
  </UiStack>
</template>
