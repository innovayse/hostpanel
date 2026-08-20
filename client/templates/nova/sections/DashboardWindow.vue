<template>
  <!--
    A drawing, not a document: the whole thing is hidden from assistive
    technology and the surrounding section carries the same information in prose.
    Announcing a fake window chrome and seven decorative rows would add nothing a
    screen-reader user can act on.
  -->
  <div
    class="overflow-hidden rounded-2xl border border-nova-border bg-nova-surface shadow-[0_28px_60px_-30px_rgba(0,0,0,0.55)]"
    aria-hidden="true"
  >
    <div class="flex items-center gap-2 border-b border-nova-border bg-nova-surface-2 px-4 py-3">
      <span class="h-2.5 w-2.5 rounded-full bg-nova-danger" />
      <span class="h-2.5 w-2.5 rounded-full bg-nova-accent" />
      <span class="h-2.5 w-2.5 rounded-full bg-nova-success" />
      <span class="ml-2 text-[13px] font-medium text-nova-muted">{{ t('nova.dashboard.windowTitle') }}</span>
    </div>

    <ul class="divide-y divide-[color:var(--n-border)]">
      <li
        v-for="row in DASHBOARD_ROWS"
        :key="row.key"
        class="flex items-center gap-3 px-4 py-3.5"
      >
        <span class="grid h-9 w-9 shrink-0 place-items-center rounded-lg bg-nova-surface-2 text-nova-brand">
          <Icon :name="row.icon" class="h-[18px] w-[18px]" />
        </span>
        <span class="min-w-0 flex-1 truncate text-[15px] font-medium text-nova-ink">{{ t(row.labelKey) }}</span>
        <span class="shrink-0 text-[13px] text-nova-muted">{{ t(row.valueKey) }}</span>
      </li>
    </ul>
  </div>
</template>

<script setup lang="ts">
/**
 * The control-panel window itself, shared by the hero and the dashboard section.
 * Rows come from `content.ts`; each names an area of the client area that exists.
 */
import { DASHBOARD_ROWS } from '~/templates/nova/content'

const { t } = useI18n()
</script>
