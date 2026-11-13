<script setup lang="ts">
/**
 * A page a plugin adds to the admin panel (`/apps/:plugin/:page*`), rendered from its view document.
 *
 * The page is found by its address, not by an id the panel guesses: the store asks the API's lookup by
 * path (`/admin/plugins/ui/{plugin}/pages/by-path`), which matches the plugin's page templates and answers
 * the view with its `externalOrigins`, or the one `PLUGIN_PAGE_NOT_FOUND` for a page that is unknown, not
 * available (switched off or not configured) or not the operator's to open. The page shows a skeleton while
 * it loads, a not-found state with the API's sentence, a failure with a retry, and otherwise the view. The
 * page title heads the view unless its first block is a header of its own. A language switch reads the page
 * again, since its labels come in the operator's language.
 */
import { computed, onMounted, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute } from 'vue-router'
import PluginView from '@/components/plugins/view/PluginView.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { usePluginUiStore } from '@/stores/pluginUi'
import { PluginBlockKind } from '@/types/pluginview'
import { UiGap } from '@/utils/ui/layout'

/** Translation function and the active locale. */
const { t, locale } = useI18n()

/** The current route: `plugin` names the plugin, `page` the segments below it. */
const route = useRoute()

/** Plugin ui store: the page on screen and its state. */
const store = usePluginUiStore()

/** The plugin id from the address; empty when the route carries none. */
const pluginId = computed<string>(() => {
  const raw = route.params.plugin
  return typeof raw === 'string' ? raw : ''
})

/** The decoded address segments below the plugin id; empty for the plugin's root page. */
const segments = computed<string[]>(() => {
  const raw = route.params.page
  if (Array.isArray(raw)) {
    return raw.filter((segment) => segment !== '')
  }
  return typeof raw === 'string' && raw !== '' ? [raw] : []
})

/** A key that changes with the page on screen, so another page mounts a fresh view with fresh state. */
const viewKey = computed<string>(() => `${pluginId.value}/${segments.value.join('/')}`)

/** Whether the view brings its own header, so the page title is not drawn twice. */
const ownHeader = computed<boolean>(() => store.page?.view.blocks[0]?.kind === PluginBlockKind.Header)

/**
 * Reads the page the address names.
 *
 * @returns Resolves when the read has ended.
 */
const load = async (): Promise<void> => {
  // The index tells a row or an action which pages it may open; the sidebar has usually read it already.
  await Promise.all([store.loadPage(pluginId.value, segments.value), store.loadUi()])
}

onMounted(load)

watch(viewKey, load)

watch(locale, load)
</script>

<template>
  <UiStack :gap="UiGap.Lg" class="w-full" data-testid="plugin-page">
    <template v-if="store.pageLoading">
      <UiPageHeader v-if="!ownHeader" loading data-testid="plugin-page-loading-header" />
      <UiCard padding="md" loading :skeleton-lines="4" data-testid="plugin-page-loading" />
    </template>
    <UiEmptyState
      v-else-if="store.pageNotFound"
      :title="t('plugins.pluginPage.notFoundTitle')"
      :description="store.pageError ?? undefined"
      data-testid="plugin-page-not-found"
    />
    <UiErrorState
      v-else-if="store.pageError !== null"
      :message="store.pageError"
      testid="plugin-page-error"
      @retry="load"
    />
    <template v-else-if="store.page !== null">
      <UiPageHeader v-if="!ownHeader" :title="store.page.title" data-testid="plugin-page-title" />
      <PluginView
        :key="viewKey"
        :plugin-id="store.page.pluginId"
        :view="store.page.view"
        :values="store.pageValues"
        :external-origins="store.page.externalOrigins"
      />
    </template>
  </UiStack>
</template>
