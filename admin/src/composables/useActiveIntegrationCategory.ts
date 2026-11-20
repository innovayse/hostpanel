/**
 * The integration category the current route stands for, shared by the category sidebar and the narrow-screen tab
 * strip so both mark the same entry.
 */
import { computed } from 'vue'
import type { ComputedRef } from 'vue'
import { useRoute } from 'vue-router'
import { IntegrationCategory } from '@/types/integration'
import { AppRoute, integrationCategoryPath } from '@/lib/routes'

/**
 * Follows the route and names the category it shows.
 *
 * `/integrations` itself counts as the payments category; a route that is no category page (an integration's
 * configure page) has none.
 *
 * @param categories - Keys of the categories the menu lists, or a getter of them.
 * @returns Key of the active category, or undefined outside any category page.
 */
export const useActiveIntegrationCategory = (categories: () => readonly IntegrationCategory[]): ComputedRef<IntegrationCategory | undefined> => {
  /** Current route, whose path names the category. */
  const route = useRoute()

  return computed<IntegrationCategory | undefined>(() => {
    if (route.path === AppRoute.Integrations) {
      return IntegrationCategory.Payments
    }
    return categories().find(key => route.path === integrationCategoryPath(key))
  })
}
