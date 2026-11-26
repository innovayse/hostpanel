/**
 * Skeleton schema of the knowledgebase screen: a header over the strip of the two add tabs, the add category card (the tab
 * the screen opens on), the breadcrumb line and the card of the categories with their table.
 *
 * The screen is a composite. `KnowledgebaseView` draws the tab strip, the add category card and the card of the categories
 * with the head of their table from this data, and `UiSkeletonOf` draws the same blocks while the view chunk is
 * downloading. The article tab, the tag browser and the article list are content of the loaded screen and not part of it.
 */
import { navTitleKey } from '@/lib/navigation'
import { AppRoute } from '@/lib/routes'
import { KnowledgebaseTab } from '@/types/supporttab'
import { SkeletonFieldKind, SkeletonKind, SkeletonPartKind, SkeletonSpan } from '@/utils/ui/skeletonSchema'
import type { CardsSkeletonPart, CompositeSkeletonSchema, FormSkeletonPart, SkeletonTable, TabsSkeletonPart } from '@/utils/ui/skeletonSchema'
import { UiCardRole, UiColumnKind, UiColumnSize } from '@/utils/ui/table'

/** Key of the card of the categories table, the suffix of its `card-<key>` slot. */
export const KB_CATEGORIES_CARD = 'categories'

/** The strip of the two add tabs, one per {@link KnowledgebaseTab}. */
export const KB_TABS_PART: TabsSkeletonPart = {
  kind: SkeletonPartKind.Tabs,
  tabs: [
    { key: KnowledgebaseTab.Category, label: 'shared.createCategory' },
    { key: KnowledgebaseTab.Article, label: 'support.knowledgebase.createArticle' },
  ],
}

/** The add category card: its name, the hidden flag and its description, one under the other. */
export const KB_CATEGORY_FORM_PART: FormSkeletonPart = {
  kind: SkeletonPartKind.Form,
  columns: [
    [
      {
        key: 'category',
        columns: 1,
        fields: [
          { key: 'name', label: 'shared.categoryName', span: SkeletonSpan.Full },
          { key: 'hidden', kind: SkeletonFieldKind.Check, span: SkeletonSpan.Full },
          { key: 'description', label: 'shared.description', span: SkeletonSpan.Full },
        ],
      },
    ],
  ],
}

/** The categories table: name, description, status and the row actions. */
export const KB_CATEGORIES_TABLE: SkeletonTable = {
  columns: [
    { key: 'name', label: 'shared.name', size: UiColumnSize.Grow },
    { key: 'description', label: 'shared.description' },
    { key: 'status', label: 'shared.status', kind: UiColumnKind.Status, cardRole: UiCardRole.Badge },
    { key: 'actions', actions: true },
  ],
}

/** The card of the categories, headed "Browse by category". */
export const KB_CATEGORIES_PART: CardsSkeletonPart = {
  kind: SkeletonPartKind.Cards,
  columns: [[{ key: KB_CATEGORIES_CARD, title: 'support.knowledgebase.browseByCategory', table: KB_CATEGORIES_TABLE }]],
}

/** What the knowledgebase screen is made of; the breadcrumb line is a placeholder of the one line of text above the categories. */
const schema: CompositeSkeletonSchema = {
  kind: SkeletonKind.Composite,
  header: { title: navTitleKey(AppRoute.SupportKnowledgebase), subtitle: 'support.knowledgebase.manageKnowledgebaseArticlesAndCategories' },
  parts: [KB_TABS_PART, KB_CATEGORY_FORM_PART, { kind: SkeletonPartKind.Lines }, KB_CATEGORIES_PART],
}

export default schema
