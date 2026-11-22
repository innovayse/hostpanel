<script setup lang="ts">
/**
 * Detail/config page for a single integration.
 *
 * Route: /integrations/:slug
 * Layout: breadcrumb + the shared config panel (2/3 config form + 1/3 status sidebar).
 * Special case: slug "cwp" renders CwpIntegrationView.
 */
import { defineAsyncComponent, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useIntegrationsStore } from '@/stores/integrations'
import { IntegrationSlug } from '@/types/integration'
import IntegrationConfigPanel from '@/components/integrations/IntegrationConfigPanel.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { AppRoute } from '@/lib/routes'

/** Current route, whose param is the slug. */
const route = useRoute()

/** Router, to leave the CWP page when the plugin is not installed. */
const router = useRouter()

/** Integrations store: the list the CWP check reads. */
const store = useIntegrationsStore()

/** Slug from the current route. */
const slug = route.params.slug as string

/** True when the current integration is the CWP plugin. */
const isCwp = slug === IntegrationSlug.Cwp

/** Async CWP integration page: only loaded when slug is "cwp". */
const CwpIntegrationView = defineAsyncComponent(
  () => import('@/views/CwpIntegrationView.vue'),
)

/** Leaves for the plugins page when the CWP plugin is not installed. */
onMounted(() => {
  if (isCwp && store.integrations.length > 0 && !store.integrations.some(i => i.slug === IntegrationSlug.Cwp)) {
    void router.replace({ path: AppRoute.Plugins })
  }
})
</script>

<template>
  <CwpIntegrationView v-if="isCwp" />

  <UiStack v-else gap="md" class="w-full">
    <IntegrationConfigPanel :slug="slug" />
  </UiStack>
</template>
