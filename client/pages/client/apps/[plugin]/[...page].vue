<template>
  <UiStack :gap="workspace ? 'sm' : 'lg'" :class="workspace ? 'flex-1 p-3 md:min-h-0' : ''" data-testid="plugin-page">
    <UiStack v-if="store.pageLoading">
      <UiSkeleton shape="block" class="h-20" />
      <UiSkeleton shape="block" class="h-64" />
    </UiStack>

    <UiEmptyState v-else-if="store.pageNotFound" :icon="AlertCircle" :title="store.pageError" data-testid="plugin-page-not-found" />

    <!-- Any other failed read: the API's sentence and a way to read again. -->
    <UiAlert v-else-if="!store.page" variant="error" data-testid="plugin-page-error">
      {{ store.pageError }}
      <template #action>
        <UiButton variant="outline" size="sm" class="shrink-0" data-testid="plugin-page-retry" @click="refresh()">
          {{ $t('common.retry') }}
        </UiButton>
      </template>
    </UiAlert>

    <UiBackLink v-if="store.page && workspace && !headerHoldsBack" :to="backTarget" :label="$t('plugins.workspaceBack')" compact testid="plugin-page-back" />

    <PluginView
      v-if="store.page"
      :key="route.fullPath"
      :plugin-id="store.page.pluginId"
      :view="store.page.view"
      :external-origins="store.page.externalOrigins"
      :frame-origins="store.page.frameOrigins"
      :class="workspace ? 'flex-1 md:min-h-0' : ''"
      :values="store.pageValues"
    />
  </UiStack>
</template>

<script setup lang="ts">
/**
 * A page a plugin adds to the client area, `/client/apps/<plugin>/<path>`: the API matches the address after the
 * plugin id against the plugin's page templates (`/sites/{siteId}`) and answers the page's view document in the
 * reader's language, which the host's own components render block by block; the parameter values read from the
 * address fill the `{param}` segments of the routes its blocks call. Under `/client/**` the portal renders in the
 * browser only (`ssr: false` in `nuxt.config.ts`), like every client page, so the read runs there. A page declaring the
 * workspace layout fills the area the client layout leaves it, with a slim link back to its service.
 */
import { AlertCircle } from 'lucide-vue-next'
import { CLIENT_DASHBOARD_PATH, CLIENT_SERVICES_PATH } from '~/constants/clientRoutes'
import { providePluginBackLink } from '~/composables/usePluginView'
import { usePluginsStore } from '~/stores/plugins'
import { PluginBlockKind } from '~/types/pluginblockkind'
import { PluginPageLayout } from '~/types/pluginpagelayout'

definePageMeta({ layout: 'client', middleware: 'client-auth' })

/** The current route: the plugin and the page path. */
const route = useRoute()
/** The reader's locale; the page is read again in a new language. */
const { locale } = useI18n()
/** The plugin store, which reads the page. */
const store = usePluginsStore()

/** The plugin id in the route. */
const pluginId = computed(() => String(route.params.plugin))

/** The decoded address segments after the plugin id. */
const segments = computed<string[]>(() => {
  const raw = route.params.page
  return (Array.isArray(raw) ? raw.map(String) : [String(raw ?? '')]).filter(segment => segment !== '')
})

/** Whether the page on screen declares the workspace layout; anything unknown reads as standard. */
const workspace = computed(() => store.page?.layout === PluginPageLayout.Workspace)

/** Keeps the slim link in the reader's language. */
const localePath = useLocalePath()

/** The reader's translator, for the slim link's text. */
const { t } = useI18n()

/** Where the slim link goes: the service a `{serviceId}` page belongs to, otherwise the client area's start. */
const backTarget = computed(() => {
  const serviceId = store.page?.path.includes('{serviceId}') ? store.pageValues.serviceId : undefined
  return localePath(serviceId ? `${CLIENT_SERVICES_PATH}/${encodeURIComponent(serviceId)}` : CLIENT_DASHBOARD_PATH)
})

/** Whether the view opens with a compact header, which draws the slim link on its own line. */
const headerHoldsBack = computed(() => {
  const first = store.page?.view.blocks[0]
  return first?.kind === PluginBlockKind.Header && first.compact === true
})

providePluginBackLink(computed(() => (workspace.value ? { to: backTarget.value, label: t('plugins.workspaceBack') } : null)))

/** Reads the page again, for the retry button. The outcome is in the store; the handler answers `true` once the read
 * ended, because Nuxt treats an `undefined` answer as no data and would warn on every read. */
const { refresh } = await useAsyncData(
  () => `client-plugin-page-${pluginId.value}-${segments.value.join('/')}`,
  async (): Promise<boolean> => {
    await store.loadPage(pluginId.value, segments.value)
    return true
  },
  { watch: [locale] },
)

useHead(() => ({ title: store.page?.title }))
</script>
