/**
 * Skeleton schema of the storefront server location form: two single column cards under the page title.
 *
 * The general card holds the country code, the sort order and the active switch; the translations card holds the
 * language tabs, the name, the city and the latency note. The name carries the key `translations` because that is the key
 * under which the API reports a refused translation, so the form shows its sentence on the field without a mapping.
 */
import { SkeletonFieldKind, SkeletonKind, SkeletonSpan } from '@/utils/ui/skeletonSchema'
import type { FormSkeletonSchema } from '@/utils/ui/skeletonSchema'

/** What the location form is made of. */
const schema: FormSkeletonSchema = {
  kind: SkeletonKind.Form,
  header: {},
  columns: [
    [
      {
        key: 'general',
        title: 'storefront.form.general',
        columns: 1,
        fields: [
          { key: 'countryCode', label: 'storefront.locations.country' },
          { key: 'sortOrder', label: 'storefront.form.sortOrder' },
          { key: 'isActive', label: 'shared.active', kind: SkeletonFieldKind.Toggle },
        ],
      },
      {
        key: 'translations',
        title: 'storefront.form.translations',
        columns: 1,
        fields: [
          { key: 'locale', span: SkeletonSpan.Full },
          { key: 'translations', label: 'storefront.locations.name' },
          { key: 'city', label: 'storefront.locations.city' },
          { key: 'latencyNote', label: 'storefront.locations.latencyNote' },
        ],
      },
    ],
  ],
}

export default schema
