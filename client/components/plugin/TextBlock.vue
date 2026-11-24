<template>
  <UiText :as="UiTextTag.P" class="whitespace-pre-line" data-testid="plugin-text">{{ block.text }}</UiText>
</template>

<script setup lang="ts">
/**
 * PluginTextBlock: plain text from a plugin. Interpolated, never bound as HTML, so markup a plugin writes is
 * shown as the characters it is; line breaks are kept.
 */
import type { PluginTextBlock } from '~/types/pluginblock'
import { UiTextTag } from '~/utils/ui/enums'

/** Props of PluginTextBlock. */
interface Props {
  /** The block. */
  block: PluginTextBlock
}

defineProps<Props>()
</script>
