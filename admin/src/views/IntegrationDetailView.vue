<script setup lang="ts">
/**
 * Detail/config page for a single integration.
 *
 * Route: /integrations/:slug
 * Layout: breadcrumb + the shared config panel (2/3 config form + 1/3 status sidebar).
 */
import { useRoute } from 'vue-router'
import IntegrationConfigPanel from '@/components/integrations/IntegrationConfigPanel.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { UiGap } from '@/utils/ui/layout'

/** Current route, whose param is the slug. */
const route = useRoute()

/** Slug from the current route. */
const slug = route.params.slug as string
</script>

<template>
  <UiStack :gap="UiGap.Md">
    <IntegrationConfigPanel :slug="slug" />
  </UiStack>
</template>
