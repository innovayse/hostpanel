<template>
  <header class="mb-8" data-testid="ui-page-header">
    <div v-if="$slots.breadcrumbs" class="mb-4">
      <slot name="breadcrumbs" />
    </div>
    <div class="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
      <div class="flex items-start gap-3 min-w-0">
        <div v-if="icon" class="w-11 h-11 rounded-xl bg-primary-500/10 flex items-center justify-center flex-shrink-0">
          <component :is="icon" :size="22" :stroke-width="2" class="text-primary-600 dark:text-primary-400" />
        </div>
        <div class="min-w-0">
          <UiHeading :level="1">{{ title }}</UiHeading>
          <UiText v-if="description" tone="muted" class="mt-1">{{ description }}</UiText>
        </div>
      </div>
      <div v-if="$slots.actions" class="flex flex-wrap items-center gap-3 flex-shrink-0">
        <slot name="actions" />
      </div>
    </div>
  </header>
</template>

<script setup lang="ts">
/**
 * UiPageHeader. The top of a page: optional breadcrumbs, the h1 title with an optional icon and
 * description, and an actions slot (buttons) aligned to the right.
 */
import type { Component } from 'vue'

/** Props of UiPageHeader. */
interface Props {
  /** The page title, rendered as the h1. */
  title: string
  /** One sentence under the title. */
  description?: string
  /** A lucide icon component shown beside the title. */
  icon?: Component
}

withDefaults(defineProps<Props>(), {
  description: undefined,
  icon: undefined,
})
</script>
