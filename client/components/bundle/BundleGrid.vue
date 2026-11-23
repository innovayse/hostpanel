<template>
  <UiSection v-if="bundles.length > 0" padding="lg" class="bg-ui-page" data-testid="bundle-grid">
    <UiStack gap="sm" class="mb-10 text-center">
      <UiHeading :level="2" size="section">{{ $t('bundles.title') }}</UiHeading>
      <UiText tone="muted">{{ $t('bundles.subtitle') }}</UiText>
    </UiStack>

    <UiBalancedGrid :count="bundles.length">
      <UiCard
        v-for="bundle in bundles"
        :key="bundle.id"
        class="flex h-full flex-col gap-4"
        :data-testid="`bundle-card-${bundle.id}`"
      >
        <UiStack direction="row" wrap align="start" justify="between" gap="xs">
          <UiHeading :level="3" size="subsection">{{ bundle.name }}</UiHeading>
          <UiBadge v-if="discountLabel(bundle)" :data-testid="`bundle-discount-${bundle.id}`">{{ discountLabel(bundle) }}</UiBadge>
        </UiStack>

        <UiText v-if="bundle.description" size="sm" tone="muted">{{ bundle.description }}</UiText>

        <UiList list-style="plain" class="flex-1 space-y-1.5">
          <UiListItem v-for="component in bundle.components" :key="component.productId" :icon="CheckCircle" class="text-sm">
            {{ component.name }}
          </UiListItem>
          <UiListItem v-if="bundle.includesDomain && bundle.domainYears" :icon="Globe" class="text-sm" :data-testid="`bundle-domain-${bundle.id}`">
            {{ $t('bundles.domainIncluded', { years: $t('domains.years', bundle.domainYears) }) }}
          </UiListItem>
        </UiList>

        <UiButton :to="localePath(siteBundlePath(bundle.id))" variant="primary" full-width icon="mdi:arrow-right" :data-testid="`bundle-choose-${bundle.id}`">
          {{ $t('bundles.choose') }}
        </UiButton>
      </UiCard>
    </UiBalancedGrid>
  </UiSection>
</template>

<script setup lang="ts">
/**
 * The bundle offers of the products page: one card per active bundle with what it contains, whether
 * it includes a domain, its discount and the way into its order screen. Renders nothing while the
 * operator offers no bundle.
 *
 * Presentational: the page reads the bundles and words each discount.
 */
import { CheckCircle, Globe } from 'lucide-vue-next'
import type { CatalogBundle } from '~/types/catalogbundle'
import { siteBundlePath } from '~/constants/siteRoutes'

/** Props of BundleGrid. */
interface Props {
  /** The active bundles, in display order. */
  bundles: CatalogBundle[]
  /** Words a bundle's discount; empty for a bundle without one. */
  discountLabel: (bundle: CatalogBundle) => string
}

defineProps<Props>()

/** Prefixes a path with the active language. */
const localePath = useLocalePath()
</script>
