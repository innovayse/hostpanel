<template>
  <UiStack gap="xs" data-testid="plugin-header-block">
    <UiPageHeader :title="block.title" class="!mb-0" data-testid="plugin-header">
      <template v-if="actions.length" #actions>
        <PluginBlock v-for="action in actions" :key="action.key" :block="action.block" />
      </template>
    </UiPageHeader>
    <UiText
      v-if="message?.text"
      size="xs"
      :tone="message.failed ? 'danger' : 'success'"
      class="sm:text-right"
      data-testid="plugin-header-message"
    >{{ message.text }}</UiText>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * PluginHeaderBlock - a plugin view's title, with its action buttons beside it. The title is interpolated, never
 * bound as HTML. Each action is drawn through PluginBlock, so its own `visibleWhen` applies like any block's.
 *
 * The sentence an action answers with is shown once, on one line under the whole header, not under its button: a
 * sentence inside one button's column made that column taller than the others and the row's buttons fell out of
 * line. The latest action's sentence replaces the previous one.
 */
import { providePluginActionMessages } from '~/composables/usePluginView'
import type { PluginHeaderBlock } from '~/types/pluginblock'

/** Props of PluginHeaderBlock. */
interface Props {
  /** The block. */
  block: PluginHeaderBlock
}

/** The props. */
const props = defineProps<Props>()

/** The last sentence one of the header's actions answered with; null until one has. */
const message = providePluginActionMessages()

/**
 * The actions, each keyed by its route and label; position is added because a header may repeat a route under
 * another label but a view document never reorders its actions.
 */
const actions = computed(() => (props.block.actions ?? []).map((action, position) => ({
  key: `${action.route.method} ${action.route.path}:${action.label}:${position}`,
  block: action,
})))
</script>
