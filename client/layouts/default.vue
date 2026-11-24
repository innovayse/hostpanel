<template>
  <UiStack gap="none" :class="[scopeClass, 'min-h-dvh bg-page']" data-testid="default-layout">
    <!-- Demo-mode notice; renders nothing outside a demo -->
    <LayoutDemoBanner />

    <!-- Sign-in prompt after a silent sign-in found no session; renders nothing otherwise -->
    <LayoutSignInPrompt />

    <!-- Header supplied by the active template -->
    <component :is="header" />

    <!-- Main content -->
    <UiStack :as="UiStackTag.Main" gap="none" class="flex-1">
      <slot />
    </UiStack>

    <!-- Footer supplied by the active template -->
    <component :is="footer" />

    <!-- Floating Action Button (messenger links and live chat) -->
    <UiFloatingActions />

    <!-- Consent bar: public site only, never on the auth pages (they use no layout) -->
    <UiCookieBanner />

    <!-- Cart Drawer (Side panel) -->
    <LayoutCartDrawer />
  </UiStack>
</template>

<script setup lang="ts">
/**
 * Default layout with the active template's header and footer.
 * Applied to all pages unless a custom layout is specified.
 */
import { computed, toValue } from 'vue'
import { TemplateSlot } from '~/types/templateslot'
import { UiStackTag } from '~/utils/ui/enums'

const { name, slot } = useTemplate()

/**
 * Template scope class (`tpl-classic`, `tpl-aurora`, `tpl-nova`) on the layout root, so the
 * stylesheets can restyle the shared kit per template.
 */
const scopeClass = computed(() => `tpl-${toValue(name)}`)

/** The active template's header component. */
const header = slot(TemplateSlot.Header)

/** The active template's footer component. */
const footer = slot(TemplateSlot.Footer)

// Stamps the active template on <html> so global CSS can scope to it (nova's body
// tokens). Every template's body follows the colour-mode tokens; see
// assets/styles/global.css.
useHead({ htmlAttrs: { 'data-template': name } })

// The colour mode is not restored here: plugins/color-mode.ts renders the visitor's
// cookie (or the operator's default) into the <html> class on the server, so there is
// nothing for the layout to do on mount.
</script>
