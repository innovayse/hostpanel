<script setup lang="ts">
import { useData } from 'vitepress'
import { computed } from 'vue'

const props = defineProps<{
  path?: string
  clientPath?: string
}>()

const { theme } = useData()

const adminUrl = computed<string>(() => {
  const base = ((theme.value as Record<string, string>).demoAdminUrl ?? '').replace(/\/+$/, '')
  return base && props.path ? `${base}${props.path}` : ''
})

const clientUrl = computed<string>(() => {
  const base = ((theme.value as Record<string, string>).demoClientUrl ?? '').replace(/\/+$/, '')
  return base && props.clientPath ? `${base}${props.clientPath}` : ''
})
</script>

<template>
  <a v-if="adminUrl" :href="adminUrl" target="_blank" rel="noopener noreferrer">{{ adminUrl }}</a>
  <a v-if="clientUrl" :href="clientUrl" target="_blank" rel="noopener noreferrer">{{ clientUrl }}</a>
</template>
