<script setup lang="ts">
/**
 * Collapsible inner sidebar for the Integrations section.
 *
 * One entry per integration category, drawn by the kit's sidebar (icons only, captions on hover).
 * The active entry follows the route: `/integrations` itself counts as the payments category.
 */
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { INTEGRATION_CATEGORIES } from '@/types/integrationmeta'
import { IntegrationCategory } from '@/types/integration'
import type { UiSidebarItem } from '@/utils/ui/sidebar'
import UiSidebar from '@/components/ui/UiSidebar.vue'
import { AppRoute, integrationCategoryPath } from '@/lib/routes'

/** Current route, to mark the active category. */
const route = useRoute()

/** Inner SVG markup (shape tags only) of each category icon. */
const CATEGORY_ICONS: Record<IntegrationCategory, string> = {
  [IntegrationCategory.Payments]: '<rect x="1" y="4" width="22" height="16" rx="2" ry="2"/><line x1="1" y1="10" x2="23" y2="10"/>',
  [IntegrationCategory.Registrars]: '<circle cx="12" cy="12" r="10"/><line x1="2" y1="12" x2="22" y2="12"/><path d="M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z"/>',
  [IntegrationCategory.Provisioning]: '<rect x="2" y="3" width="20" height="14" rx="2"/><line x1="8" y1="21" x2="16" y2="21"/><line x1="12" y1="17" x2="12" y2="21"/>',
  [IntegrationCategory.Email]: '<path d="M4 4h16c1.1 0 2 .9 2 2v12c0 1.1-.9 2-2 2H4c-1.1 0-2-.9-2-2V6c0-1.1.9-2 2-2z"/><polyline points="22,6 12,13 2,6"/>',
  [IntegrationCategory.Fraud]: '<path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/>',
  [IntegrationCategory.Fulfilment]: '<path d="M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z"/><polyline points="3.27 6.96 12 12.01 20.73 6.96"/><line x1="12" y1="22.08" x2="12" y2="12"/>',
  [IntegrationCategory.Identity]: '<path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/>',
  [IntegrationCategory.Hosted]: '<polyline points="22 12 18 12 15 21 9 3 6 12 2 12"/>',
}

/** Sidebar entries, one per category. */
const items = computed<UiSidebarItem[]>(() =>
  INTEGRATION_CATEGORIES.map(category => ({
    key: category.key,
    label: category.label,
    to: integrationCategoryPath(category.key),
    icon: CATEGORY_ICONS[category.key],
  })),
)

/** Key of the category for the current route. */
const activeKey = computed<string | undefined>(() => {
  if (route.path === AppRoute.Integrations) {
    return IntegrationCategory.Payments
  }
  return INTEGRATION_CATEGORIES.find(category => route.path === integrationCategoryPath(category.key))?.key
})
</script>

<template>
  <UiSidebar :items="items" :active-key="activeKey" testid="integrations-nav" />
</template>
