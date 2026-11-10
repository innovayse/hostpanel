<script setup lang="ts">
/**
 * A plugin's notice in one of the admin's alert tones: an optional bold title over the sentence, both
 * interpolated as text.
 */
import { computed } from 'vue'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { pluginAlertTone } from '@/lib/pluginView'
import type { PluginAlertBlock } from '@/types/pluginview'
import { UiGap } from '@/utils/ui/layout'
import type { UiTone } from '@/utils/ui/tone'

/** Props for PluginAlertBlock. */
const props = defineProps<{
  /** The block. */
  block: PluginAlertBlock
}>()

/** The alert tone of the block's tone. */
const tone = computed<UiTone>(() => pluginAlertTone(props.block.tone))
</script>

<template>
  <UiAlert :variant="tone" data-testid="plugin-alert">
    <UiStack :gap="UiGap.Xs">
      <UiText v-if="block.title" class="font-semibold" data-testid="plugin-alert-title">{{ block.title }}</UiText>
      <UiText>{{ block.text }}</UiText>
    </UiStack>
  </UiAlert>
</template>
