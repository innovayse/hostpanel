<script setup lang="ts">
/**
 * Failed-load state of a list, table or report: the API's sentence in a danger alert and a Retry
 * button under it.
 *
 * A screen shows it instead of its empty state when the load was refused, so "nothing found" is
 * never claimed for data that could not be read. `testid` prefixes the test ids: the alert carries
 * the prefix itself and the button `<prefix>-retry`.
 */
import { useI18n } from 'vue-i18n'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiTone } from '@/utils/ui/tone'
import { UiActionKind } from '@/utils/ui/actions'

const { t } = useI18n()

/** Props for UiErrorState. */
withDefaults(defineProps<{
  /** The finished sentence of the failure (the API's own). */
  message: string
  /** Whether the retry is running (shows progress on the button). */
  loading?: boolean
  /** Prefix of the `data-testid`s. */
  testid?: string
}>(), {
  loading: false,
  testid: 'error-state',
})

const emit = defineEmits<{
  /** Emitted when the operator asks to load again. */
  retry: []
}>()
</script>

<template>
  <UiStack gap="sm" align="start">
    <UiAlert :variant="UiTone.Danger" :data-testid="testid">
      {{ message }}
    </UiAlert>
    <UiButton
      :action="UiActionKind.Refresh"
      type="button"
      :variant="UiButtonVariant.Secondary"
      :loading="loading"
      :data-testid="`${testid}-retry`"
      @click="emit('retry')"
    >
      {{ t('shared.retry') }}
    </UiButton>
  </UiStack>
</template>
