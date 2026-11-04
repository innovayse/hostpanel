<script setup lang="ts">
import { useData } from 'vitepress'
import { computed } from 'vue'

const props = withDefaults(defineProps<{
  /** Path on the demo admin panel, e.g. "/servers". Omit when this is a client-only page. */
  path?: string
  /** Path on the demo client portal, e.g. "/client/invoices". Omit when this is an admin-only page. */
  clientPath?: string
}>(), {
  path: undefined,
  clientPath: undefined,
})

const { theme } = useData()

const adminUrl = computed<string | null>(() => {
  const base: string = (theme.value as Record<string, string>).demoAdminUrl ?? ''
  return base && props.path ? `${base}${props.path}` : null
})

const clientUrl = computed<string | null>(() => {
  const base: string = (theme.value as Record<string, string>).demoClientUrl ?? ''
  return base && props.clientPath ? `${base}${props.clientPath}` : null
})

const show = computed(() => adminUrl.value !== null || clientUrl.value !== null)
</script>

<template>
  <div v-if="show" class="demo-link-block">
    <a v-if="adminUrl" :href="adminUrl" target="_blank" rel="noopener noreferrer" class="demo-link">
      See it in the admin panel demo →
    </a>
    <a v-if="clientUrl" :href="clientUrl" target="_blank" rel="noopener noreferrer" class="demo-link">
      See it in the client portal demo →
    </a>
  </div>
</template>

<style scoped>
.demo-link-block {
  display: flex;
  flex-direction: column;
  gap: 6px;
  margin: 16px 0;
}

.demo-link {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 6px 14px;
  border-radius: 20px;
  border: 1px solid var(--vp-c-brand-1);
  color: var(--vp-c-brand-1);
  font-size: 0.875rem;
  font-weight: 500;
  text-decoration: none;
  width: fit-content;
  transition: background-color 0.15s, color 0.15s;
}

.demo-link:hover {
  background-color: var(--vp-c-brand-1);
  color: var(--vp-c-white);
  text-decoration: none;
}
</style>
