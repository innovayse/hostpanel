<script setup lang="ts">
/**
 * Main Servers management page.
 *
 * Lists all provisioning servers grouped by module, then shows server groups.
 * Provides Add/Edit/Delete actions for both servers and groups.
 */
import { computed, onMounted, ref } from 'vue'
import { useServersStore } from '../stores/serversStore'
import ServerFormModal from '../components/ServerFormModal.vue'
import GroupFormModal from '../components/GroupFormModal.vue'
import type { ServerDto, ServerGroupDto, ServerPayload, ServerGroupPayload } from '../types/server.types'
import { MODULE_LABELS, FILL_TYPE_LABELS } from '../types/server.types'

const store = useServersStore()

/** True while a server save is in flight. */
const savingServer = ref(false)
/** True while a group save is in flight. */
const savingGroup = ref(false)

/** Set of server IDs currently being tested. */
const testingIds = ref<Set<number>>(new Set())

/** Last test result message per server ID. */
const testResults = ref<Map<number, { ok: boolean; msg: string }>>(new Map())

/**
 * Tests connectivity to a server and shows the result briefly.
 *
 * @param id - Server identifier.
 */
async function handleTestConnection(id: number): Promise<void> {
  testingIds.value = new Set([...testingIds.value, id])
  testResults.value.delete(id)
  try {
    const result = await store.testConnection(id)
    testResults.value = new Map(testResults.value).set(id, {
      ok: result.connected,
      msg: result.connected
        ? `Online · ${result.accountsCount ?? 0} accounts · ${result.version ?? ''}`
        : (result.errorMessage ?? 'Connection failed'),
    })
  } catch {
    testResults.value = new Map(testResults.value).set(id, { ok: false, msg: 'Request failed' })
  } finally {
    testingIds.value = new Set([...testingIds.value].filter(i => i !== id))
  }
}

/** The server being edited, or null when creating. */
const editingServer = ref<ServerDto | null>(null)
/** True when the server form modal is open. */
const showServerModal = ref(false)

/** The group being edited, or null when creating. */
const editingGroup = ref<ServerGroupDto | null>(null)
/** True when the group form modal is open. */
const showGroupModal = ref(false)

/** Groups servers by module type for display. */
const serversByModule = computed(() => {
  const map = new Map<string, ServerDto[]>()
  for (const s of store.servers) {
    if (!map.has(s.module)) map.set(s.module, [])
    map.get(s.module)!.push(s)
  }
  return map
})

/** Ungrouped servers (no group assigned). */
const ungroupedServers = computed(() =>
  store.servers.filter(s => s.serverGroupId === null)
)

/** Opens the server create modal. */
function openAddServer(): void {
  editingServer.value = null
  showServerModal.value = true
}

/** Opens the server edit modal. */
function openEditServer(server: ServerDto): void {
  editingServer.value = server
  showServerModal.value = true
}

/** Opens the group create modal. */
function openAddGroup(): void {
  editingGroup.value = null
  showGroupModal.value = true
}

/** Opens the group edit modal. */
function openEditGroup(group: ServerGroupDto): void {
  editingGroup.value = group
  showGroupModal.value = true
}

/**
 * Saves a server (create or update).
 *
 * @param payload - Form data from ServerFormModal.
 */
async function handleSaveServer(payload: ServerPayload): Promise<void> {
  savingServer.value = true
  try {
    if (editingServer.value)
      await store.updateServer(editingServer.value.id, payload)
    else
      await store.createServer(payload)
    showServerModal.value = false
  } finally {
    savingServer.value = false
  }
}

/**
 * Deletes a server after confirmation.
 *
 * @param server - Server to delete.
 */
async function handleDeleteServer(server: ServerDto): Promise<void> {
  if (!confirm(`Delete "${server.name}"? This cannot be undone.`)) return
  await store.deleteServer(server.id)
}

/**
 * Saves a group (create or update).
 *
 * @param payload - Form data from GroupFormModal.
 */
async function handleSaveGroup(payload: ServerGroupPayload): Promise<void> {
  savingGroup.value = true
  try {
    if (editingGroup.value)
      await store.updateGroup(editingGroup.value.id, payload)
    else
      await store.createGroup(payload)
    showGroupModal.value = false
  } finally {
    savingGroup.value = false
  }
}

/**
 * Deletes a group after confirmation.
 *
 * @param group - Group to delete.
 */
async function handleDeleteGroup(group: ServerGroupDto): Promise<void> {
  if (!confirm(`Delete group "${group.name}"? Servers will be unassigned.`)) return
  await store.deleteGroup(group.id)
}

