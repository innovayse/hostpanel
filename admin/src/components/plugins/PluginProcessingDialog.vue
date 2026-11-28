<script setup lang="ts">
/**
 * The one modal that stands over a plugin job: an install, an update, a removal, an archive upload and a server restart.
 * It shows the plugin's logo and name, what is done to it and the version it brings, a bar with the real percentage, the
 * elapsed time and the list of steps, each with its icon and colour (pending grey, running in the accent with a spinner,
 * done green, failed red). Nothing can dismiss it while the job runs. When the job ends it shows the outcome, green with
 * the version that now runs or red with the reason, and a Close button; a restart that did not come back in time also
 * offers Retry. It owns no state: the screen hands it the state from {@link usePluginProcessing}, so every screen wires the
 * dialog the same way, one prop.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiButton from '@/components/ui/UiButton.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiPluginAvatar from '@/components/ui/UiPluginAvatar.vue'
import UiProgress from '@/components/ui/UiProgress.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import UiStepList, { UiStepState } from '@/components/ui/UiStepList.vue'
import type { UiStepListItem } from '@/components/ui/UiStepList.vue'
import UiText from '@/components/ui/UiText.vue'
import type { PluginProcessing } from '@/composables/usePluginProcessing'
import { usePluginsStore } from '@/stores/plugins'
import { PluginProcessingPhase } from '@/types/plugin'
import { UiActionKind } from '@/utils/ui/actions'
import { UiButtonType, UiButtonVariant } from '@/utils/ui/button'
import { UiAlignItems, UiDirection, UiGap } from '@/utils/ui/layout'
import { UiSize } from '@/utils/ui/size'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'

/** Translation function. */
const { t } = useI18n()

/** Props for PluginProcessingDialog. */
const props = defineProps<{
  /** The screen's processing state: the plugin, the steps, the percentage, how the job ended, and the actions. */
  processing: PluginProcessing
}>()

/** Plugins store: whether a restart is still needed, the plugins a restart applied and the retry of its wait. */
const store = usePluginsStore()

/** The heading line: what is done to the plugin. */
const stepText = computed<string>(() => t(`plugins.processing.${props.processing.step.value}`, { name: props.processing.name.value }))

/** The version line, empty when the job brings no version. */
const versionText = computed<string>(() =>
  props.processing.version.value === null ? '' : t('plugins.processing.targetVersion', { version: props.processing.version.value }),
)

/** The steps as the list draws them, each with its label. */
const steps = computed<UiStepListItem[]>(() =>
  props.processing.view.value.steps.map(item => ({ key: item.step, label: t(`plugins.processing.steps.${item.step}`), state: item.state })),
)

/** The spoken name of each step state. */
const stateLabels = computed<Record<UiStepState, string>>(() => ({
  [UiStepState.Pending]: t('plugins.processing.state.pending'),
  [UiStepState.Running]: t('plugins.processing.state.running'),
  [UiStepState.Done]: t('plugins.processing.state.done'),
  [UiStepState.Failed]: t('plugins.processing.state.failed'),
}))

/** The colour of the bar: the accent while the job runs, green once done, red when it failed. */
const barTone = computed<UiTone>(() => {
  const phase = props.processing.view.value.phase
  if (phase === PluginProcessingPhase.Failed) {
    return UiTone.Danger
  }
  return phase === PluginProcessingPhase.Done ? UiTone.Success : UiTone.Primary
})
</script>

<template>
  <UiDialog
    :open="processing.open.value"
    :title="processing.name.value"
    :dismissible="!processing.busy.value"
    data-testid="plugin-processing-dialog"
    @close="processing.close()"
  >
    <UiStack :gap="UiGap.Md" data-testid="plugin-processing-body">
      <UiStack :direction="UiDirection.Row" :align="UiAlignItems.Center" :gap="UiGap.Sm">
        <UiPluginAvatar
          :name="processing.name.value"
          :icon-url="processing.iconUrl.value"
          :color="processing.color.value"
          :size="UiSize.Lg"
          decorative
        />
        <UiStack :gap="UiGap.Xs">
          <UiText :tone="UiTextTone.Primary" data-testid="plugin-processing-step">{{ stepText }}</UiText>
          <UiText v-if="versionText !== ''" :tone="UiTextTone.Muted" :size="UiTextSize.Sm" data-testid="plugin-processing-version">
            {{ versionText }}
          </UiText>
        </UiStack>
      </UiStack>
      <UiProgress :value="processing.view.value.percent" :label="t('plugins.processing.progress')" :tone="barTone" data-testid="plugin-processing-progress" />
      <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm" data-testid="plugin-processing-elapsed">
        {{ t('plugins.processing.elapsed', { time: processing.elapsed.value }) }}
      </UiText>
      <UiStepList :steps="steps" :state-labels="stateLabels" />
      <UiStack v-if="processing.message.value !== null" :gap="UiGap.Sm" data-testid="plugin-processing-outcome">
        <UiStatusMessage
          :message="processing.message.value"
          :tone="processing.failed.value ? UiTone.Danger : UiTone.Success"
          data-testid="plugin-processing-message"
        />
        <UiText
          v-for="plugin in store.restartedPlugins"
          :key="plugin.id"
          :tone="UiTextTone.Muted"
          :size="UiTextSize.Sm"
          :data-testid="`plugin-processing-plugin-${plugin.id}`"
        >
          {{ t('plugins.restart.pluginVersion', { name: plugin.name, version: plugin.version }) }}
        </UiText>
        <UiText v-if="!processing.failed.value && store.requiresRestart" :tone="UiTextTone.Muted" :size="UiTextSize.Sm" data-testid="plugin-processing-restart">
          {{ t('plugins.processing.restartNeeded') }}
        </UiText>
      </UiStack>
    </UiStack>
    <template v-if="!processing.busy.value" #footer>
      <UiButton
        v-if="processing.canRetry.value"
        :action="UiActionKind.Refresh"
        :type="UiButtonType.Button"
        :variant="UiButtonVariant.Secondary"
        data-testid="plugin-processing-retry"
        @click="store.retryRestartWait()"
      >
        {{ t('shared.retry') }}
      </UiButton>
      <UiButton
        :action="UiActionKind.Close"
        :type="UiButtonType.Button"
        :variant="UiButtonVariant.Primary"
        data-testid="plugin-processing-close"
        @click="processing.close()"
      >
        {{ t('shared.close') }}
      </UiButton>
    </template>
  </UiDialog>
</template>
