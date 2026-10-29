<script setup lang="ts">
/**
 * Main Servers management page.
 *
 * Lists all provisioning servers grouped by module, then shows server groups.
 * Provides Add/Edit/Delete actions for both servers and groups. Every refusal sentence belongs to
 * the servers store and is shown as the API returned it: a refused load with a retry, a refused save
 * inside its dialog, a delete (confirmed first in a danger prompt) or a saved dialog in the outcome
 * line under the header. The SSH key management dialog confirms its own saves: it stays open, since
 * its later steps use what was saved.
 */
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useServersStore } from '@/stores/servers'
import { FILL_TYPE_LABELS, MODULE_LABELS } from '@/types/server'
import type { ServerDto, ServerGroupDto, ServerGroupPayload, ServerModule, ServerPayload } from '@/types/server'
import { UiTone } from '@/utils/ui/tone'
import GroupFormModal from '@/components/servers/GroupFormModal.vue'
import ServerFormModal from '@/components/servers/ServerFormModal.vue'
import ServerSshSettingsModal from '@/components/servers/ServerSshSettingsModal.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiSkeleton from '@/components/ui/UiSkeleton.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiRowActions from '@/components/ui/UiRowActions.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import UiText from '@/components/ui/UiText.vue'
import { useResultBanner } from '@/composables/useResultBanner'

/** Translation function and the active locale (for date formatting). */
const { t, locale } = useI18n()

/** Servers store: the lists, the refusal sentences and the actions. */
const store = useServersStore()

/** Result of one connection test, as shown in the status column. */
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

/** Servers grouped by module, in first-seen order, for display. */
const serversByModule = computed<{ module: ServerModule; servers: ServerDto[] }[]>(() => {
  const groups: { module: ServerModule; servers: ServerDto[] }[] = []
  for (const server of store.servers) {
    const existing = groups.find(g => g.module === server.module)
    if (existing) {
      existing.servers.push(server)
    } else {
      groups.push({ module: server.module, servers: [server] })
    }
  }
  return groups
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
  if (server.isOnline === true) {
    return { tone: 'success', key: 'servers.servers.online' }
  }
  if (server.isOnline === false) {
    return { tone: 'danger', key: 'servers.servers.offline' }
  }
  return { tone: 'neutral', key: 'servers.servers.notTested' }
}

/**
 * Formats the account usage of a server.
 *
 * @param server - The server.
 * @returns `count / max`, `count`, or a dash when unknown.
 */
const accountsLabel = (server: ServerDto): string => {
  if (server.accountsCount === null) {
    return '—'
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

    <!-- Loading -->
    <UiSkeleton v-else-if="store.loading && store.servers.length === 0" :lines="4" data-testid="servers-loading" />

    <!-- Servers, one table per module -->
    <UiStack v-else-if="store.servers.length > 0" gap="md" data-testid="servers-list">
      <UiStack v-for="entry in serversByModule" :key="entry.module" gap="xs">
        <UiText tone="muted" size="sm" class="font-semibold">{{ MODULE_LABELS[entry.module] }}</UiText>
        <UiTable density="comfortable" :data-testid="`servers-table-${entry.module}`">
          <UiTableHead>
            <UiTableRow>
              <UiTableHeader>{{ t('servers.servers.serverName') }}</UiTableHeader>
              <UiTableHeader>{{ t('shared.ipAddress') }}</UiTableHeader>
              <UiTableHeader>{{ t('shared.accounts') }}</UiTableHeader>
              <UiTableHeader>{{ t('shared.status') }}</UiTableHeader>
              <UiTableHeader>{{ t('shared.actions') }}</UiTableHeader>
            </UiTableRow>
          </UiTableHead>
          <UiTableBody divided>
            <UiTableRow v-for="server in entry.servers" :key="server.id" hoverable :data-testid="`server-row-${server.id}`">
              <UiTableCell>
                <UiStack direction="row" align="center" gap="sm">
                  <UiText tone="primary" size="lg" class="font-medium">{{ server.name }}</UiText>
                  <UiBadge v-if="server.limits.isDefault" tone="warning" small>{{ t('servers.servers.default') }}</UiBadge>
                </UiStack>
              </UiTableCell>
              <UiTableCell muted class="font-mono">{{ server.endpoint.ipAddress ?? '—' }}</UiTableCell>
              <UiTableCell muted>{{ accountsLabel(server) }}</UiTableCell>
              <UiTableCell>
                <UiText v-if="testingIds.includes(server.id)" tone="muted" size="sm">{{ t('shared.testing') }}</UiText>
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
              </UiTableCell>
              <UiTableCell align="right">
                <UiRowActions
                  :actions="[
                    { label: t('shared.testConnection'), testid: `server-test-${server.id}`, onSelect: () => handleTestConnection(server.id) },
                    { label: t('servers.ssh.open'), testid: `server-ssh-${server.id}`, onSelect: () => { sshServerTarget = server } },
                    { label: t('shared.edit'), testid: `server-edit-${server.id}`, onSelect: () => openEditServer(server) },
                    { label: t('shared.delete'), testid: `server-delete-${server.id}`, onSelect: () => { deleteServerTarget = server }, danger: true },
                  ]"
                /></UiTableCell>
            </UiTableRow>
          </UiTableBody>
        </UiTable>
      </UiStack>
    </UiStack>

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

      <UiTable v-if="store.groups.length > 0" density="comfortable" data-testid="server-groups-table">
        <UiTableHead>
          <UiTableRow>
            <UiTableHeader>{{ t('servers.servers.groupName') }}</UiTableHeader>
            <UiTableHeader>{{ t('servers.servers.fillType') }}</UiTableHeader>
            <UiTableHeader>{{ t('servers.servers.servers') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.actions') }}</UiTableHeader>
          </UiTableRow>
        </UiTableHead>
        <UiTableBody divided>
          <UiTableRow v-for="group in store.groups" :key="group.id" hoverable :data-testid="`server-group-row-${group.id}`">
            <UiTableCell>{{ group.name }}</UiTableCell>
            <UiTableCell muted>{{ FILL_TYPE_LABELS[group.fillType] }}</UiTableCell>
            <UiTableCell muted>
              {{ group.servers.length > 0 ? group.servers.map(s => s.name).join(', ') : '—' }}
            </UiTableCell>
            <UiTableCell align="right">
              <UiRowActions
                :actions="[
                  { label: t('shared.edit'), testid: `server-group-edit-${group.id}`, onSelect: () => openEditGroup(group) },
                  { label: t('shared.delete'), testid: `server-group-delete-${group.id}`, onSelect: () => { deleteGroupTarget = group }, danger: true },
                ]"
              />
            </UiTableCell>
          </UiTableRow>
        </UiTableBody>
      </UiTable>

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
      :loading-label="t('shared.deleting')"
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
      :loading-label="t('shared.deleting')"
      :loading="deletingGroup"
      :tone="UiTone.Danger"
      @confirm="confirmDeleteGroup"
      @close="dismissDeleteGroup"
    />
  </UiStack>
</template>
