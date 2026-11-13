<template>
  <UiCard padding="none" class="px-2.5 py-2" data-testid="plugin-chat-steps-box">
    <UiStack as="ul" :gap="UiGap.Xs" data-testid="plugin-chat-steps">
      <UiStack
        v-for="step in steps"
        :key="step.key"
        as="li"
        :direction="UiDirection.Row"
        :gap="UiGap.Xs"
        :align="UiCrossAlign.Center"
        :data-state="step.state"
        data-testid="plugin-chat-step"
      >
        <component :is="STEP_ICONS[step.state]" :size="14" :class="STEP_ICON_CLASSES[step.state]" aria-hidden="true" />
        <UiText as="span" :size="UiTextSize.Xs" :tone="UiTextTone.Muted">{{ step.label }}</UiText>
        <UiText v-if="step.state === PluginChatStepState.Stopped" as="span" :size="UiTextSize.Xs" :tone="UiTextTone.Muted" data-testid="plugin-chat-step-stopped">{{ $t('plugins.chatStepStopped') }}</UiText>
      </UiStack>
    </UiStack>
    <UiText as="p" :size="UiTextSize.Xs" :tone="UiTextTone.Muted" class="mt-1" data-testid="plugin-chat-steps-count">{{ $t('plugins.chatStepsCount', { done, total: steps.length }) }}</UiText>
  </UiCard>
</template>

<script setup lang="ts">
/**
 * PluginChatSteps: the compact list of steps a plugin's reply reports, each with the icon of its state, and how many
 * have finished.
 */
import { UiCrossAlign, UiDirection, UiGap, UiTextSize, UiTextTone } from '~/utils/ui/enums'
import type { Component } from 'vue'
import { Check, CircleSlash, LoaderCircle, X } from 'lucide-vue-next'
import type { PluginChatStep } from '~/types/pluginchatstep'
import { PluginChatStepState } from '~/types/pluginchatstepstate'

/** Props of PluginChatSteps. */
interface Props {
  /** The reply's steps, in the order they first arrived. */
  steps: readonly PluginChatStep[]
}

/** The props. */
const props = defineProps<Props>()

/** The icon of each step state. */
const STEP_ICONS: Readonly<Record<PluginChatStepState, Component>> = {
  [PluginChatStepState.Running]: LoaderCircle,
  [PluginChatStepState.Done]: Check,
  [PluginChatStepState.Failed]: X,
  [PluginChatStepState.Stopped]: CircleSlash,
}

/** The icon colour of each step state, light and dark. */
const STEP_ICON_CLASSES: Readonly<Record<PluginChatStepState, string>> = {
  [PluginChatStepState.Running]: 'shrink-0 animate-spin text-primary-500 motion-reduce:animate-none',
  [PluginChatStepState.Done]: 'shrink-0 text-green-600 dark:text-green-400',
  [PluginChatStepState.Failed]: 'shrink-0 text-red-600 dark:text-red-400',
  [PluginChatStepState.Stopped]: 'shrink-0 text-gray-400',
}

/** How many of the steps have finished. */
const done = computed(() => props.steps.filter(step => step.state === PluginChatStepState.Done).length)
</script>
