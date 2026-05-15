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

/** ID of the plugin currently being removed (for per-card loading state). */
const removingId = ref<string | null>(null)

/** True while a restart request is in flight. */
const restarting = ref(false)

onMounted(store.fetchAll)

/**
 * Removes a plugin and sets the per-card loading indicator.
 *
 * @param id - Plugin identifier to remove.
 */
async function handleRemove(id: string): Promise<void> {
  removingId.value = id
  await store.remove(id)
  removingId.value = null
}

/**
 * Triggers a graceful server restart and shows a restarting indicator.
 */
async function handleRestart(): Promise<void> {
  restarting.value = true
  await store.restart()
  restarting.value = false
}

/**
 * Installs the given ZIP file via the store.
 *
 * @param file - ZIP file selected in PluginUploader.
 */
async function handleUpload(file: File): Promise<void> {
  await store.install(file)
}
</script>

<template>
  <div class="max-w-3xl">
    <!-- Page header -->
    <div class="mb-6">
      <h1 class="text-2xl font-bold text-gray-800">Plugin Manager</h1>
      <p class="text-sm text-gray-500 mt-1">Install and manage provider plugins</p>
    </div>

    <!-- Restart banner -->
    <RestartBanner
      v-if="store.requiresRestart"
      :restarting="restarting"
      class="mb-5"
      @restart="handleRestart"
    />

    <!-- Upload -->
    <PluginUploader
      class="mb-6"
      @upload="handleUpload"
    />

    <!-- Error -->
    <div v-if="store.error" class="mb-4 text-sm text-red-600">{{ store.error }}</div>

    <!-- Loading -->
    <div v-if="store.loading && store.plugins.length === 0" class="text-gray-400 text-sm">
      Loading plugins…
    </div>

    <!-- Empty state -->
    <div
      v-else-if="!store.loading && store.plugins.length === 0"
      class="text-sm text-gray-400 bg-white rounded-xl border border-gray-200 p-8 text-center"
    >
      No plugins installed yet. Upload a <span class="font-mono">.zip</span> file above to get started.
    </div>

    <!-- Plugin list -->
    <div v-else class="flex flex-col gap-3">
      <h2 class="text-sm font-semibold text-gray-600 uppercase tracking-wide">Installed Plugins</h2>
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
