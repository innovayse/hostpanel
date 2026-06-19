<script setup lang="ts">
/**
 * Plugin Manager page.
 *
 * Lists installed plugins, provides ZIP upload for installation,
 * and shows a restart banner when pending changes need to take effect.
 */
import { ref, onMounted } from 'vue'
import { usePluginsStore } from '../stores/pluginsStore'
import PluginCard from '../components/PluginCard.vue'
import PluginUploader from '../components/PluginUploader.vue'
import RestartBanner from '../components/RestartBanner.vue'

const store = usePluginsStore()

const removingId = ref<string | null>(null)
const restarting = ref(false)

onMounted(store.fetchAll)

async function handleRemove(id: string): Promise<void> {
  removingId.value = id
  await store.remove(id)
  removingId.value = null
}

async function handleRestart(): Promise<void> {
  restarting.value = true
  await store.restart()
  restarting.value = false
}

async function handleUpload(file: File): Promise<void> {
  await store.install(file)
}
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full max-w-3xl">

    <!-- Header -->
    <div class="mb-6">
      <h1 class="font-display font-bold text-[1.25rem] text-text-primary leading-none">Plugin Manager</h1>
      <p class="text-[0.78rem] text-text-secondary mt-1">
        Install and manage provider plugins
        <span v-if="store.plugins.length > 0" class="ml-1 text-text-muted">
          · {{ store.plugins.length }} installed
        </span>
      </p>
    </div>

    <!-- Restart banner -->
    <RestartBanner
      v-if="store.requiresRestart"
      :restarting="restarting"
      class="mb-5"
      @restart="handleRestart"
    />

    <!-- Upload -->
    <PluginUploader class="mb-6" @upload="handleUpload" />

    <!-- Error -->
    <div
      v-if="store.error"
      class="mb-4 text-[0.82rem] text-status-red bg-status-red/8 border border-status-red/20 rounded-xl p-4"
    >
      {{ store.error }}
    </div>

    <!-- Loading -->
    <div v-if="store.loading && store.plugins.length === 0" class="flex items-center gap-3 text-text-secondary text-sm">
      <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
      Loading plugins…
    </div>

    <!-- Empty state -->
    <div
      v-else-if="!store.loading && store.plugins.length === 0"
      class="bg-surface-card border border-border rounded-2xl flex flex-col items-center justify-center py-16 gap-3"
    >
      <div class="w-12 h-12 rounded-2xl bg-white/[0.04] border border-border flex items-center justify-center">
        <svg class="w-6 h-6 text-text-muted" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
          <rect x="2" y="3" width="20" height="14" rx="2"/>
          <line x1="8" y1="21" x2="16" y2="21"/>
          <line x1="12" y1="17" x2="12" y2="21"/>
        </svg>
      </div>
      <p class="text-[0.875rem] font-medium text-text-secondary">No plugins installed yet</p>
      <p class="text-[0.78rem] text-text-muted">Upload a <span class="font-mono text-primary-400">.zip</span> file above to get started</p>
    </div>

    <!-- Plugin list -->
    <div v-else class="flex flex-col gap-2">
      <p class="text-[0.72rem] font-semibold text-text-muted uppercase tracking-widest mb-1">Installed Plugins</p>
      <PluginCard
        v-for="plugin in store.plugins"
        :key="plugin.id"
        :plugin="plugin"
        :removing="removingId === plugin.id"
        @remove="handleRemove"
      />
    </div>

  </div>
</template>
