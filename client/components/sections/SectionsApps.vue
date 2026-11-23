<template>
  <UiSection :contained="false" padding="lg" class="bg-classic-page" data-testid="sections-apps">
    <UiContainer>
      <ClassicSectionHeader
        class="mb-6"
        :eyebrow="$t('hostingSections.apps.badge')"
        :title="$t('hostingSections.apps.title')"
        :subtitle="$t('hostingSections.apps.subtitle')"
      />

      <UiText v-if="installer.count" tone="inherit" class="text-center mb-10 text-classic-muted" data-testid="sections-apps-count">
        {{ $t('hostingSections.apps.count', { count: installer.count }) }}
      </UiText>

      <UiBalancedGrid :count="installer.categories.length" class="mt-6" data-testid="sections-apps-grid">
        <ClassicCard
          v-for="category in installer.categories"
          :key="category"
          hover
          :data-testid="`app-${category}`"
        >
          <UiStack direction="row" align="center" gap="md">
            <ClassicIconTile :icon="CATEGORY_ICONS[category]" />
            <UiHeading :level="3" size="bare" class="text-lg font-semibold text-classic-ink">{{ $t(`hostingSections.apps.categories.${category}`) }}</UiHeading>
          </UiStack>
        </ClassicCard>
      </UiBalancedGrid>
    </UiContainer>
  </UiSection>
</template>

<script setup lang="ts">
/**
 * One-click apps section: the categories of the installer and, when the operator set it, how
 * many apps it offers. The section is shown whenever its toggle is on.
 */
import { InstallerAppCategory } from '~/constants/installerAppCategory'

/** The installer's app count and its categories. */
const installer = useInstallerApps()

/** Icon of each category. */
const CATEGORY_ICONS: Record<InstallerAppCategory, string> = {
  [InstallerAppCategory.Cms]: 'mdi:file-document-edit-outline',
  [InstallerAppCategory.Shop]: 'mdi:cart-outline',
  [InstallerAppCategory.Forum]: 'mdi:forum-outline',
  [InstallerAppCategory.Wiki]: 'mdi:book-open-page-variant-outline',
  [InstallerAppCategory.Blog]: 'mdi:post-outline',
  [InstallerAppCategory.Gallery]: 'mdi:image-multiple-outline',
}
</script>
