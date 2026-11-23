<template>
  <!--
    A drawing, not a document: the whole thing is hidden from assistive
    technology and the surrounding section carries the same information in prose.
    Announcing a fake window chrome and seven decorative rows would add nothing a
    screen-reader user can act on.
  -->
  <UiCard
    padding="none"
    class="overflow-hidden shadow-nova-window !border-nova-border !bg-nova-surface"
    aria-hidden="true"
  >
    <UiStack direction="row" align="center" gap="xs" class="border-b border-nova-border bg-nova-surface-2 px-4 py-3">
      <UiStack :as="UiStackTag.Span" gap="none" class="h-2.5 w-2.5 rounded-full bg-nova-danger" />
      <UiStack :as="UiStackTag.Span" gap="none" class="h-2.5 w-2.5 rounded-full bg-nova-accent" />
      <UiStack :as="UiStackTag.Span" gap="none" class="h-2.5 w-2.5 rounded-full bg-nova-success" />
      <UiText :as="UiTextTag.Span" size="inherit" tone="inherit" weight="medium" class="ml-2 text-[13px] text-nova-muted">
        {{ t('nova.dashboard.windowTitle') }}
      </UiText>
    </UiStack>

    <UiList list-style="bare" class="divide-y divide-[color:var(--n-border)]">
      <UiStack
        v-for="row in DASHBOARD_ROWS"
        :key="row.key"
        :as="UiStackTag.Li"
        direction="row"
        align="center"
        gap="sm"
        class="px-4 py-3.5"
      >
        <UiIconTile :icon="row.icon" size="compact" tone="nova" />
        <UiText :as="UiTextTag.Span" size="inherit" tone="inherit" weight="medium" class="min-w-0 flex-1 truncate text-[15px] text-nova-ink">
          {{ t(row.labelKey) }}
        </UiText>
        <UiText :as="UiTextTag.Span" size="inherit" tone="inherit" class="shrink-0 text-[13px] text-nova-muted">
          {{ t(row.valueKey) }}
        </UiText>
      </UiStack>
    </UiList>
  </UiCard>
</template>

<script setup lang="ts">
/**
 * The control-panel window itself, shared by the hero and the dashboard section.
 * Rows come from `content.ts`; each names an area of the client area that exists.
 */
import { DASHBOARD_ROWS } from '~/templates/nova/content'
import { UiStackTag, UiTextTag } from '~/utils/ui/enums'

/** Translator for the row labels and values. */
const { t } = useI18n()
</script>
