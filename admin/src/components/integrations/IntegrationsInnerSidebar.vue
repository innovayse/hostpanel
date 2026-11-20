<script setup lang="ts">
/**
 * Collapsible inner sidebar for the Integrations section.
 *
 * One entry per integration category, drawn by the kit's sidebar (icons only, captions on hover): the caption is the
 * category's short menu label, which the sidebar keeps on one line (cut with an ellipsis), and the tooltip is its full title.
 * The active entry follows the route: `/integrations` itself counts as the payments category.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { IntegrationCategory } from '@/types/integration'
import { INTEGRATION_CATEGORY_BY_KEY } from '@/types/integrationmeta'
import type { UiSidebarItem } from '@/utils/ui/sidebar'
import type { SkeletonTab } from '@/utils/ui/skeletonSchema'
import UiSidebar from '@/components/ui/UiSidebar.vue'
import { useActiveIntegrationCategory } from '@/composables/useActiveIntegrationCategory'
import { integrationCategoryPath } from '@/lib/routes'

/** Props for IntegrationsInnerSidebar. */
const props = defineProps<{
  /** The categories to list, one entry each, in order: the layout's schema, which is what its skeleton draws too. */
  categories: readonly SkeletonTab<IntegrationCategory>[]
}>()

/** Translation function. */
const { t } = useI18n()

/** Sidebar entries, one per category: the short caption in the menu, the full title as the tooltip. */
const items = computed<UiSidebarItem[]>(() =>
  props.categories.map(category => ({
    key: category.key,
    label: t(category.label),
    title: t(INTEGRATION_CATEGORY_BY_KEY[category.key].labelKey),
    to: integrationCategoryPath(category.key),
    icon: INTEGRATION_CATEGORY_BY_KEY[category.key].icon,
  })),
)

/** Key of the category for the current route. */
const activeKey = useActiveIntegrationCategory(() => props.categories.map(category => category.key))
</script>

<template>
  <UiSidebar :items="items" :active-key="activeKey" testid="integrations-nav" />
</template>
