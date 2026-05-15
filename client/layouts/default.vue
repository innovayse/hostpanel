<template>
  <div class="min-h-screen flex flex-col bg-[#0a0a0f]">
    <!-- Header -->
    <LayoutHeader />

    <!-- Main content -->
    <main class="flex-1">
      <slot />
    </main>

    <!-- Footer -->
    <LayoutFooter />

    <!-- Floating Action Button (Telegram / WhatsApp / Live Chat) -->
    <UiFloatingActions />

    <!-- Cart Drawer (Side panel) -->
    <LayoutCartDrawer />
  </div>
</template>

<script setup lang="ts">
/**
 * Default layout with Header and Footer
 * Applied to all pages unless a custom layout is specified
 */
</script>
