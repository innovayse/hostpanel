<script setup lang="ts">
/**
 * Shell of the Integrations section: the category sidebar on the left, the routed page beside it.
 *
 * The categories the sidebar lists are the layout's schema, which is also what its skeleton draws. Built on the kit's
 * sidebar layout, which cancels the app layout's gutter so the sidebar meets the shell edge and, as the schema asks
 * (`stripBelowMd`), swaps the sidebar for a tab strip of the same categories below `md`.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import IntegrationsInnerSidebar from '@/components/integrations/IntegrationsInnerSidebar.vue'
import RoutedView from '@/components/layout/RoutedView.vue'
import UiSidebarLayout from '@/components/ui/UiSidebarLayout.vue'
import { useActiveIntegrationCategory } from '@/composables/useActiveIntegrationCategory'
import schema from '@/layouts/IntegrationsLayout.schema'
import { integrationCategoryPath } from '@/lib/routes'
import { tabItemsOfSchema } from '@/utils/ui/tabs'
import type { UiTabItem } from '@/utils/ui/tabs'

/** Translation function. */
const { t } = useI18n()

/** Router that opens the category a tab stands for. */
const router = useRouter()

/** One tab per category, listed by the layout's schema; captions are read each time, so a language switch applies. */
const tabs = computed<UiTabItem[]>(() => tabItemsOfSchema(schema.tabs, t, 'integrations-tab-'))

/** The category the route shows, or undefined outside any category page (no tab selected). */
const activeKey = useActiveIntegrationCategory(() => schema.tabs.map(tab => tab.key))

/**
 * Opens the category of the chosen tab.
 *
 * @param category - Key of the category.
 */
const openCategory = (category: string): void => {
  void router.push(integrationCategoryPath(category))
}
</script>

<template>
  <UiSidebarLayout
    :strip="schema.stripBelowMd ? tabs : undefined"
    :strip-value="activeKey ?? ''"
    strip-testid="integrations-tabs"
    data-testid="integrations-layout"
    @select-strip="openCategory"
  >
    <template #sidebar>
      <IntegrationsInnerSidebar :categories="schema.tabs" />
    </template>
    <RoutedView />
  </UiSidebarLayout>
</template>
