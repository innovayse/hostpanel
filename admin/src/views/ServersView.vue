<script setup lang="ts">
/**
 * Main Servers management page.
 *
 * Lists the provisioning servers as cards grouped by module, with a search box, module and status
 * filters (kept in the address, shown as chips) and a pager, then shows server groups.
 * Provides Add/Edit/Delete actions for both servers and groups. Every refusal sentence belongs to
 * the servers store and is shown as the API returned it: a refused load with a retry, a refused save
 * inside its dialog, a delete (confirmed first in a danger prompt) or a saved dialog in the outcome
 * line under the header. The SSH key management dialog confirms its own saves: it stays open, since
 * its later steps use what was saved.
 */
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useServersStore } from '@/stores/servers'
import { FILL_TYPE_LABELS, MODULE_LABELS, ServerModule, ServerState } from '@/types/server'
import type { ServerDto, ServerGroupDto, ServerGroupPayload, ServerPayload } from '@/types/server'
import { UiTone } from '@/utils/ui/tone'
import GroupFormModal from '@/components/servers/GroupFormModal.vue'
import ServerFormModal from '@/components/servers/ServerFormModal.vue'
import ServerSshSettingsModal from '@/components/servers/ServerSshSettingsModal.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiListToolbar from '@/components/ui/UiListToolbar.vue'
import UiLoader from '@/components/ui/UiLoader.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiPagination from '@/components/ui/UiPagination.vue'
import UiProgress from '@/components/ui/UiProgress.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiRowActions from '@/components/ui/UiRowActions.vue'
import type { UiMenuAction } from '@/utils/ui/menu'
import { UiTableDensity } from '@/utils/ui/injection'
import type { UiSelectOption } from '@/utils/ui/select'
import type { UiFilterChip } from '@/utils/ui/filter'
import { DEFAULT_PAGE_SIZE } from '@/lib/limits'
import { pageSlice } from '@/lib/paging'
import { filterServers, serverLoadPercent, serverState } from '@/lib/servers'
import { useFilterQuery } from '@/composables/useFilterQuery'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import UiText from '@/components/ui/UiText.vue'
import { useResultBanner } from '@/composables/useResultBanner'
import { useDocsGuide } from '@/composables/useDocsGuide'
import PageHelpLink from '@/components/layout/PageHelpLink.vue'
import { EMPTY_VALUE } from '@/lib/format'

/** Translation function and the active locale (for date formatting). */
const { t, locale } = useI18n()

/** Servers store: the lists, the refusal sentences and the actions. */
const store = useServersStore()

/** Locale key of the caption of each server state. */
const STATE_KEYS: Record<ServerState, string> = {
  [ServerState.Online]: 'servers.servers.online',
  [ServerState.Offline]: 'servers.servers.offline',
  [ServerState.Untested]: 'servers.servers.notTested',
}

/** Badge tone of each server state. */
const STATE_TONES: Record<ServerState, UiTone> = {
  [ServerState.Online]: UiTone.Success,
  [ServerState.Offline]: UiTone.Danger,
  [ServerState.Untested]: UiTone.Neutral,
}

/** Load percentage from which a server's bar turns to the warning colour. */
const LOAD_WARN_AT = 80

/** Placeholder cards drawn in the servers grid while the list loads. */
const LOADING_CARD_COUNT = 3

/** Result of one connection test, as shown on the server's card. */
interface TestOutcome {
  /** Whether the server answered. */
  ok: boolean
  /** The sentence to show: the summary on success, the server's reason otherwise. */
  message: string
}

/** True while a server save is in flight. */
const savingServer = ref(false)

/** True while a group save is in flight. */
const savingGroup = ref(false)

/** Ids of the servers currently being tested. */
const testingIds = ref<number[]>([])

/** Last test result per server id. */
const testResults = ref<Record<number, TestOutcome>>({})

/** The server being edited, or null when creating. */
const editingServer = ref<ServerDto | null>(null)

/** True when the server form dialog is open. */
const showServerModal = ref(false)

/** The group being edited, or null when creating. */
const editingGroup = ref<ServerGroupDto | null>(null)

