<template>
  <div
    v-if="name && offer"
    class="flex items-center justify-between gap-3 text-sm"
    data-testid="bundle-summary-discount"
  >
    <span class="flex min-w-0 items-center gap-2 text-gray-600 dark:text-gray-400 [.tpl-classic_&]:text-classic-muted">
      <span class="truncate">{{ $t('bundles.summary.discount', { name }) }}</span>
      <UiBadge size="sm" rounded data-testid="bundle-summary-offer">{{ offer }}</UiBadge>
    </span>
    <span class="whitespace-nowrap text-gray-900 dark:text-white [.tpl-classic_&]:text-classic-ink" data-testid="bundle-summary-amount">-{{ discount }}</span>
  </div>
</template>

<script setup lang="ts">
/**
 * The bundle line of an order summary: the bundle's name, its discount in words and the estimated
 * amount taken off. Renders nothing when `name` or `offer` is empty, i.e. when the cart holds no
 * bundle or the bundle has no discount, so a summary can include it unconditionally.
 *
 * Presentational: the amount is the storefront's estimate; the backend applies the real discount.
 */

/** Props of BundleSummary. */
interface Props {
  /** The bundle's name; empty when the cart holds no bundle. */
  name: string
  /** The discount in words, e.g. "10% off"; empty when the bundle has none. */
  offer: string
  /** The estimated discount, formatted. */
  discount: string
}

defineProps<Props>()
</script>
