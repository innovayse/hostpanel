<script setup lang="ts">
/**
 * Clients list view: a search box, the filter dialog with its removable chips, and a paged table.
 *
 * The filters, the search text and the page live in the address (`useFilterQuery`), so a reload, a shared link and
 * the back button restore the same list; the view loads whenever the address changes. The table scrolls inside its
 * card under a sticky header and the pager sits under it.
 */
import { computed, onMounted, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useClientsStore } from '@/stores/clients'
import { useClientGroupsStore } from '@/stores/clientGroups'
import { useFilterQuery } from '@/composables/useFilterQuery'
import type { ClientFilters } from '@/types/client'
import { ClientStatus } from '@/types/client'
import { clientStatusTone } from '@/lib/tones'
import { formatDate } from '@/lib/format'
import { Icon } from '@/lib/icons'
import { PAGE_SIZE_OPTIONS } from '@/lib/limits'
import ClientGroupBadge from '@/components/clients/ClientGroupBadge.vue'
import ClientGroupSelect from '@/components/clients/ClientGroupSelect.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiListToolbar from '@/components/ui/UiListToolbar.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiPagination from '@/components/ui/UiPagination.vue'
import UiRowActions from '@/components/ui/UiRowActions.vue'
import type { UiMenuAction } from '@/utils/ui/menu'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiText from '@/components/ui/UiText.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiTooltip from '@/components/ui/UiTooltip.vue'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiAlignItems } from '@/utils/ui/layout'
import { UiSize } from '@/utils/ui/size'
import { actionsColumnClasses } from '@/utils/ui/table'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import type { UiFilterChip } from '@/utils/ui/filter'
import { UiTone } from '@/utils/ui/tone'
import { AppRoute, clientPath } from '@/lib/routes'
import { UiTableDensity } from '@/utils/ui/injection'

const { t } = useI18n()

/** The filter fields the dialog edits; each is one query key and one `ClientFilters` member. */
const FilterKey = {
  Email: 'email',
  Phone: 'phone',
  Status: 'status',
  ClientGroupId: 'clientGroupId',
} as const

/** One of the {@link FilterKey} values. */
type FilterKey = (typeof FilterKey)[keyof typeof FilterKey]

/** Router, used to open a client. */
const router = useRouter()

/** Clients store, the only caller of the client endpoints. */
const store = useClientsStore()

/** Client groups store, read to name the group of an applied group filter. */
const groupsStore = useClientGroupsStore()

/** Filters, search text and page kept in the address. */
const { applied, draft, search, appliedSearch, page, activeCount, apply, reset, cancel, remove, clearAll, setPage }
  = useFilterQuery(Object.values(FilterKey))

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

/** True while the first load is in flight and there are no rows yet; the toolbar and the table then keep their layout with placeholders. */
const initialLoading = computed<boolean>(() => store.loading && store.clients.length === 0)

/** Whether the list is narrowed by the search or by a filter. */
const narrowed = computed<boolean>(() => activeCount.value > 0 || appliedSearch.value !== '')

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
const chips = computed<UiFilterChip<FilterKey>[]>(() => {
  const values = applied.value
  const status = statusOptions.value.find(option => option.value === values.status)
  const all: UiFilterChip<FilterKey>[] = [
    { key: FilterKey.Email, label: t('shared.emailAddress'), value: values.email },
    { key: FilterKey.Phone, label: t('shared.phoneNumber'), value: values.phone },
    { key: FilterKey.Status, label: t('shared.status'), value: status?.label ?? values.status },
    { key: FilterKey.ClientGroupId, label: t('clients.clientGroups.group'), value: groupName.value },
  ]
  return all.filter(chip => values[chip.key] !== '')
})

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
    clientGroupId: values.clientGroupId === '' ? undefined : Number(values.clientGroupId),
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

/**
 * The part of the address the list is loaded from. The router replaces the query object on every navigation, so
 * watching the values themselves would reload the list when the reader leaves for another screen; the key only
 * changes when a filter, the search or the page does.
 */
