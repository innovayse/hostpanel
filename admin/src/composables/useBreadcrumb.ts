/**
 * The breadcrumb the top bar draws for the page the operator is on, or is about to be on.
 */
import { computed } from 'vue'
import type { ComputedRef } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute } from 'vue-router'
import { useNavSections } from '@/composables/useNavSections'
import { breadcrumbOf } from '@/lib/breadcrumb'
import { useNavigationStore } from '@/stores/navigation'
import type { UiBreadcrumbItem } from '@/utils/ui/breadcrumb'

/**
 * Builds the live trail of the current page.
 *
 * It follows the navigation in flight: while the screens of the target are still downloading the trail already names
 * the target, so the click is answered in the top bar at once and a hard load shows the real trail beside the
 * skeleton of the page. The trail comes from the navigation and the route record alone (`lib/breadcrumb.ts`), and
 * is translated again when the language or the plugins' menu changes.
 *
 * @returns A computed trail, outermost first, whose last step is the current page.
 */
export const useBreadcrumb = (): ComputedRef<UiBreadcrumbItem[]> => {
  /** Translation function, which also makes the trail follow a language switch. */
  const { t } = useI18n()

  /** The current route, which the pending navigation overrides. */
  const route = useRoute()

  /** The navigation in flight, if any. */
  const navigation = useNavigationStore()

  /** The navigation sections, plugins' entries included. */
  const sections = useNavSections()

  return computed<UiBreadcrumbItem[]>(() => {
    const target = navigation.pending ?? route
    const record = target.matched[target.matched.length - 1]
    return breadcrumbOf(target.path, record?.path ?? target.path, sections.value, (key) => t(key), target.meta.crumb)
  })
}
