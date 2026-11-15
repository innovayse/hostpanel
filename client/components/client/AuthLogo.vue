<template>
  <UiLink :to="SITE_HOME_PATH" tone="inherit" :underline="false" testid="auth-logo" class="mb-6 inline-block">
    <!-- Logo image cap: intrinsically small UI, not page layout. -->
    <Logo
      :surface="LogoSurface.Auto"
      :width="160"
      :height="48"
      img-class="h-12 w-auto max-w-[200px] mx-auto object-contain"
    />
  </UiLink>
</template>

<script setup lang="ts">
/**
 * Logo above the sign-in, register, forgot-password, reset-password and
 * accept-invite forms.
 *
 * Those pages follow the site colour mode (light by default, dark with the `dark` class), so this
 * uses the auto surface that swaps the wordmark with the mode. One
 * component rather than five copies of the same markup, which is what the pages had.
 */
import { LogoSurface } from '~/utils/appLogo'
import { SITE_HOME_PATH } from '~/constants/siteRoutes'
</script>
