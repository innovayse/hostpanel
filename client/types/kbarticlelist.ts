import type { KbArticle } from '~/types/kbarticle'

/** The envelope the knowledgebase article endpoints answer with. */
export interface KbArticleList {
  /** The articles on this page of results. */
  items: KbArticle[]
}
