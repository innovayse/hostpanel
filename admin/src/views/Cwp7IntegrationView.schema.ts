/**
 * Skeleton schema of the CWP7 module screen: the module card, the two quick action cards side by side and the servers card.
 *
 * Everything but the servers is static page text. The module card's head is the module's logo and name; the servers card
 * holds three placeholder boxes until the lookup answers, whatever the real number of servers is.
 */
import { SkeletonKind, SkeletonSpan } from '@/utils/ui/skeletonSchema'
import type { DetailSkeletonSchema } from '@/utils/ui/skeletonSchema'

/** What the CWP7 module screen is made of. */
const schema: DetailSkeletonSchema = {
  kind: SkeletonKind.Detail,
  columns: [
    [
      {
        key: 'module',
        identity: true,
        rows: ['shared.category', 'integrations.cwp7Integration.moduleType', 'integrations.cwp7Integration.developer'],
      },
      { key: 'createServer', span: SkeletonSpan.Half, title: 'integrations.cwp7Integration.createNewServer', lines: 1, actions: 1 },
      { key: 'createProduct', span: SkeletonSpan.Half, title: 'integrations.cwp7Integration.createNewProduct', lines: 1, actions: 1 },
      { key: 'servers', title: 'integrations.cwp7Integration.cwp7Servers', tiles: 3 },
    ],
  ],
}

export default schema
