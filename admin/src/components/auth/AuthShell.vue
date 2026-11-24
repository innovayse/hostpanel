<script setup lang="ts">
/**
 * Centered glass card on the page ground, shared by the sign-in, first-run setup and email verification screens.
 *
 * Draws the brand wordmark for the active theme, the screen title and an optional subtitle; the screen's own content is the
 * default slot. An optional `banner` slot renders full width above the card, for example the demo notice of the sign-in
 * screen; the card stays centred in the space below it. The theme and language icon menus are pinned to the top right
 * corner of the page so they are reachable before signing in; the padding respects the device safe areas and the card
 * fits a 360px phone.
 */
import UiCard from '@/components/ui/UiCard.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import BrandLogo from '@/components/layout/BrandLogo.vue'
import LocaleMenu from '@/components/layout/LocaleMenu.vue'
import ThemeToggle from '@/components/layout/ThemeToggle.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { UiCardPadding, UiCardRadius } from '@/utils/ui/card'
import { UiAlignItems, UiDirection, UiGap, UiJustify } from '@/utils/ui/layout'
import { UiHeadingVariant, UiTextSize, UiTextTone } from '@/utils/ui/text'

/** Props for AuthShell. */
defineProps<{
  /** Screen title (already translated). */
  title: string
  /** Line under the title (already translated). */
  subtitle?: string
}>()

</script>

<template>
  <UiStack :gap="UiGap.None" class="ground min-h-dvh">
    <slot name="banner" />
    <!-- Pinned to the top right corner, clear of the notch and the side safe areas; the card below stays centred. -->
    <UiStack
      :direction="UiDirection.Row"
      :align="UiAlignItems.Center"
      :gap="UiGap.Xs"
      class="fixed right-0 top-0 z-10 pr-[max(1rem,env(safe-area-inset-right))] pt-[max(1rem,env(safe-area-inset-top))]"
    >
      <ThemeToggle testid="auth-theme" />
      <LocaleMenu testid="auth-locale" />
    </UiStack>
    <!-- Safe-area padding keeps the content clear of notches and the home indicator on a phone. -->
    <UiStack
      :align="UiAlignItems.Center"
      :justify="UiJustify.Center"
      :gap="UiGap.Md"
      class="flex-1 px-[max(1rem,env(safe-area-inset-left))] pb-[max(1rem,env(safe-area-inset-bottom))] pt-[max(4rem,env(safe-area-inset-top))]"
    >
      <UiCard elevated :padding="UiCardPadding.Md" :radius="UiCardRadius.Panel" class="w-full max-w-sm sm:p-8">
        <UiStack :gap="UiGap.Lg">
          <UiStack :direction="UiDirection.Row" :align="UiAlignItems.Center" :gap="UiGap.Sm">
            <!-- The wordmark for the active theme: it carries the name, so no text beside it. -->
            <BrandLogo img-class="h-9 w-auto" />
          </UiStack>
          <UiStack :gap="UiGap.Xs">
            <UiHeading :level="1" :variant="UiHeadingVariant.Page" class="font-display" data-testid="auth-title">
              {{ title }}
            </UiHeading>
            <UiText v-if="subtitle !== undefined" :tone="UiTextTone.Secondary" :size="UiTextSize.Md">
              {{ subtitle }}
            </UiText>
          </UiStack>
          <slot />
        </UiStack>
      </UiCard>
    </UiStack>
  </UiStack>
</template>
