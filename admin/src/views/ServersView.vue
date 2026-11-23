<script setup lang="ts">
/**
 * Main Servers management page.
 *
 * Lists one page of the provisioning servers as cards, with a search box, module and state filters (kept in the
 * address with the page, shown as chips) that the API applies before it pages, then shows server groups.
 * Provides Add/Edit/Delete actions for both servers and groups. Every refusal sentence belongs to
 * the servers store and is shown as the API returned it: a refused load with a retry, a refused save
 * inside its dialog, a delete (confirmed first in a danger prompt) or a saved dialog in the outcome
 * line under the header. The SSH key management dialog confirms its own saves: it stays open, since
 * its later steps use what was saved.
 *
 * The placeholder cards of the grid, the card of the groups and the head of its table are the ones of `ServersView.schema`,
 * which `UiSkeletonOf` also draws while the view downloads.
 */
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useServersStore } from '@/stores/servers'
import { FILL_TYPE_LABELS, ServerState, moduleNameOf, moduleOptionLabel } from '@/types/server'
import type { ServerDto, ServerGroupDto, ServerGroupPayload, ServerListFilters, ServerPayload, ServerTestOutcome } from '@/types/server'
import { UiTone } from '@/utils/ui/tone'
import ServerCard from '@/components/servers/ServerCard.vue'
import GroupFormModal from '@/components/servers/GroupFormModal.vue'
import ServerFormModal from '@/components/servers/ServerFormModal.vue'
import ServerSshSettingsModal from '@/components/servers/ServerSshSettingsModal.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiDetailCards from '@/components/ui/UiDetailCards.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiListToolbar from '@/components/ui/UiListToolbar.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiPagination from '@/components/ui/UiPagination.vue'
import UiSchemaTableHead from '@/components/ui/UiSchemaTableHead.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiDataTable from '@/components/ui/UiDataTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiRowActions from '@/components/ui/UiRowActions.vue'
import type { UiMenuAction } from '@/utils/ui/menu'
import { UiTableDensity } from '@/utils/ui/injection'
import type { UiSelectOption } from '@/utils/ui/select'
import { SERVER_STATE_KEYS } from '@/lib/servers'
import { useListQuery } from '@/composables/useListQuery'
import UiText from '@/components/ui/UiText.vue'
import { useOutcomeToast } from '@/composables/useOutcomeToast'
import { useDocsGuide } from '@/composables/useDocsGuide'
import PageHelpLink from '@/components/layout/PageHelpLink.vue'
import { EMPTY_VALUE } from '@/lib/format'
import { Icon } from '@/lib/icons'
import { schemaHeader } from '@/lib/schemaHeader'
import schema, { SERVER_CARDS_PART, SERVER_GROUPS_PART, SERVER_GROUPS_TABLE } from '@/views/ServersView.schema'
import { UiActionKind, actionIcons } from '@/utils/ui/actions'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiDialogVariant } from '@/utils/ui/dialog'
import { UiAlignItems, UiDirection, UiGap, UiGridMinWidth } from '@/utils/ui/layout'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import type { UiHeaderAction } from '@/utils/ui/actionBar'

/** Translation function. */
const { t } = useI18n()

/** The header actions, each with its icon; on a phone only the primary one stays a button. */
const headerActions = computed<UiHeaderAction[]>(() => [
  { label: t('servers.servers.createGroup'), icon: actionIcons[UiActionKind.Add], variant: UiButtonVariant.Secondary, testid: 'servers-add-group', onSelect: openAddGroup },
  { label: t('servers.servers.createServer'), icon: actionIcons[UiActionKind.Add], variant: UiButtonVariant.Primary, testid: 'servers-add', onSelect: openAddServer },
])

/** Servers store: the lists, the refusal sentences and the actions. */
const store = useServersStore()

/** True while a server save is in flight. */
const savingServer = ref(false)

/** True while a group save is in flight. */
const savingGroup = ref(false)

/** Ids of the servers currently being tested. */
const testingIds = ref<number[]>([])

/** Last test result per server id. */
const testResults = ref<Record<number, ServerTestOutcome>>({})

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
const feedback = useOutcomeToast()
/** Link to the docs guide for this screen, or null when docsUrl is not configured. */
const { guideUrl } = useDocsGuide()

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

/** The filter fields the dialog edits; each is one query key and one `ServerListFilters` member. */
const FilterKey = {
  Module: 'module',
  State: 'state',
} as const

/** The module and state filters, the search text and the page of the list, kept in the address. */
const { draft, search, appliedSearch, page, activeCount, apply, reset, cancel, remove, clearAll, setPage, narrowed, loadOnChange, stepBackWhenEmpty, enumOf, textOf, chipsOf }
  = useListQuery(Object.values(FilterKey))

/** The module text in the address, undefined when none is set; it may name a module that is not installed. */
const moduleTextOfAddress = textOf(FilterKey.Module)

/** The module in the address, undefined when none is set or the loaded module list does not know it. */
const moduleOfAddress = computed<string | undefined>(() =>
  store.moduleOptions.some(option => option.id === moduleTextOfAddress.value) ? moduleTextOfAddress.value : undefined,
)

