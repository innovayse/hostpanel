<template>
  <UiStack
    v-if="name && offer"
    direction="row"
    align="center"
    justify="between"
    gap="sm"
    data-testid="bundle-summary-discount"
  >
    <UiStack direction="row" align="center" gap="xs" class="min-w-0">
      <UiText :as="UiTextTag.Span" size="sm" tone="muted" truncate class="[.tpl-classic_&]:text-classic-muted">{{ $t('bundles.summary.discount', { name }) }}</UiText>
      <UiBadge size="sm" rounded data-testid="bundle-summary-offer">{{ offer }}</UiBadge>
    </UiStack>
    <UiText :as="UiTextTag.Span" size="sm" class="whitespace-nowrap [.tpl-classic_&]:text-classic-ink" data-testid="bundle-summary-amount">-{{ discount }}</UiText>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * The bundle line of an order summary: the bundle's name, its discount in words and the estimated
 * amount taken off. Renders nothing when `name` or `offer` is empty, i.e. when the cart holds no
 * bundle or the bundle has no discount, so a summary can include it unconditionally.
 *
 * Presentational: the amount is the storefront's estimate; the backend applies the real discount.
 */
import { UiTextTag } from '~/utils/ui/enums'

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
