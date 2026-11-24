<script setup lang="ts">
/**
 * "Back to ..." navigation: a ghost pill with the back arrow and the short name of the place it returns to.
 *
 * It is the one back control of the panel. `UiPageHeader` draws it on its own row above the title at every width (`backTo`
 * and `backLabel`), so it never narrows the title; a screen whose header is not a `UiPageHeader` (a card header, a client sub-page) uses it directly. The
 * label names the destination ("Plugins"), never "Back to plugins": the arrow already says back. `data-testid` and other
 * attributes fall through to the link. Without a target (a skeleton) the pill is a disabled button, not an anchor that
 * leads nowhere.
 *
 * From `md` up the top bar's breadcrumb already names the parents of the page, so a screen whose back target is one of
 * them passes `mobileOnly`: the link then shows only below `md`, where the breadcrumb is cut to the current page. A
 * screen whose target is not in the breadcrumb (a tab of a record) keeps the link on every screen size.
 */
import type { RouteLocationRaw } from 'vue-router'
import UiButton from '@/components/ui/UiButton.vue'
import UiLink from '@/components/ui/UiLink.vue'
import { Icon } from '@/lib/icons'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiSize } from '@/utils/ui/size'

/** Props for UiBackLink. */
withDefaults(defineProps<{
  /** The route it returns to; absent in a skeleton, where the pill is drawn but leads nowhere yet. */
  to?: RouteLocationRaw
  /** Short name of the destination (already translated). */
  label: string
  /** Shows the link only below `md`, for a target the top bar's breadcrumb already offers from `md` up. */
  mobileOnly?: boolean
}>(), {
  to: undefined,
  mobileOnly: false,
})
</script>

<template>
  <UiButton v-if="to === undefined" :icon="Icon.Back" :variant="UiButtonVariant.Ghost" :size="UiSize.Sm" pill disabled class="max-w-full shrink-0 whitespace-nowrap" :class="{ 'md:hidden': mobileOnly }">
    {{ label }}
  </UiButton>
  <UiLink v-else :to="to" :icon="Icon.Back" :variant="UiButtonVariant.Ghost" :size="UiSize.Sm" pill class="max-w-full shrink-0 whitespace-nowrap" :class="{ 'md:hidden': mobileOnly }">
    {{ label }}
  </UiLink>
</template>
