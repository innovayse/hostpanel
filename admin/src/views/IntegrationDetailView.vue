<script setup lang="ts">
/**
 * Detail/config page for a single integration.
 *
 * Route: /integrations/:slug
 * Layout: breadcrumb + the shared config panel (2/3 config form + 1/3 status sidebar).
 * Special cases: slug "cwp" renders CwpIntegrationView and "cwp7" renders Cwp7IntegrationView.
 */
import { defineAsyncComponent, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useIntegrationsStore } from '@/stores/integrations'
import { IntegrationSlug } from '@/types/integration'
import IntegrationBreadcrumb from '@/components/integrations/IntegrationBreadcrumb.vue'
import IntegrationConfigPanel from '@/components/integrations/IntegrationConfigPanel.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { AppRoute } from '@/lib/routes'

/** Current route, whose param is the slug. */
const route = useRoute()

/** Router, to leave the CWP page when the plugin is not installed. */
const router = useRouter()

/** Integrations store: the loaded integration the breadcrumb names. */
const store = useIntegrationsStore()

/** Slug from the current route. */
const slug = route.params.slug as string

/** True when the current integration is the CWP plugin. */
const isCwp = slug === IntegrationSlug.Cwp

/** True when the current integration is CWP7. */
const isCwp7 = slug === IntegrationSlug.Cwp7

/** Async CWP integration page: only loaded when slug is "cwp". */
const CwpIntegrationView = defineAsyncComponent(
  () => import('@/views/CwpIntegrationView.vue'),
)

/** Async CWP7 integration page: only loaded when slug is "cwp7". */
const Cwp7IntegrationView = defineAsyncComponent(
  () => import('@/views/Cwp7IntegrationView.vue'),
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
  <Cwp7IntegrationView v-else-if="isCwp7" />

  <UiStack v-else gap="md" class="w-full">
    <IntegrationBreadcrumb
      :name="store.current?.name ?? slug"
      :is-plugin="store.current?.isPlugin ?? false"
      :icon-url="store.current?.iconUrl ?? null"
    />
    <IntegrationConfigPanel :slug="slug" />
  </UiStack>
</template>