const listKey = computed<string>(() => JSON.stringify([applied.value, appliedSearch.value, page.value]))

// Every change of a filter, the search or the page loads the list the address names.
watch(listKey, load)

onMounted(() => {
  void load()
  void groupsStore.fetchAll()
})
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
          <UiIcon :markup="Icon.Add" :size="UiSize.Sm" />
          {{ t('clients.clientsList.addClient') }}
        </UiLink>
      </template>
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

    <UiCard v-else-if="initialLoading || store.clients.length > 0">
      <UiTable
        :density="UiTableDensity.Comfortable"
        :scrollable="false"
        :loading="initialLoading"
        data-testid="clients-list-table"
      >
        <UiTableHead>
          <UiTableRow>
            <UiTableHeader hide-below="md">{{ t('shared.id') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.name') }}</UiTableHeader>
            <UiTableHeader hide-below="lg">{{ t('shared.email') }}</UiTableHeader>
            <UiTableHeader hide-below="xl">{{ t('shared.company') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.status') }}</UiTableHeader>
            <UiTableHeader hide-below="xl">{{ t('clients.clientGroups.group') }}</UiTableHeader>
            <UiTableHeader hide-below="xl">{{ t('clients.clientsList.2fa') }}</UiTableHeader>
            <UiTableHeader hide-below="lg">{{ t('clients.clientsList.joined') }}</UiTableHeader>
            <UiTableHeader :class="actionsColumnClasses">
              <span class="sr-only">{{ t('shared.actions') }}</span>
            </UiTableHeader>
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
            <UiTableCell muted hide-below="md">{{ client.id }}</UiTableCell>
            <UiTableCell truncate>
              <UiStack direction="row" :align="UiAlignItems.Center" gap="sm">
                <UiBadge :tone="UiTone.Primary" small class="shrink-0">
                  {{ client.firstName.charAt(0) }}{{ client.lastName.charAt(0) }}
                </UiBadge>
                <UiStack gap="none" class="min-w-0">
                  <UiText class="truncate">{{ client.firstName }} {{ client.lastName }}</UiText>
                  <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Xs" class="truncate lg:hidden">{{ client.email }}</UiText>
                </UiStack>
              </UiStack>
            </UiTableCell>
            <UiTableCell muted hide-below="lg">{{ client.email }}</UiTableCell>
            <UiTableCell muted hide-below="xl">{{ client.companyName || '—' }}</UiTableCell>
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
              <template v-else>—</template>
            </UiTableCell>
            <UiTableCell hide-below="xl">
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
            <UiTableCell muted hide-below="lg">{{ formatDate(client.createdAt) }}</UiTableCell>
            <UiTableCell :class="actionsColumnClasses" @click.stop>
              <UiRowActions :actions="rowActions(client.id)" :testid="`client-row-actions-${client.id}`" />
            </UiTableCell>
          </UiTableRow>
        </UiTableBody>
      </UiTable>
      <template #footer>
        <UiPagination
          :page="page"
          :total="store.totalCount"
          :page-size="store.pageSize"
          :page-size-options="PAGE_SIZE_OPTIONS"
          :loading="initialLoading"
          testid="clients-list-pagination"
          @update:page="setPage"
          @update:page-size="changePageSize"
        />
      </template>
    </UiCard>

    <UiCard v-else>
      <UiEmptyState
        :title="narrowed ? t('clients.clientsList.noClientsMatchYourFilters') : t('clients.clientsList.noClientsFound')"
      >
        <template v-if="narrowed" #actions>
          <UiButton
            :variant="UiButtonVariant.Link"
            data-testid="clients-list-clear-empty"
            @click="clearAll"
          >
            {{ t('clients.clientsList.clearFilters') }}
          </UiButton>
        </template>
      </UiEmptyState>
    </UiCard>
  </UiStack>
</template>