/** True when the group form dialog is open. */
const showGroupModal = ref(false)

/** The server awaiting delete confirmation, or null when nothing is staged. */
const deleteServerTarget = ref<ServerDto | null>(null)

/** Server whose SSH key management settings dialog is open, null when closed. */
const sshServerTarget = ref<ServerDto | null>(null)

/** The outcome line of the last save or delete. */
const feedback = useResultBanner()
/** Link to the docs guide for this screen, or null when docsUrl is not configured. */
const { guideUrl } = useDocsGuide()

/** Tone of the outcome line: danger for a refusal, success otherwise. */
const feedbackTone = computed<UiTone>(() => (feedback.isError.value ? UiTone.Danger : UiTone.Success))

/**
 * Shows the outcome of a delete: the success sentence, or the API's refusal.
 *
 * @param ok - Whether the API accepted the delete.
 */
const reportDelete = (ok: boolean): void => {
  if (ok) {
    feedback.show(t('shared.recordDeleted'), false)
  } else {
    feedback.show(store.actionError ?? t('errors.noAnswer'), true)
  }
}

/**
 * Loads the servers and the groups.
 *
 * @returns Resolves when both loads have settled.
 */
const loadAll = async (): Promise<void> => {
  await Promise.all([store.fetchServers(), store.fetchGroups()])
}

/** True while the staged server is being deleted. */
const deletingServer = ref(false)

/** The group awaiting delete confirmation, or null when nothing is staged. */
const deleteGroupTarget = ref<ServerGroupDto | null>(null)

/** True while the staged group is being deleted. */
const deletingGroup = ref(false)

/** The filters, the search text and the page of the list, kept in the address. */
const query = useFilterQuery(['module', 'state'] as const)

/** The module choices of the filter, "any" first. */
const moduleOptions = computed<UiSelectOption<string>[]>(() => [
  { value: '', label: t('servers.servers.anyModule') },
  ...Object.values(ServerModule).map(module => ({ value: module, label: MODULE_LABELS[module] })),
])

/** The state choices of the filter, "any" first. */
const stateOptions = computed<UiSelectOption<string>[]>(() => [
  { value: '', label: t('servers.servers.anyState') },
  ...Object.values(ServerState).map(state => ({ value: state, label: t(STATE_KEYS[state]) })),
])

/** The servers that match the applied filters and the search text. */
const matchingServers = computed<ServerDto[]>(() =>
  filterServers(store.servers, { ...query.applied.value, search: query.appliedSearch.value }),
)

/** The servers of the current page. */
const pagedServers = computed<ServerDto[]>(() =>
  pageSlice(matchingServers.value, query.page.value, DEFAULT_PAGE_SIZE),
)

/** The active filters as removable chips. */
const filterChips = computed<UiFilterChip<'module' | 'state'>[]>(() => {
  const chips: UiFilterChip<'module' | 'state'>[] = []
  const { module, state } = query.applied.value
  if (module !== '') {
    chips.push({ key: 'module', label: t('servers.servers.filterModule'), value: MODULE_LABELS[module as ServerModule] ?? module })
  }
  if (state !== '') {
    chips.push({ key: 'state', label: t('servers.servers.filterState'), value: t(STATE_KEYS[state as ServerState]) })
  }
  return chips
})

/**
 * Tests connectivity to a server and shows the result in its row.
 *
 * @param id - Server identifier.
 */
const handleTestConnection = async (id: number): Promise<void> => {
  testingIds.value = [...testingIds.value, id]
  testResults.value = Object.fromEntries(
    Object.entries(testResults.value).filter(([key]) => Number(key) !== id),
  )
  const result = await store.testConnection(id)
  let outcome: TestOutcome
  if (result === null) {
    outcome = { ok: false, message: store.actionError ?? '' }
  } else if (result.connected) {
    outcome = {
      ok: true,
      message: t('servers.servers.onlineDetails', {
        accounts: result.accountsCount ?? 0,
        version: result.version ?? '',
      }),
    }
  } else {
    outcome = { ok: false, message: result.errorMessage ?? t('servers.servers.connectionFailed') }
  }
  testResults.value = { ...testResults.value, [id]: outcome }
  testingIds.value = testingIds.value.filter(i => i !== id)
}

