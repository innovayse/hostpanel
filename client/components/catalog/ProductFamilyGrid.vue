<template>
  <section id="products-section" class="py-24 bg-ui-page relative" data-testid="products-catalog">
    <UiContainer>
      <!-- Product navigation -->
      <div class="flex flex-wrap justify-center gap-2 sm:gap-3 mb-12 sm:mb-14 md:mb-16">
        <UiButton
          v-for="(product, index) in products"
          :key="product.id"
          variant="subtle"
          size="sm"
          :icon="product.icon"
          :to="hrefFor(product)"
          class="font-semibold whitespace-nowrap animate-reveal-up motion-reduce:animate-none"
          :data-testid="`product-nav-${product.id}`"
          :class="revealDelay(index * 100)"
        >
          {{ product.name }}
        </UiButton>
      </div>

      <!-- Catalogue grid -->
      <UiBalancedGrid :count="products.length" data-testid="product-grid">
        <CatalogProductFamilyCard v-for="(product, index) in products" :key="product.id" :product="product" :index="index" />
      </UiBalancedGrid>
    </UiContainer>

    <!-- Decorative rule -->
    <UiDecor class="bottom-0 left-0 right-0 h-px bg-gradient-to-r from-transparent via-secondary-500/30 to-transparent" />
  </section>
</template>

<script setup lang="ts">
/**
 * The catalogue of the products page: a row of family shortcuts and one card per family.
 *
 * Each card is a `ProductFamilyCard`, coloured by its family's own accent. A family that has a
 * `learnMoreUrl` links there; one without links to its detail page.
 */

import { revealDelay } from '~/utils/reveal'
import type { ProductFamily } from '~/types/productfamily'
import { siteProductFamilyPath } from '~/constants/siteRoutes'

/** Props of ProductFamilyGrid. */
interface Props {
  /** The families to show, in order. */
  products: ProductFamily[]
}

defineProps<Props>()

/** Prefixes a path with the active language. */
const localePath = useLocalePath()

/**
 * The localized address a family's shortcut links to.
 *
 * @param product The family.
 * @returns Its learn-more page, or its detail page when it has none.
 */
const hrefFor = (product: ProductFamily): string => localePath(product.learnMoreUrl || siteProductFamilyPath(product.id))
</script>
