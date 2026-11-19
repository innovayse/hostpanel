<script setup lang="ts">
/**
 * Shown by `RoutedView` in place of a screen whose code could not be downloaded, even after the repeats.
 *
 * The usual cause is a release that replaced the files this page was built against, or a lost
 * connection; reloading fetches the current shell, which fixes the first and retries the second.
 */
import { useI18n } from 'vue-i18n'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { UiAlignItems } from '@/utils/ui/layout'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiActionKind } from '@/utils/ui/actions'

/** Translation function. */
const { t } = useI18n()

/** Reloads the page. */
const reload = (): void => {
  window.location.reload()
}
</script>

<template>
  <UiStack gap="md" :align="UiAlignItems.Start" data-testid="view-load-error">
    <UiAlert variant="error">
      {{ t('layout.viewLoadFailed') }}
    </UiAlert>
    <UiButton :action="UiActionKind.Refresh" :variant="UiButtonVariant.Secondary" data-testid="view-load-error-reload" @click="reload">
      {{ t('layout.reload') }}
    </UiButton>
  </UiStack>
</template>