/**
 * Tone and caption of a server's last known state.
 *
 * @param server - The server.
 * @returns The badge tone and the locale key of its caption.
 */
const stateOf = (server: ServerDto): { tone: UiTone; key: string } => {
  const state = serverState(server)
  return { tone: STATE_TONES[state], key: STATE_KEYS[state] }
}

/**
 * Formats the account usage of a server.
 *
 * @param server - The server.
 * @returns `count / max`, `count`, or a dash when unknown.
 */
const accountsLabel = (server: ServerDto): string => {
  if (server.accountsCount === null) {
    return EMPTY_VALUE
  }
  return server.limits.maxAccounts === null ? `${server.accountsCount}` : `${server.accountsCount} / ${server.limits.maxAccounts}`
}

/** Opens the server create dialog. */
const openAddServer = (): void => {
  store.actionError = null
  editingServer.value = null
  showServerModal.value = true
}

/**
 * Opens the server edit dialog.
 *
 * @param server - The server to edit.
 */
const openEditServer = (server: ServerDto): void => {
  store.actionError = null
  editingServer.value = server
  showServerModal.value = true
}

/** Opens the group create dialog. */
const openAddGroup = (): void => {
  store.actionError = null
  editingGroup.value = null
  showGroupModal.value = true
}

/**
 * Opens the group edit dialog.
 *
 * @param group - The group to edit.
 */
const openEditGroup = (group: ServerGroupDto): void => {
  store.actionError = null
  editingGroup.value = group
  showGroupModal.value = true
}

/**
 * Saves a server (create or update).
 *
 * @param payload - Form data from ServerFormModal.
 */
const handleSaveServer = async (payload: ServerPayload): Promise<void> => {
  savingServer.value = true
  const saved = editingServer.value
    ? await store.updateServer(editingServer.value.id, payload)
    : await store.createServer(payload)
  savingServer.value = false
  if (saved) {
    showServerModal.value = false
    feedback.show(t('shared.changesSavedSuccessfully'), false)
  }
}

/** Deletes the staged server and clears the prompt. */
const confirmDeleteServer = async (): Promise<void> => {
  const target = deleteServerTarget.value
  if (!target) {
    return
  }
  feedback.clear()
  deletingServer.value = true
  const deleted = await store.deleteServer(target.id)
  deletingServer.value = false
  deleteServerTarget.value = null
  reportDelete(deleted)
}

/** Closes the server delete confirmation; ignored while the delete runs. */
const dismissDeleteServer = (): void => {
  if (!deletingServer.value) {
    deleteServerTarget.value = null
  }
}

/**
 * Saves a group (create or update).
 *
 * @param payload - Form data from GroupFormModal.
 */
const handleSaveGroup = async (payload: ServerGroupPayload): Promise<void> => {
  savingGroup.value = true
  const saved = editingGroup.value
    ? await store.updateGroup(editingGroup.value.id, payload)
    : await store.createGroup(payload)
  savingGroup.value = false
  if (saved) {
    showGroupModal.value = false
    feedback.show(t('shared.changesSavedSuccessfully'), false)
  }
}

/** Deletes the staged group and clears the prompt. */
const confirmDeleteGroup = async (): Promise<void> => {
  const target = deleteGroupTarget.value
  if (!target) {
    return
  }
  feedback.clear()
  deletingGroup.value = true
  const deleted = await store.deleteGroup(target.id)
  deletingGroup.value = false
  deleteGroupTarget.value = null
  reportDelete(deleted)
}

/** Closes the group delete confirmation; ignored while the delete runs. */
const dismissDeleteGroup = (): void => {
  if (!deletingGroup.value) {
    deleteGroupTarget.value = null
  }
}

/**
 * Row actions for a server. The "test connection" action is disabled while a test is already
 * running for that server so a second concurrent request cannot be sent.
 *
 * @param server - The server whose actions to build.
 * @returns The action list passed to UiRowActions.
 */
