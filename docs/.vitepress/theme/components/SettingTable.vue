/**
 * Renders a bordered table of configuration settings with name, type, default
 * value, and description columns.
 */
<script setup lang="ts">
/** A single row in the settings table. */
export interface Setting {
  name: string
  type: string
  default: string
  description: string
}

defineProps<{ settings: Setting[] }>()
</script>

<template>
  <div class="setting-table-wrap">
    <table class="setting-table">
      <thead>
        <tr>
          <th>Name</th>
          <th>Type</th>
          <th>Default</th>
          <th>Description</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="setting in settings" :key="setting.name">
          <td><code>{{ setting.name }}</code></td>
          <td><span class="setting-type">{{ setting.type }}</span></td>
          <td>
            <code v-if="setting.default !== '—' && setting.default !== ''">{{ setting.default }}</code>
            <span v-else class="setting-empty">—</span>
          </td>
          <td>{{ setting.description }}</td>
        </tr>
      </tbody>
    </table>
  </div>
</template>

<style scoped>
.setting-table-wrap {
  overflow-x: auto;
  margin: 16px 0;
  border-radius: 8px;
  border: 1px solid var(--vp-c-divider);
}

.setting-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.9rem;
}

.setting-table th,
.setting-table td {
  padding: 10px 14px;
  text-align: left;
  border-bottom: 1px solid var(--vp-c-divider);
  vertical-align: top;
}

.setting-table thead th {
  background-color: var(--vp-c-bg-soft);
  font-weight: 600;
  color: var(--vp-c-text-1);
  white-space: nowrap;
}

.setting-table tbody tr:last-child td {
  border-bottom: none;
}

.setting-table tbody tr:hover td {
  background-color: var(--vp-c-bg-soft);
}

.setting-type {
  font-family: var(--vp-font-family-mono);
  font-size: 0.82rem;
  color: var(--vp-c-brand-1);
  background-color: var(--vp-c-brand-soft);
  padding: 2px 6px;
  border-radius: 4px;
  white-space: nowrap;
}

.setting-empty {
  color: var(--vp-c-text-3);
}
</style>
