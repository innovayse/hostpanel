<template>
  <UiPageHeader :title="block.title" class="!mb-0" data-testid="plugin-header">
    <template v-if="actions.length" #actions>
      <PluginBlock v-for="action in actions" :key="action.key" :block="action.block" />
    </template>
  </UiPageHeader>
</template>

<script setup lang="ts">
/**
 * PluginHeaderBlock - a plugin view's title, with its action buttons beside it. The title is interpolated, never
 * bound as HTML. Each action is drawn through PluginBlock, so its own `visibleWhen` applies like any block's.
 */
import type { PluginHeaderBlock } from '~/types/pluginblock'

/** Props of PluginHeaderBlock. */
interface Props {
  /** The block. */
  block: PluginHeaderBlock
}

/** The props. */
const props = defineProps<Props>()

/**
 * The actions, each keyed by its route and label; position is added because a header may repeat a route under
 * another label but a view document never reorders its actions.
 */
const actions = computed(() => (props.block.actions ?? []).map((action, position) => ({
  key: `${action.route.method} ${action.route.path}:${action.label}:${position}`,
  block: action,
})))
</script>
