<template>
  <section v-if="bundles.length > 0" class="py-16 bg-ui-page" data-testid="bundle-grid">
    <UiContainer>
      <div class="mb-10 text-center">
        <UiHeading :level="2" size="section">{{ $t('bundles.title') }}</UiHeading>
        <UiText tone="muted" class="mt-3">{{ $t('bundles.subtitle') }}</UiText>
      </div>

      <UiBalancedGrid :count="bundles.length">
        <UiCard
          v-for="bundle in bundles"
          :key="bundle.id"
          class="flex h-full flex-col gap-4"
          :data-testid="`bundle-card-${bundle.id}`"
        >
          <div class="flex flex-wrap items-start justify-between gap-2">
            <UiHeading :level="3" size="subsection">{{ bundle.name }}</UiHeading>
            <UiBadge v-if="discountLabel(bundle)" :data-testid="`bundle-discount-${bundle.id}`">{{ discountLabel(bundle) }}</UiBadge>
          </div>

          <UiText v-if="bundle.description" size="sm" tone="muted">{{ bundle.description }}</UiText>

          <UiList list-style="plain" class="flex-1 space-y-1.5">
            <UiListItem v-for="component in bundle.components" :key="component.productId" class="flex items-center gap-2 text-sm">
              <CheckCircle :size="15" :stroke-width="2" class="flex-shrink-0 text-cyan-700 dark:text-cyan-400" />
              {{ component.name }}
            </UiListItem>
            <UiListItem v-if="bundle.includesDomain && bundle.domainYears" class="flex items-center gap-2 text-sm" :data-testid="`bundle-domain-${bundle.id}`">
              <Globe :size="15" :stroke-width="2" class="flex-shrink-0 text-cyan-700 dark:text-cyan-400" />
              {{ $t('bundles.domainIncluded', { years: $t('domains.years', bundle.domainYears) }) }}
            </UiListItem>
          </UiList>

          <UiButton :to="localePath(`/bundles/${bundle.id}`)" full-width :data-testid="`bundle-choose-${bundle.id}`">
            {{ $t('bundles.choose') }}
          </UiButton>
        </UiCard>
      </UiBalancedGrid>
    </UiContainer>
  </section>
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
