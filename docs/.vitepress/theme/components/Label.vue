/**
 * Inline badge used in documentation pages to mark features as admin-only,
 * newly added, locally hosted, or SSO-managed.
 */
<script setup lang="ts">
/** The visual variant of the label. */
type LabelType = 'admin' | 'new' | 'local' | 'sso'

withDefaults(
  defineProps<{
    type: LabelType
    version?: string
  }>(),
  { version: undefined },
)

const labels: Record<LabelType, string> = {
  admin: 'Admin',
  new: 'New',
  local: 'Local',
  sso: 'SSO',
}
</script>

<template>
  <span :class="['label', `label--${type}`]">
    <template v-if="type === 'new' && version">New in {{ version }}</template>
    <template v-else>{{ labels[type] }}</template>
  </span>
</template>

<style scoped>
.label {
  display: inline-flex;
  align-items: center;
  font-size: 0.72rem;
  font-weight: 600;
  padding: 2px 7px;
  border-radius: 20px;
  vertical-align: middle;
  letter-spacing: 0.02em;
  line-height: 1.5;
  white-space: nowrap;
}

/* admin — purple */
.label--admin {
  background-color: rgba(139, 92, 246, 0.15);
  color: #7c3aed;
}

/* new — green */
.label--new {
  background-color: var(--vp-c-tip-soft, rgba(16, 185, 129, 0.12));
  color: var(--vp-c-tip-2, #059669);
}

/* local — gray */
.label--local {
  background-color: var(--vp-c-bg-soft);
  color: var(--vp-c-text-2);
  border: 1px solid var(--vp-c-divider);
}

/* sso — blue (brand) */
.label--sso {
  background-color: var(--vp-c-brand-soft);
  color: var(--vp-c-brand-1);
}
</style>
