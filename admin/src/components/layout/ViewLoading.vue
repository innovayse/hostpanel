<script setup lang="ts">
/**
 * Stand-in shown in place of a screen whose code is still downloading.
 *
 * `RoutedView` shows it as soon as a navigation passes the guards, so a click is answered at once
 * instead of the content area sitting on the old page until the chunk arrives. The screen that is about to
 * open is not known yet (its code has not arrived), so no real layout can be drawn: it shows only what every screen
 * has, a title and a card, with the kit's own loading states; the screen's own loading state takes over from it.
 */
import { useI18n } from 'vue-i18n'
import UiCard from '@/components/ui/UiCard.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiStack from '@/components/ui/UiStack.vue'

/** Translation function. */
const { t } = useI18n()
</script>

<template>
  <UiStack gap="md" role="status" aria-busy="true" data-testid="view-loading">
    <UiPageHeader loading />
    <UiCard padding="md" loading />
    <span class="sr-only">{{ t('ui.loading') }}</span>
  </UiStack>
</template>
