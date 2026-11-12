<script setup lang="ts">
/**
 * The compact list of steps a plugin's reply reports, each with the icon of its state, and how many have
 * finished.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiCard from '@/components/ui/UiCard.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { Icon } from '@/lib/icons'
import { PluginChatStepState } from '@/types/pluginview'
import type { PluginChatStep } from '@/types/pluginview'
import { UiAlignItems, UiDirection, UiGap } from '@/utils/ui/layout'
import { UiSize } from '@/utils/ui/size'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'

/** Props for PluginChatSteps. */
const props = defineProps<{
  /** The reply's steps, in the order they first arrived. */
  steps: readonly PluginChatStep[]
}>()

/** Translation function. */
const { t } = useI18n()

/** The icon of each step state. */
const STEP_ICONS: Readonly<Record<PluginChatStepState, string>> = {
  [PluginChatStepState.Running]: Icon.Refresh,
  [PluginChatStepState.Done]: Icon.Check,
  [PluginChatStepState.Failed]: Icon.Close,
  [PluginChatStepState.Stopped]: Icon.Remove,
}

/** The icon colour of each step state. */
const STEP_ICON_CLASSES: Readonly<Record<PluginChatStepState, string>> = {
  [PluginChatStepState.Running]: 'shrink-0 animate-spin text-primary-500 motion-reduce:animate-none',
  [PluginChatStepState.Done]: 'shrink-0 text-status-green',
  [PluginChatStepState.Failed]: 'shrink-0 text-status-red',
  [PluginChatStepState.Stopped]: 'shrink-0 text-text-muted',
}

/** How many of the steps have finished. */
const done = computed<number>(() => props.steps.filter((step) => step.state === PluginChatStepState.Done).length)
</script>

<template>
  <UiCard class="px-2.5 py-2" data-testid="plugin-chat-steps-box">
    <UiStack :gap="UiGap.Xs" data-testid="plugin-chat-steps">
      <UiStack
        v-for="step in steps"
        :key="step.key"
        :direction="UiDirection.Row"
        :gap="UiGap.Xs"
        :align="UiAlignItems.Center"
        :data-state="step.state"
        data-testid="plugin-chat-step"
      >
        <UiIcon :markup="STEP_ICONS[step.state]" :size="UiSize.Sm" :class="STEP_ICON_CLASSES[step.state]" />
        <UiText :size="UiTextSize.Xs" :tone="UiTextTone.Muted">{{ step.label }}</UiText>
        <UiText v-if="step.state === PluginChatStepState.Stopped" :size="UiTextSize.Xs" :tone="UiTextTone.Muted" data-testid="plugin-chat-step-stopped">{{ t('plugins.pluginPage.chatStepStopped') }}</UiText>
      </UiStack>
    </UiStack>
    <UiText :size="UiTextSize.Xs" :tone="UiTextTone.Muted" class="mt-1" data-testid="plugin-chat-steps-count">{{ t('plugins.pluginPage.chatStepsCount', { done, total: steps.length }) }}</UiText>
  </UiCard>
</template>
