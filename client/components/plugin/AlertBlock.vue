<template>
  <UiAlert :variant="variant" :title="block.title" :live="false" data-testid="plugin-alert">{{ block.text }}</UiAlert>
</template>

<script setup lang="ts">
/**
 * PluginAlertBlock: a plugin's notice, in one of the host's alert tones. It is part of the page, not news, so it
 * is not a live region; its text is interpolated, never bound as HTML.
 */
import type { PluginAlertBlock } from '~/types/pluginblock'
import { pluginAlertVariant } from '~/utils/plugins'

/** Props of PluginAlertBlock. */
interface Props {
  /** The block. */
  block: PluginAlertBlock
}

/** The props. */
const props = defineProps<Props>()

/** The alert variant of the block's tone. */
const variant = computed(() => pluginAlertVariant(props.block.tone))
</script>
