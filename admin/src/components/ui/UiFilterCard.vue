<script setup lang="ts">
/**
 * Reusable filter card wrapper with named slots for filter sections.
 * Provides consistent styling for any page with filters.
 */
</script>

<template>
  <div class="bg-surface-card border border-border rounded-2xl p-4 mb-6 space-y-4">
    <slot name="fields" />
    <slot />
    <slot name="dates" />
    <slot name="actions" />
  </div>
</template>
