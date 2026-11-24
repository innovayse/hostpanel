<script setup lang="ts">
/**
 * Warning banner displayed after install or remove operations.
 * Prompts the admin to restart the server for changes to take effect.
 */
import { useI18n } from 'vue-i18n'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { UiButtonType, UiButtonVariant } from '@/utils/ui/button'
import { UiActionKind } from '@/utils/ui/actions'
import { UiAlignItems, UiDirection, UiGap, UiJustify } from '@/utils/ui/layout'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'
import { UiSize } from '@/utils/ui/size'

/** Translation function. */
const { t } = useI18n()

/** Props for RestartBanner. */
defineProps<{
  /** True while the restart request is in flight. */
  restarting: boolean
}>()

/** Events emitted by RestartBanner. */
const emit = defineEmits<{
  /** Emitted when the admin asks for the server restart. */
  restart: []
}>()
</script>

<template>
  <UiAlert :variant="UiTone.Warning" data-testid="plugins-restart-banner">
    <!-- Wraps so a narrow screen puts the button below the text instead of squeezing it. -->
    <UiStack :direction="UiDirection.Row" :align="UiAlignItems.Center" :justify="UiJustify.Between" :gap="UiGap.Md" wrap>
      <UiStack :gap="UiGap.Xs">
        <UiText :tone="UiTextTone.Warning" :size="UiTextSize.Lg" class="font-medium">{{ t('plugins.restartBanner.restartRequired') }}</UiText>
        <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">{{ t('plugins.restartBanner.pluginChangesWillTakeEffect') }}</UiText>
      </UiStack>
      <UiButton :action="UiActionKind.Refresh"
        :title="t('plugins.restartBanner.restartServer')"
        :aria-label="t('plugins.restartBanner.restartServer')"
        :type="UiButtonType.Button"
        :variant="UiButtonVariant.Secondary"
        :size="UiSize.Sm"
        :loading="restarting"
        data-testid="plugins-restart"
        @click="emit('restart')"
      >
        {{ t('plugins.restartBanner.restart') }}
      </UiButton>
    </UiStack>
  </UiAlert>
</template>
