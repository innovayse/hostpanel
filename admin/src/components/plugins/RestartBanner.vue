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
  <UiAlert variant="warning" data-testid="plugins-restart-banner">
    <!-- Wraps so a narrow screen puts the button below the text instead of squeezing it. -->
    <UiStack direction="row" align="center" justify="between" gap="md" wrap>
      <UiStack gap="xs">
        <UiText tone="warning" size="lg" class="font-medium">{{ t('plugins.restartBanner.restartRequired') }}</UiText>
        <UiText tone="muted" size="sm">{{ t('plugins.restartBanner.pluginChangesWillTakeEffect') }}</UiText>
      </UiStack>
      <UiButton
        type="button"
        variant="secondary"
        size="sm"
        :disabled="restarting"
        data-testid="plugins-restart"
        @click="emit('restart')"
      >
        {{ restarting ? t('plugins.restartBanner.restarting') : t('plugins.restartBanner.restartServer') }}
      </UiButton>
    </UiStack>
  </UiAlert>
</template>
