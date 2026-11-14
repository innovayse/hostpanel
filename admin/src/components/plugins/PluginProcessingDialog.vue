<script setup lang="ts">
/**
 * The modal that stands over a plugin job: while it runs, a loader with the plugin's name and the step in
 * plain words (installing, updating, removing, uploading), and nothing can dismiss it (no close button, no
 * Escape, no backdrop click). When the job ends it shows the outcome, the API's sentence for a refusal or a
 * done line with the restart note when the server must restart, and a Close button. It owns no state: the
 * screen drives it from {@link usePluginProcessing}.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiButton from '@/components/ui/UiButton.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiLoader from '@/components/ui/UiLoader.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import UiText from '@/components/ui/UiText.vue'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiSize } from '@/utils/ui/size'
import { UiTone } from '@/utils/ui/tone'
import type { PluginProcessingStep } from '@/types/plugin'

/** Translation function. */
const { t } = useI18n()

/** Props for PluginProcessingDialog. */
const props = defineProps<{
  /** Whether the dialog is shown. */
  open: boolean
  /** Name of the plugin or archive being worked on. */
  name: string
  /** What is being done to it. */
  step: PluginProcessingStep
  /** True while the job runs. */
  running: boolean
  /** Whether the job was refused. */
  failed: boolean
  /** The sentence the job ended with (the refusal, or a staged-version note), null for a plain success. */
  message: string | null
  /** Whether the server must restart for the change to take effect. */
  restartRequired: boolean
}>()

/** Events emitted by PluginProcessingDialog. */
const emit = defineEmits<{
  /** Emitted when the admin closes the finished dialog. */
  close: []
}>()

/** The sentence under the loader or the outcome: what the job does while it runs, "done" after a success. */
const stepText = computed<string>(() => t(`plugins.processing.${props.step}`, { name: props.name }))
</script>

<template>
  <UiDialog :open="open" :title="name" :dismissible="!running" data-testid="plugin-processing-dialog" @close="emit('close')">
    <UiStack v-if="running" align="center" gap="sm" data-testid="plugin-processing-running">
      <UiLoader :size="UiSize.Lg" :label="stepText" show-label />
    </UiStack>
    <UiStack v-else gap="sm" data-testid="plugin-processing-outcome">
      <UiStatusMessage
        :message="failed ? message : (message ?? t('plugins.processing.done'))"
        :tone="failed ? UiTone.Danger : UiTone.Success"
        data-testid="plugin-processing-message"
      />
      <UiText v-if="!failed && restartRequired" tone="muted" size="sm" data-testid="plugin-processing-restart">
        {{ t('plugins.processing.restartNeeded') }}
      </UiText>
    </UiStack>
    <template v-if="!running" #footer>
      <UiButton type="button" :variant="UiButtonVariant.Primary" data-testid="plugin-processing-close" @click="emit('close')">
        {{ t('plugins.processing.close') }}
      </UiButton>
    </template>
  </UiDialog>
</template>