onMounted(async () => {
  await Promise.all([store.fetchServers(), store.fetchGroups()])
})
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 max-w-6xl w-full flex flex-col gap-6">

    <!-- Page header -->
    <div class="flex flex-wrap items-center justify-between gap-3">
      <div>
        <h1 class="font-display font-bold text-[1.25rem] text-text-primary leading-none">Servers</h1>
        <p class="text-[0.78rem] text-text-secondary mt-1">
          Manage provisioning servers and load-balancing groups.
        </p>
      </div>
      <div class="flex items-center gap-2">
        <button
          class="flex items-center gap-1.5 px-3.5 py-2 text-[0.82rem] font-medium text-text-secondary bg-white/[0.05] border border-border rounded-[10px] hover:text-text-primary hover:border-border/80 transition-colors"
          @click="openAddGroup"
        >
          <svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <rect x="3" y="3" width="7" height="7"/><rect x="14" y="3" width="7" height="7"/>
            <rect x="14" y="14" width="7" height="7"/><rect x="3" y="14" width="7" height="7"/>
          </svg>
          Create Group
        </button>
        <button
          class="gradient-brand flex items-center gap-1.5 px-3.5 py-2 text-[0.82rem] font-semibold text-white rounded-[10px] transition-opacity hover:opacity-90"
          @click="openAddServer"
        >
          <svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5">
            <line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/>
          </svg>
          Add New Server
        </button>
      </div>
    </div>

    <!-- Loading -->
    <div v-if="store.loading && store.servers.length === 0" class="flex justify-center py-16">
      <div class="w-6 h-6 border-2 border-primary-500/30 border-t-primary-500 rounded-full animate-spin" />
    </div>

    <!-- Servers table section -->
    <div v-else-if="store.servers.length > 0" class="bg-surface-card border border-border rounded-2xl overflow-hidden">

      <!-- Table header -->
      <div class="hidden sm:grid grid-cols-[2fr_1.5fr_1fr_1.5fr_auto] gap-4 px-5 py-3 border-b border-border bg-white/[0.02]">
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Server Name</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">IP Address</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Accounts</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Status</span>
        <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Actions</span>
      </div>

      <!-- Module groups -->
      <template v-for="[mod, modServers] in serversByModule" :key="mod">
        <!-- Module label row -->
        <div class="px-5 py-2 bg-white/[0.015] border-b border-border">
          <span class="text-[0.72rem] font-semibold text-text-muted">{{ MODULE_LABELS[mod as keyof typeof MODULE_LABELS] ?? mod }}</span>
        </div>

        <!-- Server rows -->
        <div
          v-for="server in modServers"
          :key="server.id"
          class="border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors"
        >
          <!-- Desktop row -->
          <div class="hidden sm:grid grid-cols-[2fr_1.5fr_1fr_1.5fr_auto] gap-4 px-5 py-3.5 items-center">
            <div class="flex items-center gap-2 min-w-0">
              <span class="w-1.5 h-1.5 rounded-full shrink-0"
                :class="server.isOnline === true ? 'bg-status-green' : server.isOnline === false ? 'bg-status-red' : 'bg-text-muted'" />
              <span class="text-[0.875rem] font-medium text-primary-400 truncate">{{ server.name }}</span>
              <span v-if="server.isDefault" class="shrink-0 text-[0.62rem] font-bold text-status-yellow border border-status-yellow/30 rounded px-1 py-0.5">DEFAULT</span>
            </div>
            <span class="text-[0.82rem] text-text-secondary font-mono">{{ server.ipAddress ?? '—' }}</span>
            <span class="text-[0.82rem] text-text-secondary">
              {{ server.accountsCount != null ? `${server.accountsCount}${server.maxAccounts != null ? ` / ${server.maxAccounts}` : ''}` : '—' }}
            </span>
            <!-- Status column -->
            <div class="flex flex-col gap-0.5 min-w-0">
              <div v-if="testingIds.has(server.id)" class="flex items-center gap-1.5">
                <span class="w-3 h-3 border border-text-muted border-t-transparent rounded-full animate-spin shrink-0" />
                <span class="text-[0.75rem] text-text-muted">Testing…</span>
              </div>
              <template v-else-if="testResults.has(server.id)">
                <div class="flex items-center gap-1.5">
                  <span class="w-2 h-2 rounded-full shrink-0" :class="testResults.get(server.id)!.ok ? 'bg-status-green' : 'bg-status-red'" />
                  <span class="text-[0.75rem] truncate" :class="testResults.get(server.id)!.ok ? 'text-status-green' : 'text-status-red'">
                    {{ testResults.get(server.id)!.msg }}
                  </span>
                </div>
              </template>
              <template v-else>
                <div class="flex items-center gap-1.5">
                  <span class="w-2 h-2 rounded-full shrink-0"
                    :class="server.isOnline === true ? 'bg-status-green' : server.isOnline === false ? 'bg-status-red' : 'bg-text-muted/40'" />
                  <span class="text-[0.75rem]"
                    :class="server.isOnline === true ? 'text-status-green' : server.isOnline === false ? 'text-status-red' : 'text-text-muted'">
                    {{ server.isOnline === true ? 'Online' : server.isOnline === false ? 'Offline' : 'Not tested' }}
                  </span>
                </div>
                <span v-if="server.lastTestedAt" class="text-[0.65rem] text-text-muted">
                  {{ new Date(server.lastTestedAt).toLocaleString() }}
                </span>
              </template>
            </div>
            <div class="flex items-center gap-1">
              <!-- Test connection -->
              <button
                class="w-7 h-7 flex items-center justify-center rounded-lg text-text-muted hover:text-primary-400 hover:bg-primary-500/[0.08] transition-colors"
                title="Test Connection"
                :disabled="testingIds.has(server.id)"
                @click="handleTestConnection(server.id)"
              >
                <svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <path d="M8.56 2.9A7 7 0 1 1 2.9 8.56"/><polyline points="2 2 2 8 8 8"/>
                </svg>
              </button>
              <button class="w-7 h-7 flex items-center justify-center rounded-lg text-text-muted hover:text-text-primary hover:bg-white/[0.06] transition-colors" title="Edit" @click="openEditServer(server)">
                <svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/><path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/></svg>
              </button>
              <button class="w-7 h-7 flex items-center justify-center rounded-lg text-text-muted hover:text-status-red hover:bg-status-red/[0.08] transition-colors" title="Delete" @click="handleDeleteServer(server)">
                <svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="3 6 5 6 21 6"/><path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6"/><path d="M10 11v6"/><path d="M14 11v6"/><path d="M9 6V4a1 1 0 0 1 1-1h4a1 1 0 0 1 1 1v2"/></svg>
              </button>
            </div>
          </div>

          <!-- Mobile card -->
          <div class="sm:hidden px-4 py-3 flex flex-col gap-2">
            <div class="flex items-center justify-between gap-2">
              <div class="flex items-center gap-2 min-w-0">
                <span class="w-1.5 h-1.5 rounded-full shrink-0"
                  :class="server.isOnline === true ? 'bg-status-green' : server.isOnline === false ? 'bg-status-red' : 'bg-text-muted'" />
                <span class="text-[0.875rem] font-medium text-primary-400 truncate">{{ server.name }}</span>
                <span v-if="server.isDefault" class="shrink-0 text-[0.62rem] font-bold text-status-yellow border border-status-yellow/30 rounded px-1 py-0.5">DEFAULT</span>
              </div>
              <div class="flex items-center gap-1 shrink-0">
                <button class="w-7 h-7 flex items-center justify-center rounded-lg text-text-muted hover:text-primary-400 hover:bg-primary-500/[0.08] transition-colors" :disabled="testingIds.has(server.id)" @click="handleTestConnection(server.id)">
                  <svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M8.56 2.9A7 7 0 1 1 2.9 8.56"/><polyline points="2 2 2 8 8 8"/></svg>
                </button>
                <button class="w-7 h-7 flex items-center justify-center rounded-lg text-text-muted hover:text-text-primary hover:bg-white/[0.06] transition-colors" @click="openEditServer(server)">
                  <svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/><path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/></svg>
                </button>
                <button class="w-7 h-7 flex items-center justify-center rounded-lg text-text-muted hover:text-status-red hover:bg-status-red/[0.08] transition-colors" @click="handleDeleteServer(server)">
                  <svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="3 6 5 6 21 6"/><path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6"/><path d="M10 11v6"/><path d="M14 11v6"/><path d="M9 6V4a1 1 0 0 1 1-1h4a1 1 0 0 1 1 1v2"/></svg>
                </button>
              </div>
            </div>
            <div class="flex items-center gap-4 flex-wrap">
              <span class="text-[0.75rem] text-text-muted">IP: <span class="font-mono text-text-secondary">{{ server.ipAddress ?? '—' }}</span></span>
              <span class="text-[0.75rem] text-text-muted">Accounts: <span class="text-text-secondary">
                {{ server.accountsCount != null ? `${server.accountsCount}${server.maxAccounts != null ? ` / ${server.maxAccounts}` : ''}` : '—' }}
              </span></span>
              <div v-if="testingIds.has(server.id)" class="flex items-center gap-1">
                <span class="w-3 h-3 border border-text-muted border-t-transparent rounded-full animate-spin" />
                <span class="text-[0.75rem] text-text-muted">Testing…</span>
              </div>
              <div v-else class="flex items-center gap-1">
                <span class="w-1.5 h-1.5 rounded-full"
                  :class="server.isOnline === true ? 'bg-status-green' : server.isOnline === false ? 'bg-status-red' : 'bg-text-muted/40'" />
                <span class="text-[0.75rem]"
                  :class="server.isOnline === true ? 'text-status-green' : server.isOnline === false ? 'text-status-red' : 'text-text-muted'">
                  {{ server.isOnline === true ? 'Online' : server.isOnline === false ? 'Offline' : 'Not tested' }}
                </span>
              </div>
            </div>
          </div>
        </div>
      </template>

    </div>

    <!-- Empty state -->
    <div
      v-else
      class="bg-surface-card border border-border rounded-2xl flex flex-col items-center justify-center py-16 gap-3"
    >
      <div class="w-12 h-12 rounded-2xl bg-white/[0.04] border border-border flex items-center justify-center">
        <svg class="w-6 h-6 text-text-muted" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
          <rect x="2" y="2" width="20" height="8" rx="2" ry="2"/>
          <rect x="2" y="14" width="20" height="8" rx="2" ry="2"/>
          <line x1="6" y1="6" x2="6.01" y2="6"/><line x1="6" y1="18" x2="6.01" y2="18"/>
        </svg>
      </div>
      <p class="text-[0.875rem] font-medium text-text-secondary">No servers yet</p>
      <button class="text-[0.82rem] text-primary-400 hover:text-primary-300 transition-colors" @click="openAddServer">
        Add your first server →
      </button>
    </div>

    <!-- Groups section -->
    <div>
      <div class="flex items-center justify-between mb-3">
        <h2 class="font-display font-semibold text-[0.95rem] text-text-primary">Groups</h2>
      </div>
      <p class="text-[0.75rem] text-text-secondary mb-4">
        Server groups allow you to assign products to a set of servers. New orders rotate across servers in the group based on the fill type.
      </p>

      <!-- Groups table -->
      <div v-if="store.groups.length > 0" class="bg-surface-card border border-border rounded-2xl overflow-hidden">
        <div class="hidden sm:grid grid-cols-[2fr_2fr_3fr_auto] gap-4 px-5 py-3 border-b border-border bg-white/[0.02]">
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Group Name</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Fill Type</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Servers</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Actions</span>
        </div>

        <div
          v-for="group in store.groups"
          :key="group.id"
          class="grid grid-cols-1 sm:grid-cols-[2fr_2fr_3fr_auto] gap-2 sm:gap-4 px-5 py-3.5 border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors"
        >
          <span class="text-[0.875rem] font-medium text-text-primary">{{ group.name }}</span>
          <span class="text-[0.82rem] text-text-secondary">{{ FILL_TYPE_LABELS[group.fillType] }}</span>
          <span class="text-[0.82rem] text-text-secondary truncate">
            {{ group.servers.length > 0 ? group.servers.map(s => s.name).join(', ') : '—' }}
          </span>
          <div class="flex items-center gap-1">
            <button
              class="w-7 h-7 flex items-center justify-center rounded-lg text-text-muted hover:text-text-primary hover:bg-white/[0.06] transition-colors"
              @click="openEditGroup(group)"
            >
              <svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/>
                <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/>
              </svg>
            </button>
            <button
              class="w-7 h-7 flex items-center justify-center rounded-lg text-text-muted hover:text-status-red hover:bg-status-red/[0.08] transition-colors"
              @click="handleDeleteGroup(group)"
            >
              <svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <polyline points="3 6 5 6 21 6"/><path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6"/>
                <path d="M10 11v6"/><path d="M14 11v6"/>
                <path d="M9 6V4a1 1 0 0 1 1-1h4a1 1 0 0 1 1 1v2"/>
              </svg>
            </button>
          </div>
        </div>
      </div>

      <!-- Groups empty -->
      <div
        v-else
        class="bg-surface-card border border-border rounded-2xl flex items-center justify-center py-10"
      >
        <p class="text-[0.82rem] text-text-muted">No groups yet —
          <button class="text-primary-400 hover:text-primary-300 transition-colors" @click="openAddGroup">create one</button>
        </p>
      </div>
    </div>

    <!-- Server Form Modal -->
    <Teleport to="body">
      <ServerFormModal
        v-if="showServerModal"
        :server="editingServer"
        :saving="savingServer"
        @saved="handleSaveServer"
        @close="showServerModal = false"
      />
    </Teleport>

    <!-- Group Form Modal -->
    <Teleport to="body">
      <GroupFormModal
        v-if="showGroupModal"
        :group="editingGroup"
        :available-servers="store.servers"
        :saving="savingGroup"
        @saved="handleSaveGroup"
        @close="showGroupModal = false"
      />
    </Teleport>

  </div>
</template>