const serverActions = (server: ServerDto): UiMenuAction[] => {
  /**
   * Forwards to {@link handleTestConnection} for this server row.
   * @returns Nothing.
   */
  const onTest = (): void => { handleTestConnection(server.id) }
  /**
   * Opens the SSH key management dialog for this server row.
   * @returns Nothing.
   */
  const onSsh = (): void => { sshServerTarget.value = server }
  /**
   * Opens the edit dialog for this server row.
   * @returns Nothing.
   */
  const onEdit = (): void => openEditServer(server)
  /**
   * Stages this server for deletion.
   * @returns Nothing.
   */
  const onDelete = (): void => { deleteServerTarget.value = server }
  return [
    { label: t('shared.testConnection'), testid: `server-test-${server.id}`, disabled: testingIds.value.includes(server.id), onSelect: onTest },
    { label: t('servers.ssh.open'), testid: `server-ssh-${server.id}`, onSelect: onSsh },
    { label: t('shared.edit'), testid: `server-edit-${server.id}`, onSelect: onEdit },
    { label: t('shared.delete'), testid: `server-delete-${server.id}`, onSelect: onDelete, danger: true },
  ]
}

/**
 * Row actions for a server group.
 *
 * @param group - The group whose actions to build.
 * @returns The action list passed to UiRowActions.
 */
const serverGroupActions = (group: ServerGroupDto): UiMenuAction[] => {
  /**
   * Opens the edit dialog for this group row.
   * @returns Nothing.
   */
  const onEdit = (): void => openEditGroup(group)
  /**
   * Stages this group for deletion.
   * @returns Nothing.
   */
  const onDelete = (): void => { deleteGroupTarget.value = group }
  return [
    { label: t('shared.edit'), testid: `server-group-edit-${group.id}`, onSelect: onEdit },
    { label: t('shared.delete'), testid: `server-group-delete-${group.id}`, onSelect: onDelete, danger: true },
  ]
}

onMounted(loadAll)
</script>

