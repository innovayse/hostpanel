<template>
  <div class="grid items-start gap-8 lg:grid-cols-[minmax(0,1fr)_360px]">
    <div class="min-w-0" data-testid="ui-aside-layout-main">
      <slot name="main" />
    </div>
    <div class="min-w-0 lg:sticky lg:top-24" data-testid="ui-aside-layout-aside">
      <slot name="aside" />
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * UiAsideLayout: a main column beside a fixed-width side panel that stays in view while the page
 * scrolls (an order summary next to the product being configured).
 *
 * Slots `main` and `aside` are the two columns; below `lg` they stack, the main column first.
 */
</script>
