<template>
  <component :is="checkout" />
</template>

<script setup lang="ts">
/**
 * Checkout page.
 *
 * Thin by the same rule as the other public routes: the page owns the route,
 * the template owns the markup. Unlike the others there is no SEO block —
 * `routeRules` marks `/checkout/**` `ssr: false` with an
 * `X-Robots-Tag: noindex, nofollow` header, so there is nothing to describe to
 * a crawler and nothing a meta tag could add.
 *
 * Both templates currently resolve to the same component. `aurora` has no
 * checkout of its own yet: this page carries the ordering flow, and rebuilding
 * it in new markup is only safe once an order can actually be placed against a
 * running backend to prove nothing was dropped.
 */
const { slot } = useTemplate()

const checkout = slot('checkout')
</script>
