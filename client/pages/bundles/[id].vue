<template>
  <BundleConfigure
    :pending="pending"
    :bundle="bundle"
    :rows="rows"
    :discount-label="bundle ? discountLabel(bundle) : ''"
    :lines="lines"
    :discount="discountText"
    :total="totalText"
    :complete="complete"
    @update:cycle="chooseCycle"
    @continue="addToCart"
  >
    <template v-if="bundle?.includesDomain && bundle.domainYears" #domain>
      <BundleDomainPicker
        :years="bundle.domainYears"
        :results="lookupResults"
        :pending="lookupPending"
        :has-zones="priceRows.length > 0"
        :selected="domainChoice"
        :error="domainError"
        @search="searchTlds"
        @select="chooseDomain"
        @clear="clearDomain"
      />
    </template>
  </BundleConfigure>
</template>

<script setup lang="ts">
/**
 * Bundle page: the order screen of one product bundle.
 * Route: /bundles/[id]
 *
 * The page only wires: `useBundlePage` reads the bundle and owns the visitor's choices, and
 * `BundleConfigure` and `BundleDomainPicker` render them.
 */
import { useBundlePage } from '~/composables/useBundlePage'

const {
  pending, bundle, rows, lines, complete, discountLabel, discountText, totalText,
  priceRows, lookupResults, lookupPending, domainChoice, domainError,
  chooseCycle, chooseDomain, clearDomain, searchTlds, addToCart,
} = await useBundlePage(Number(useRoute().params.id))
</script>
