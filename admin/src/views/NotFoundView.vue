<script setup lang="ts">
/**
 * Page-not-found screen: the catch-all of the router, drawn in the same centered glass card as the sign-in screens.
 *
 * Any unknown address lands here instead of being silently redirected to the sign-in page. The one action leads back to
 * the panel; a visitor who is not signed in is carried on to the sign-in page by the router guard.
 */
import { useI18n } from 'vue-i18n'
import AuthShell from '@/components/auth/AuthShell.vue'
import UiLink from '@/components/ui/UiLink.vue'
import { AppRoute } from '@/lib/routes'

/** Translation function. */
const { t } = useI18n()
</script>

<template>
  <AuthShell :title="t('auth.notFound.title')" :subtitle="t('auth.notFound.description')">
    <UiLink
      :to="AppRoute.Dashboard"
      variant="primary"
      size="lg"
      class="w-full"
      data-testid="not-found-back"
    >
      {{ t('auth.notFound.backToPanel') }}
    </UiLink>
  </AuthShell>
</template>
