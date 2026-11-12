<template>
  <UiSection padding="lg" class="border-b border-nova-border" data-testid="nova-apps">
    <NovaSectionHeader
      :eyebrow="t('nova.apps.eyebrow')"
      :title="t('nova.apps.title')"
      :lead="t('nova.apps.lead')"
    />

    <UiText
      v-if="installer.count"
      as="p"
      size="inherit"
      tone="inherit"
      class="mt-5 text-[15px] font-semibold text-nova-brand"
      data-testid="nova-apps-count"
    >
      {{ t('nova.apps.count', { count: installer.count }) }}
    </UiText>

    <UiBalancedGrid as="ul" :count="installer.categories.length" class="mt-8" data-testid="nova-apps-grid">
      <NovaFeatureCard
        v-for="category in installer.categories"
        :key="category"
        :feature="INSTALLER_CATEGORY_CARDS[category]"
      />
    </UiBalancedGrid>
  </UiSection>
</template>

<script setup lang="ts">
/**
 * nova one-click installer section: the count of installable apps (when the operator has set one)
 * and a card per fixed category (CMS, shop, forum, wiki, blog, gallery). The category list and the
 * count come from `useInstallerApps()`.
 */
import NovaFeatureCard from '~/templates/nova/sections/FeatureCard.vue'
import NovaSectionHeader from '~/templates/nova/sections/NovaSectionHeader.vue'
import { INSTALLER_CATEGORY_CARDS } from '~/templates/nova/content'

/** Translator for the copy. */
const { t } = useI18n()
/** The app count and the categories. */
const installer = useInstallerApps()
</script>
