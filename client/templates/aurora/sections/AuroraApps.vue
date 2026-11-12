<template>
  <UiSection padding="lg" :contained="false" class="relative border-t border-line" data-testid="aurora-apps">
    <UiText tone="inherit" size="inherit" :class="[EYEBROW_CLASS, 'text-ac1']">{{ t('aurora.apps.eyebrow') }}</UiText>
    <UiHeading :level="2" size="bare" :class="[SECTION_TITLE_CLASS, 'mt-3']">
      {{ t('aurora.apps.title') }}
    </UiHeading>
    <UiText v-if="installer.count" tone="inherit" size="inherit" class="mt-3 text-[16px] text-mut" data-testid="apps-count">
      {{ t('aurora.apps.count', { count: installer.count }) }}
    </UiText>

    <UiBalancedGrid :count="installer.categories.length" class="mt-8" data-testid="aurora-apps-grid">
      <UiCard
        v-for="category in installer.categories"
        :key="category"
        tone="bare"
        :data-testid="`apps-category-${category}`"
        class="border-line bg-surf shadow-card transition-colors hover:border-ac1"
      >
        <UiStack direction="row" align="center" gap="md">
          <UiTile size="lg" :icon="ICONS[category]" class="bg-tile-pink text-white" />
          <UiText as="span" size="inherit" tone="inherit" weight="semibold" class="text-[16px] text-tx">{{ t(`aurora.apps.${category}`) }}</UiText>
        </UiStack>
      </UiCard>
    </UiBalancedGrid>
  </UiSection>
</template>

<script setup lang="ts">
/**
 * aurora one-click installer: the app categories the installer covers and, when the operator
 * set one, how many apps it offers. The categories are fixed labels; the section is gated by
 * its homepage toggle.
 */
import type { Component } from 'vue'
import { BookOpen, Image, MessagesSquare, Newspaper, PenLine, ShoppingCart } from 'lucide-vue-next'
import { InstallerAppCategory } from '~/constants/installerAppCategory'
import { useInstallerApps } from '~/composables/useInstallerApps'
import { EYEBROW_CLASS, SECTION_TITLE_CLASS } from '~/templates/aurora/typography'
import UiStack from '~/components/ui/UiStack.vue'

/** Translator for the active locale. */
const { t } = useI18n()

/** The installer's app count and categories. */
const installer = useInstallerApps()

/** The tile icon of each category. */
const ICONS: Record<InstallerAppCategory, Component> = {
  [InstallerAppCategory.Cms]: Newspaper,
  [InstallerAppCategory.Shop]: ShoppingCart,
  [InstallerAppCategory.Forum]: MessagesSquare,
  [InstallerAppCategory.Wiki]: BookOpen,
  [InstallerAppCategory.Blog]: PenLine,
  [InstallerAppCategory.Gallery]: Image,
}
</script>
