<template>
  <div class="min-h-screen flex flex-col bg-page">
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

// Restore the visitor's saved colour mode. layouts/client.vue already does this
// for the authenticated area; the public site never did, because its background
// was pinned to a hard-coded dark value and the loss was invisible. Now that the
// background is a theme token, skipping this would show a light-mode visitor a
// dark page on every reload.
const { init } = useAppColorMode()

onMounted(() => init())
</script>