<template>
  <UiStack gap="lg" class="w-full">
    <UiStatusMessage :message="feedback.message.value" :tone="feedbackTone" data-testid="servers-feedback" />

    <UiPageHeader
      :title="t('servers.servers.servers')"
      :subtitle="t('servers.servers.manageProvisioningServersAndLoad')"
    >
      <template #actions>
        <PageHelpLink :url="guideUrl" />
        <UiButton variant="secondary" data-testid="servers-add-group" @click="openAddGroup">
          {{ t('servers.servers.createGroup') }}
        </UiButton>
        <UiButton variant="primary" data-testid="servers-add" @click="openAddServer">
          {{ t('servers.servers.addNewServer') }}
        </UiButton>
      </template>
    </UiPageHeader>

    <UiErrorState
      v-if="store.error"
      :message="store.error"
      :loading="store.loading"
      testid="servers-error"
      @retry="loadAll"
    />

    <template v-else-if="store.loading || store.servers.length > 0">
      <!-- Search, filters and the chips of the applied ones -->
      <UiListToolbar
        v-model:search="query.search.value"
        search-testid="servers-search"
        :search-placeholder="t('servers.servers.searchPlaceholder')"
        :active-count="query.activeCount.value"
        :chips="filterChips"
        :loading="store.loading"
        @apply="query.apply"
        @reset="query.reset"
        @cancel="query.cancel"
        @remove="query.remove"
        @clear="query.clearAll"
      >
        <UiGrid :columns="1" gap="md">
          <UiField :label="t('servers.servers.filterModule')">
            <template #default="{ id }">
              <UiSelect
                :id="id"
                v-model="query.draft.value.module"
                :options="moduleOptions"
                :placeholder="t('servers.servers.anyModule')"
                data-testid="servers-filter-module"
              />
            </template>
          </UiField>
          <UiField :label="t('servers.servers.filterState')">
            <template #default="{ id }">
              <UiSelect
                :id="id"
                v-model="query.draft.value.state"
                :options="stateOptions"
                :placeholder="t('servers.servers.anyState')"
                data-testid="servers-filter-state"
              />
            </template>
          </UiField>
        </UiGrid>
      </UiListToolbar>

      <!-- Servers load in place: placeholder cards in the grid the cards will fill -->
      <UiGrid v-if="store.loading" min-item-width="md" gap="md" data-testid="servers-loading">
        <UiCard v-for="placeholder in LOADING_CARD_COUNT" :key="placeholder" padding="md" loading :skeleton-lines="3" />
      </UiGrid>

      <!-- Servers, every module in one grid -->
      <UiStack v-else-if="matchingServers.length > 0" gap="md" data-testid="servers-list">
        <!-- One grid for every module, so a module with a single server never leaves half the row empty -->
        <UiGrid min-item-width="md" gap="md">
          <UiCard v-for="server in pagedServers" :key="server.id" padding="md" :data-testid="`server-row-${server.id}`">
            <UiStack gap="md">
              <UiStack direction="row" justify="between" align="start" gap="sm">
                <UiStack gap="xs" class="min-w-0">
                  <UiText tone="muted" size="xs" class="font-semibold" :data-testid="`servers-module-${server.module}`">{{ MODULE_LABELS[server.module] }}</UiText>
                  <UiStack direction="row" align="center" gap="sm" wrap>
                    <UiText tone="primary" size="lg" class="min-w-0 break-words font-medium">{{ server.name }}</UiText>
                    <UiBadge v-if="server.limits.isDefault" tone="warning" small>{{ t('servers.servers.default') }}</UiBadge>
                    <UiBadge v-if="server.limits.isDisabled" tone="neutral" small>{{ t('servers.servers.disabled') }}</UiBadge>
                  </UiStack>
                  <UiText tone="muted" size="sm" class="font-mono">{{ server.endpoint.ipAddress ?? EMPTY_VALUE }}</UiText>
                </UiStack>
                <UiRowActions :actions="serverActions(server)" :testid="`server-actions-${server.id}`" />
              </UiStack>

              <UiProgress
                v-if="serverLoadPercent(server) !== null"
                :value="serverLoadPercent(server) ?? 0"
                :label="t('servers.servers.accountsLoad')"
                :value-text="accountsLabel(server)"
                :warn-at="LOAD_WARN_AT"
              />
              <UiStack v-else direction="row" justify="between" gap="sm">
                <UiText tone="secondary" size="sm">{{ t('shared.accounts') }}</UiText>
                <UiText tone="muted" size="sm">{{ accountsLabel(server) }}</UiText>
              </UiStack>

              <UiLoader v-if="testingIds.includes(server.id)" :data-testid="`server-testing-${server.id}`" />
              <UiBadge
                v-else-if="testResults[server.id]"
                :tone="testResults[server.id]?.ok ? 'success' : 'danger'"
                :data-testid="`server-test-result-${server.id}`"
              >
                {{ testResults[server.id]?.message }}
              </UiBadge>
              <UiStack v-else gap="xs">
                <UiBadge :tone="stateOf(server).tone" dot :data-testid="`server-state-${server.id}`">
                  {{ t(stateOf(server).key) }}
                </UiBadge>
                <UiText v-if="server.lastTestedAt" tone="muted" size="xs">
                  {{ new Date(server.lastTestedAt).toLocaleString(locale) }}
                </UiText>
              </UiStack>
            </UiStack>
          </UiCard>
        </UiGrid>
        <UiPagination
          :page="query.page.value"
          :total="matchingServers.length"
          :page-size="DEFAULT_PAGE_SIZE"
          testid="servers-pagination"
          @update:page="query.setPage"
        />
      </UiStack>

      <!-- Nothing matches the filters -->
      <UiEmptyState v-else :title="t('servers.servers.noMatches')" data-testid="servers-no-matches" />
    </template>

    <!-- Empty state -->
    <UiEmptyState v-else :title="t('servers.servers.noServersYet')" data-testid="servers-empty">
      <template #actions>
        <UiButton variant="link" data-testid="servers-add-first" @click="openAddServer">
          {{ t('servers.servers.addYourFirstServer') }}
        </UiButton>
      </template>
    </UiEmptyState>

    <!-- Groups section -->
    <UiStack gap="sm">
      <UiHeading :level="2" variant="section">{{ t('servers.servers.groups') }}</UiHeading>
      <UiText tone="secondary" size="sm">{{ t('servers.servers.serverGroupsAllowYouTo') }}</UiText>

      <UiCard v-if="store.loading || store.groups.length > 0">
        <UiTable :density="UiTableDensity.Comfortable" :loading="store.loading" data-testid="server-groups-table">
          <UiTableHead>
            <UiTableRow>
              <UiTableHeader card-role="title">{{ t('servers.servers.groupName') }}</UiTableHeader>
              <UiTableHeader>{{ t('servers.servers.fillType') }}</UiTableHeader>
              <UiTableHeader>{{ t('servers.servers.servers') }}</UiTableHeader>
              <UiTableHeader actions>{{ t('shared.actions') }}</UiTableHeader>
            </UiTableRow>
          </UiTableHead>
          <UiTableBody divided>
            <UiTableRow v-for="group in store.groups" :key="group.id" hoverable :data-testid="`server-group-row-${group.id}`">
              <UiTableCell>{{ group.name }}</UiTableCell>
              <UiTableCell muted>{{ FILL_TYPE_LABELS[group.fillType] }}</UiTableCell>
              <UiTableCell muted>
                {{ group.servers.length > 0 ? group.servers.map(s => s.name).join(', ') : EMPTY_VALUE }}
              </UiTableCell>
              <UiTableCell actions>
                <UiRowActions :actions="serverGroupActions(group)" :testid="`server-group-actions-${group.id}`" />
              </UiTableCell>
            </UiTableRow>
          </UiTableBody>
        </UiTable>
      </UiCard>

      <!-- Groups empty -->
      <UiStack v-else direction="row" align="center" gap="xs" data-testid="server-groups-empty">
        <UiText tone="muted" size="md">{{ t('servers.servers.noGroupsYet') }}</UiText>
        <UiButton variant="link" data-testid="server-groups-create-first" @click="openAddGroup">
          {{ t('servers.servers.createOne') }}
        </UiButton>
      </UiStack>
    </UiStack>

    <!-- Server form dialog -->
    <ServerSshSettingsModal
      v-if="sshServerTarget"
      :server-id="sshServerTarget.id"
      :server-name="sshServerTarget.name"
      @close="sshServerTarget = null"
    />

    <ServerFormModal
      v-if="showServerModal"
      :server="editingServer"
      :saving="savingServer"
      :error="store.actionError"
      :field-errors="store.fieldErrors"
      @saved="handleSaveServer"
      @close="showServerModal = false"
    />

    <!-- Group form dialog -->
    <GroupFormModal
      v-if="showGroupModal"
      :group="editingGroup"
      :available-servers="store.servers"
      :saving="savingGroup"
      :error="store.actionError"
      :field-errors="store.fieldErrors"
      @saved="handleSaveGroup"
      @close="showGroupModal = false"
    />

    <UiDialog
      v-if="deleteServerTarget"
      variant="confirm"
      :title="t('servers.servers.deleteServer')"
      :message="t('servers.servers.deleteServerMessage', { name: deleteServerTarget.name })"
      :confirm-label="t('shared.delete')"
      :loading="deletingServer"
      :tone="UiTone.Danger"
      @confirm="confirmDeleteServer"
      @close="dismissDeleteServer"
    />

    <UiDialog
      v-if="deleteGroupTarget"
      variant="confirm"
      :title="t('servers.servers.deleteGroup')"
      :message="t('servers.servers.deleteGroupMessage', { name: deleteGroupTarget.name })"
      :confirm-label="t('shared.delete')"
      :loading="deletingGroup"
      :tone="UiTone.Danger"
      @confirm="confirmDeleteGroup"
      @close="dismissDeleteGroup"
    />
  </UiStack>
</template>
