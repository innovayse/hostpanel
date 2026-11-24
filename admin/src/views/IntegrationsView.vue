<script setup lang="ts">
/**
 * Integration category page.
 *
 * Shows all integrations for the active category derived from the route param.
 * Falls back to 'payments' when accessed at /integrations. A category with nothing in it (identity, background jobs)
 * says what it holds instead of counting zero, and offers the plugin catalogue (while the registry is on) and the upload.
 */
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute } from 'vue-router'
import type { RouteLocationRaw } from 'vue-router'
import { useIntegrationsStore } from '@/stores/integrations'
import { usePluginsStore } from '@/stores/plugins'
import { INTEGRATION_CATEGORIES } from '@/types/integrationmeta'
import { IntegrationCategory } from '@/types/integration'
import { PluginsTab } from '@/types/plugin'
import IntegrationCard from '@/components/integrations/IntegrationCard.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import { useDocsGuide } from '@/composables/useDocsGuide'
import PageHelpLink from '@/components/layout/PageHelpLink.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { AppRoute } from '@/lib/routes'
import { UiActionKind } from '@/utils/ui/actions'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiGap } from '@/utils/ui/layout'
import { UiSize } from '@/utils/ui/size'

/** Translation function. */
const { t } = useI18n()
/** Link to the docs guide for this screen, or null when docsUrl is not configured. */
const { guideUrl } = useDocsGuide()

/** Current route, whose param names the category. */
const route = useRoute()

/** Integrations store: the list and the refusal sentence. */
const store = useIntegrationsStore()

/** Plugins store: whether the plugin registry is on, which decides if the empty state offers the catalogue. */
const plugins = usePluginsStore()

/** Active category key from route param, defaults to 'payments'. */
const activeCategory = computed<string>(() =>
  (route.params.category as string) || IntegrationCategory.Payments,
)

/** Metadata of the active category; undefined for a route param that names none. */
const category = computed(() => INTEGRATION_CATEGORIES.find(c => c.key === activeCategory.value))

/** Full title of the active category. */
const activeCategoryLabel = computed<string>(() => (category.value === undefined ? activeCategory.value : t(category.value.labelKey)))

/** Placeholder cards drawn in the grid while the list loads; the real count is only known once it has arrived. */
const LOADING_CARD_COUNT = 6

/** Integrations filtered to the active category. */
const filtered = computed(() =>
  store.integrations.filter(i => i.category === activeCategory.value),
)

/** Whether the first read of the list has answered, whatever the answer. */
const loaded = ref(false)

/** True until the first read of the list has answered: the page draws its real layout with placeholders where the values go. */
const loading = computed<boolean>(() => (store.loading || !loaded.value) && filtered.value.length === 0)

/** Number of active integrations in this category. */
const activeCount = computed<number>(() => filtered.value.filter(i => i.isEnabled).length)

/** True once the list has answered, without a refusal, and nothing in it belongs to the category. */
const empty = computed<boolean>(() => loaded.value && !store.error && filtered.value.length === 0)

/** Whether the plugin registry is on: the server has answered about it and did not refuse. */
const registryOn = computed<boolean>(() => !plugins.registryDisabled && plugins.registryInstance !== null)

/** The plugin catalogue filtered by the category's registry category (every plugin when the registry has none for it). */
const browseTarget = computed<RouteLocationRaw>(() => ({
  path: AppRoute.Plugins,
  query: { tab: PluginsTab.Browse, ...(category.value?.registryCategory === undefined ? {} : { category: category.value.registryCategory }) },
}))

/** Header line: what the category holds while it is empty, else how many integrations, and how many are active. */
const subtitle = computed<string>(() => {
  if (empty.value && category.value !== undefined) {
    return t(category.value.descriptionKey)
  }
  const total = t('integrations.integrations.lengthIntegrationValue', { length: filtered.value.length })
  return activeCount.value > 0
    ? `${total} ${t('integrations.integrations.activecountActive', { activeCount: activeCount.value })}`
    : total
})

/**
 * Asks the registry's state once a category turns out empty, so Browse is offered only while the registry is on.
 *
 * @param isEmpty - Whether the category is empty now.
 * @returns Resolves when the request, if one was needed, is over.
 */
const loadRegistryState = async (isEmpty: boolean): Promise<void> => {
  if (isEmpty && !plugins.registryDisabled && plugins.registryInstance === null) {
    await plugins.fetchRegistryInstance()
  }
}

/**
 * Reads the list, then lets the page tell an empty category from one still loading.
 *
 * @returns Resolves when the list has answered.
 */
const load = async (): Promise<void> => {
  await store.fetchAll()
  loaded.value = true
}

watch(empty, loadRegistryState)

/** Loads the integrations. */
onMounted(load)
</script>

<template>
  <UiStack :gap="UiGap.Md">
    <UiPageHeader :title="activeCategoryLabel" :subtitle="subtitle" :subtitle-loading="loading">
      <template v-if="guideUrl" #actions>
        <PageHelpLink :url="guideUrl" />
      </template>
    </UiPageHeader>

    <!-- Error -->
    <UiErrorState
      v-if="store.error"
      :message="store.error"
      :loading="store.loading"
      testid="integrations-error"
      @retry="store.fetchAll()"
    />

    <!-- Cards grid; while the list loads the same grid holds placeholder cards -->
    <UiGrid v-else-if="loading || filtered.length > 0" :columns="3" :gap="UiGap.Md" data-testid="integrations-grid">
      <template v-if="loading">
        <IntegrationCard v-for="index in LOADING_CARD_COUNT" :key="index" />
      </template>
      <template v-else>
        <IntegrationCard
          v-for="integration in filtered"
          :key="integration.slug"
          :integration="integration"
        />
      </template>
    </UiGrid>

    <!-- Empty state -->
    <UiEmptyState
      v-else
      :title="t('integrations.integrations.noIntegrationsInThisCategory')"
      :description="t('integrations.integrations.emptyHint')"
      data-testid="integrations-empty"
    >
      <template v-if="category" #icon>
        <UiIcon :markup="category.icon" :size="UiSize.Lg" />
      </template>
      <template #actions>
        <UiLink :action="UiActionKind.View"
          v-if="registryOn"
          :to="browseTarget"
          :variant="UiButtonVariant.Secondary"
          data-testid="integrations-empty-browse"
        >
          {{ t('integrations.integrations.browsePlugins') }}
        </UiLink>
        <UiLink
          :to="AppRoute.Plugins"
          :variant="UiButtonVariant.Primary"
          :action="UiActionKind.Upload"
          data-testid="integrations-empty-upload"
        >
          {{ t('plugins.plugins.uploadPlugin') }}
        </UiLink>
      </template>
    </UiEmptyState>
  </UiStack>
</template>
