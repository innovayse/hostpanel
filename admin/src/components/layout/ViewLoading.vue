<script setup lang="ts">
/**
 * Generic stand-in for a screen whose code is still downloading and that has no skeleton schema of its own.
 *
 * `RoutedView` draws the skeleton of the screen's schema when there is one (see `lib/skeletonRegistry.ts`); this is
 * only the named fallback for the screens that cannot have one, listed in `lib/skeletonExceptions.ts` (a plugin page
 * whose blocks the plugin defines, the sign-in and notice screens). Their shape is not known yet, so it shows only
 * what every screen has, a title and a card, with the kit's own loading states; the screen's own loading state takes
 * over from it.
 */
import { useI18n } from 'vue-i18n'
import UiCard from '@/components/ui/UiCard.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { UiCardPadding } from '@/utils/ui/card'
import { UiGap } from '@/utils/ui/layout'
import { UiTextTag } from '@/utils/ui/text'

/** Translation function. */
const { t } = useI18n()
</script>

<template>
  <UiStack :gap="UiGap.Md" role="status" aria-busy="true" data-testid="view-loading">
    <UiPageHeader loading />
    <UiCard :padding="UiCardPadding.Md" loading />
    <UiText :as="UiTextTag.Span" class="sr-only">{{ t('ui.loading') }}</UiText>
  </UiStack>
</template>
