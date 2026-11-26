<script setup lang="ts">
/**
 * The modal that stands over a server restart. While the panel waits for the server it shows a loader and a short
 * hint, and nothing can dismiss it (no close button, no Escape, no backdrop click). When the server answers as a new
 * process it shows "Loaded and running" with the plugins the restart applied and the version each now runs, and a
 * Close button. When the server does not come back in time it shows the error and offers Retry (wait again) and Close.
 * It reads the restart flow from the plugins store and stops the wait when it goes away.
 */
import { computed, onBeforeUnmount } from 'vue'
import { useI18n } from 'vue-i18n'
import UiButton from '@/components/ui/UiButton.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiLoader from '@/components/ui/UiLoader.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import UiText from '@/components/ui/UiText.vue'
import { failedChangeOf, failureSentence } from '@/lib/pluginChange'
import { usePluginsStore } from '@/stores/plugins'
import { PluginRestartState } from '@/types/plugin'
import type { PluginListItemDto } from '@/types/plugin'
import { UiActionKind } from '@/utils/ui/actions'
import { UiButtonType, UiButtonVariant } from '@/utils/ui/button'
import { UiAlignItems, UiGap } from '@/utils/ui/layout'
import { UiSize } from '@/utils/ui/size'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'

/** Translation function. */
const { t } = useI18n()

/** Plugins store: where the restart stands, and the plugins it applied. */
const store = usePluginsStore()

/** Whether the dialog is shown: from the moment a restart is asked for until the operator closes the result. */
const open = computed<boolean>(() => store.restartState !== PluginRestartState.Idle)

/** Whether the panel is still waiting for the server; the dialog cannot be dismissed meanwhile. */
const waiting = computed<boolean>(() => store.restartState === PluginRestartState.Restarting)

/**
 * The sentence that explains why a plugin's change failed and which version runs instead.
 *
 * @param plugin - A listed plugin whose last change failed.
 * @returns The sentence, empty when the plugin's last change did not fail.
 */
const failureOf = (plugin: PluginListItemDto): string => {
  const failure = failedChangeOf(plugin)
  return failure === null ? '' : failureSentence(failure)
}

// A screen that goes away mid-restart must not leave the poll running.
onBeforeUnmount(() => store.resetRestart())
</script>

<template>
  <UiDialog
    :open="open"
    :title="t('plugins.restart.title')"
    :dismissible="!waiting"
    data-testid="plugin-restart-dialog"
    @close="store.resetRestart()"
  >
    <UiStack v-if="waiting" :align="UiAlignItems.Center" :gap="UiGap.Sm" data-testid="plugin-restart-waiting">
      <UiLoader :size="UiSize.Lg" :label="t('plugins.restart.restarting')" show-label />
      <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">{{ t('plugins.restart.hint') }}</UiText>
    </UiStack>
    <UiStack v-else-if="store.restartState === PluginRestartState.Running" :gap="UiGap.Sm" data-testid="plugin-restart-running">
      <UiStatusMessage :message="t('plugins.restart.running')" :tone="UiTone.Success" data-testid="plugin-restart-message" />
      <UiText
        v-for="plugin in store.restartedPlugins"
        :key="plugin.id"
        :tone="UiTextTone.Muted"
        :size="UiTextSize.Sm"
        :data-testid="`plugin-restart-plugin-${plugin.id}`"
      >
        {{ t('plugins.restart.pluginVersion', { name: plugin.name, version: plugin.version }) }}
      </UiText>
      <UiText
        v-for="plugin in store.restartFailures"
        :key="plugin.id"
        :tone="UiTextTone.Danger"
        :size="UiTextSize.Sm"
        :data-testid="`plugin-restart-failed-${plugin.id}`"
      >
        {{ t('plugins.restart.pluginFailed', { name: plugin.name }) }} {{ failureOf(plugin) }}
      </UiText>
    </UiStack>
    <UiStack v-else :gap="UiGap.Sm" data-testid="plugin-restart-timed-out">
      <UiStatusMessage :message="t('plugins.restart.timedOut')" :tone="UiTone.Danger" data-testid="plugin-restart-message" />
    </UiStack>
    <template v-if="!waiting" #footer>
      <UiButton
        v-if="store.restartState === PluginRestartState.TimedOut"
        :action="UiActionKind.Refresh"
        :type="UiButtonType.Button"
        :variant="UiButtonVariant.Secondary"
        data-testid="plugin-restart-retry"
        @click="store.retryRestartWait()"
      >
        {{ t('shared.retry') }}
      </UiButton>
      <UiButton :action="UiActionKind.Close" :type="UiButtonType.Button" :variant="UiButtonVariant.Primary" data-testid="plugin-restart-close" @click="store.resetRestart()">
        {{ t('shared.close') }}
      </UiButton>
    </template>
  </UiDialog>
</template>