/** The state in the address as a state, undefined when none is set or the value is not one. */
const stateOfAddress = enumOf(FilterKey.State, ServerState)

/** What the address narrows the list by, as the store sends it to the API. */
const filters = computed<ServerListFilters>(() => ({
  search: appliedSearch.value,
  module: moduleOfAddress.value,
  state: stateOfAddress.value,
}))

/**
 * Loads the page the address names with the filters and search it holds. The module list is read first when it is
 * not yet, since the module in the address is only sent when that list knows it.
 *
 * @returns True when loaded; false when refused (see the store's `error`).
 */
const loadServers = async (): Promise<boolean> => {
  if (store.moduleOptions.length === 0) {
    await store.fetchModuleOptions()
  }
  return store.fetchServers(page.value, filters.value)
}

/**
 * Loads the page the address names and the groups, which show the names of their servers.
 *
 * @returns Resolves when both loads have settled.
 */
const loadAll = async (): Promise<void> => {
  await Promise.all([loadServers(), store.fetchGroups()])
}

// Every change of a filter, the search or the page loads the list the address names.
loadOnChange(loadServers)

/** True while the staged server is being deleted. */
const deletingServer = ref(false)

/** The group awaiting delete confirmation, or null when nothing is staged. */
const deleteGroupTarget = ref<ServerGroupDto | null>(null)

/** True while the staged group is being deleted. */
const deletingGroup = ref(false)

/** The module choices of the filter, "any" first: the host's own modules and the ones installed plugins declare. */
const moduleOptions = computed<UiSelectOption<string>[]>(() => [
  { value: '', label: t('shared.any') },
  ...store.moduleOptions.map(option => ({
    value: option.id,
    label: moduleOptionLabel(option),
  })),
])

/** The state choices of the filter, "any" first. */
const stateOptions = computed<UiSelectOption<string>[]>(() => [
  { value: '', label: t('shared.any') },
  ...Object.values(ServerState).map(state => ({ value: state, label: t(SERVER_STATE_KEYS[state]) })),
])

/** One chip per applied filter, with the value as the operator reads it. */
const chips = chipsOf(() => [
  {
    key: FilterKey.Module,
    label: t('servers.servers.filterModule'),
    value: moduleOfAddress.value === undefined ? undefined : moduleNameOf(moduleOfAddress.value, store.moduleOptions),
  },
  {
    key: FilterKey.State,
    label: t('servers.servers.filterState'),
    value: stateOfAddress.value === undefined ? undefined : t(SERVER_STATE_KEYS[stateOfAddress.value]),
  },
])

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
  if (result !== null) {
    // The test recorded the server's state and account count, so the page is read again to show them.
    await loadServers()
  }
  let outcome: ServerTestOutcome
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

/** Opens the group create dialog and reads every server, which the dialog offers for the group. */
const openAddGroup = (): void => {
  store.actionError = null
  void store.fetchServerOptions()
  editingGroup.value = null
  showGroupModal.value = true
}

/**
 * Opens the group edit dialog and reads every server, which the dialog offers for the group.
 *
 * @param group - The group to edit.
 */
const openEditGroup = (group: ServerGroupDto): void => {
  store.actionError = null
  void store.fetchServerOptions()
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
    await loadAll()
  }
}

/** Deletes the staged server and clears the prompt. */
const confirmDeleteServer = async (): Promise<void> => {
  const target = deleteServerTarget.value
  if (!target) {
    return
  }
  deletingServer.value = true
  const deleted = await store.deleteServer(target.id)
  deletingServer.value = false
  deleteServerTarget.value = null
  reportDelete(deleted)
  if (deleted) {
    await loadAll()
    await stepBackWhenEmpty(store.servers.length === 0)
  }
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
    await loadAll()
  }
}

/** Deletes the staged group and clears the prompt. */
const confirmDeleteGroup = async (): Promise<void> => {
  const target = deleteGroupTarget.value
  if (!target) {
    return
  }
  deletingGroup.value = true
  const deleted = await store.deleteGroup(target.id)
  deletingGroup.value = false
  deleteGroupTarget.value = null
  reportDelete(deleted)
  if (deleted) {
    await loadAll()
  }
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
    { label: t('shared.testConnection'), icon: Icon.Zap, testid: `server-test-${server.id}`, disabled: testingIds.value.includes(server.id), onSelect: onTest },
    { label: t('servers.ssh.open'), icon: Icon.Terminal, testid: `server-ssh-${server.id}`, onSelect: onSsh },
    { label: t('shared.edit'), icon: Icon.Edit, testid: `server-edit-${server.id}`, onSelect: onEdit },
    { label: t('shared.delete'), icon: Icon.Trash, testid: `server-delete-${server.id}`, onSelect: onDelete, danger: true },
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
    { label: t('shared.edit'), icon: Icon.Edit, testid: `server-group-edit-${group.id}`, onSelect: onEdit },
    { label: t('shared.delete'), icon: Icon.Trash, testid: `server-group-delete-${group.id}`, onSelect: onDelete, danger: true },
  ]
}

