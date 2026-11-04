<template>
  <UiStack gap="lg" data-testid="plugin-page">
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

    <PluginView
      v-else
      :key="route.fullPath"
      :plugin-id="store.page.pluginId"
      :view="store.page.view"
      :external-origins="store.page.externalOrigins"
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
 * browser only (`ssr: false` in `nuxt.config.ts`), like every client page, so the read runs there.
 */
import { AlertCircle } from 'lucide-vue-next'
import { usePluginsStore } from '~/stores/plugins'

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
