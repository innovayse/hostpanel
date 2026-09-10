<template>
  <div class="min-h-dvh flex flex-col bg-page">
    <!-- Header supplied by the active template -->
    <component :is="header" />

    <!-- Main content -->
    <main class="flex-1">
      <slot />
    </main>

    <!-- Footer supplied by the active template -->
    <component :is="footer" />

    <!-- Floating Action Button (Telegram / WhatsApp / Live Chat) -->
    <UiFloatingActions />

    <!-- Cart Drawer (Side panel) -->
    <LayoutCartDrawer />
  </div>
</template>

<script setup lang="ts">
/**
 * Default layout with the active template's header and footer.
 * Applied to all pages unless a custom layout is specified.
 */
const { name, slot } = useTemplate()

const header = slot('header')
const footer = slot('footer')

// Stamps the active template on <html> so global CSS can scope to it. The body
// surface is the reason: aurora follows the theme tokens, classic keeps its
// fixed dark. See assets/styles/global.css.
useHead({ htmlAttrs: { 'data-template': name } })

// The colour mode is not restored here any more: plugins/color-mode.ts renders the
// visitor's cookie (or the operator's default) into the <html> class on the server,
// so there is nothing left for the layout to do on mount.
</script>