// The page of servers, with the module choices it needs, loads with the address (loadOnChange); the groups are read once.
onMounted(() => {
  void store.fetchGroups()
})
</script>

<template>
  <UiStack :gap="UiGap.Lg">

    <UiPageHeader :actions="headerActions" v-bind="schemaHeader(schema, t)" >
      <template #actions>
        <PageHelpLink :url="guideUrl" />
        
        
      </template>
    </UiPageHeader>

    <UiErrorState
      v-if="store.error"
      :message="store.error"
      :loading="store.loading"
      testid="servers-error"
      @retry="loadAll"
    />

    <template v-else-if="store.loading || store.servers.length > 0 || narrowed">
      <!-- Search, filters and the chips of the applied ones -->
      <UiListToolbar
        v-model:search="search"
        search-testid="servers-search"
        :search-placeholder="t('servers.servers.searchPlaceholder')"
        :active-count="activeCount"
        :chips="chips"
        :loading="store.loading"
        @apply="apply"
        @reset="reset"
        @cancel="cancel"
        @remove="remove"
        @clear="clearAll"
      >
        <UiGrid :columns="1" :gap="UiGap.Md">
          <UiField :label="t('servers.servers.filterModule')">
            <template #default="{ id }">
              <UiSelect
                :id="id"
                v-model="draft.module"
                :options="moduleOptions"
                :placeholder="t('shared.any')"
                data-testid="servers-filter-module"
              />
            </template>
          </UiField>
          <UiField :label="t('servers.servers.filterState')">
            <template #default="{ id }">
              <UiSelect
                :id="id"
                v-model="draft.state"
                :options="stateOptions"
                :placeholder="t('shared.any')"
                data-testid="servers-filter-state"
              />
            </template>
          </UiField>
        </UiGrid>
      </UiListToolbar>

      <!-- Servers, every module in one grid; while the list loads the same grid holds placeholder cards -->
      <UiStack v-if="store.loading || store.servers.length > 0" :gap="UiGap.Md" data-testid="servers-list">
        <!-- One grid for every module, so a module with a single server never leaves half the row empty -->
        <UiGrid :min-item-width="UiGridMinWidth.Md" :gap="UiGap.Md">
          <template v-if="store.loading">
            <ServerCard v-for="placeholder in SERVER_CARDS_PART.count" :key="placeholder" />
          </template>
          <template v-else>
            <ServerCard
              v-for="server in store.servers"
              :key="server.id"
              :server="server"
              :module-options="store.moduleOptions"
              :actions="serverActions(server)"
              :testing="testingIds.includes(server.id)"
              :test-outcome="testResults[server.id] ?? null"
            />
          </template>
        </UiGrid>
        <UiPagination
          :page="page"
          :total="store.totalCount"
          :page-size="store.pageSize"
          :loading="store.loading"
          testid="servers-pagination"
          @update:page="setPage"
        />
      </UiStack>

      <!-- Nothing matches the filters -->
      <UiEmptyState v-else :title="t('servers.servers.noMatches')" data-testid="servers-no-matches" />
    </template>

    <!-- Empty state -->
    <UiEmptyState v-else :title="t('servers.servers.noServersYet')" data-testid="servers-empty">
      <template #actions>
        <UiButton :variant="UiButtonVariant.Link" data-testid="servers-add-first" @click="openAddServer">
          {{ t('servers.servers.createServer') }}
        </UiButton>
      </template>
    </UiEmptyState>

    <!-- The card of the groups: its placeholder while the lists load, the note and the table or the empty line after -->
    <UiDetailCards :columns="SERVER_GROUPS_PART.columns" :loading="store.loading">
      <template #card-groups>
        <UiText :tone="UiTextTone.Secondary" :size="UiTextSize.Sm">{{ t('servers.servers.serverGroupsAllowYouTo') }}</UiText>

        <UiDataTable v-if="store.groups.length > 0" :density="UiTableDensity.Comfortable" data-testid="server-groups-table">
          <UiSchemaTableHead :columns="SERVER_GROUPS_TABLE.columns" />
          <UiTableBody>
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
        </UiDataTable>

        <!-- Groups empty -->
        <UiStack v-else :direction="UiDirection.Row" :align="UiAlignItems.Center" :gap="UiGap.Xs" data-testid="server-groups-empty">
          <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Md">{{ t('servers.servers.noGroupsYet') }}</UiText>
          <UiButton :variant="UiButtonVariant.Link" data-testid="server-groups-create-first" @click="openAddGroup">
            {{ t('servers.servers.createGroup') }}
          </UiButton>
        </UiStack>
      </template>
    </UiDetailCards>

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
      :module-options="store.moduleOptions"
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
      :available-servers="store.serverOptions"
      :saving="savingGroup"
      :error="store.actionError"
      :field-errors="store.fieldErrors"
      @saved="handleSaveGroup"
      @close="showGroupModal = false"
    />

    <UiDialog
      v-if="deleteServerTarget"
      :variant="UiDialogVariant.Confirm"
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
      :variant="UiDialogVariant.Confirm"
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
