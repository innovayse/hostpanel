<script setup lang="ts">
/**
 * DemoLink: a link into the public demo, for a screen the page describes.
 *
 * Renders the admin and client links from the demo addresses in the theme data (`demoAdminUrl`
 * and `demoClientUrl`, set from the build environment), and nothing when an address is not set,
 * so a build without a demo shows no link. A trailing slash on an address is trimmed before the
 * path is joined to it.
 */
import { useData } from 'vitepress'
import { computed } from 'vue'

/** Props accepted by DemoLink. */
const props = defineProps<{
  /** Path of the screen in the admin panel, for example `/servers`. */
  path?: string
  /** Path of the screen in the client portal, for example `/client/services`. */
  clientPath?: string
}>()

const { theme } = useData()

/** Full address of the admin screen, or an empty string when there is no demo or no path. */
const adminUrl = computed<string>(() => {
  const base = ((theme.value as Record<string, string>).demoAdminUrl ?? '').replace(/\/+$/, '')
  return base && props.path ? `${base}${props.path}` : ''
})

/** Full address of the client portal screen, or an empty string when there is no demo or no path. */
const clientUrl = computed<string>(() => {
  const base = ((theme.value as Record<string, string>).demoClientUrl ?? '').replace(/\/+$/, '')
  return base && props.clientPath ? `${base}${props.clientPath}` : ''
})
</script>

<template>
  <a v-if="adminUrl" :href="adminUrl" target="_blank" rel="noopener noreferrer">{{ adminUrl }}</a>
  <a v-if="clientUrl" :href="clientUrl" target="_blank" rel="noopener noreferrer">{{ clientUrl }}</a>
</template>
