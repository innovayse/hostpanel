/**
 * Pinia store for published editorial content: announcements and the knowledgebase.
 *
 * A server-rendered page wraps a load action in `useAsyncData(key, () => content.loadX())`
 * and renders store state, which `@pinia/nuxt` carries from the server render to the browser.
 * The store owns the loading flags and the failure sentence, so a page holds no `catch` and
 * never builds a URL: transport belongs to {@link useContentApi}.
 *
 * @module stores/content
 */

import { defineStore } from 'pinia'
import { useContentApi } from '~/composables/apis/useContentApi'
import type { LegalPage } from '~/constants/legalPage'
import { apiErrorMessage } from '~/lib/errorHandler'
import type { KbArticle } from '~/types/kbarticle'
import type { KbArticleSummary } from '~/types/kbarticlesummary'
import type { KbArticleQuery } from '~/types/kbarticlequery'
import type { KbCategory } from '~/types/kbcategory'
import type { PaginatedList } from '~/types/paginatedlist'
import type { PublishedAnnouncement } from '~/types/publishedannouncement'
import { kbCategorySlug } from '~/utils/kbCategory'
import { failureStatus } from '~/utils/pageLoadError'

/** How many articles the knowledgebase landing page lists under the categories. */
const LANDING_ARTICLE_COUNT = 10

/** How many articles a category page and a search result list at most. */
const CATEGORY_ARTICLE_LIMIT = 100

/** How many articles a search returns at most. */
const SEARCH_ARTICLE_LIMIT = 50

/** The page a failed article read falls back to. */
const EMPTY_ARTICLE_PAGE: PaginatedList<KbArticleSummary> = { items: [], pageNumber: 1, pageSize: 0, totalCount: 0, totalPages: 0 }

/**
 * Central store for announcements and the knowledgebase.
 *
 * @returns The content state and the actions that change it.
 */
export const useContentStore = defineStore('content', () => {
  const api = useContentApi()

  /** The announcements, newest first; empty until {@link loadAnnouncements}. */
  const announcements = ref<PublishedAnnouncement[]>([])

  /** Announcements already read in full, keyed by announcement id. */
  const announcementsById = ref<Record<string, PublishedAnnouncement>>({})

  /** The operator's own legal text, by legal page and then by `portal.legal.*` key; a page is absent until {@link loadLegalText}. */
  const legalTexts = ref<Record<string, Record<string, string>>>({})

  /** The latest published announcements for the dashboard card; empty until {@link loadRecentNews}. */
  const recentNews = ref<PublishedAnnouncement[]>([])

  /** The knowledgebase categories; empty until {@link loadKbCategories}. */
  const kbCategories = ref<KbCategory[]>([])

  /** The first page of published articles the landing page lists, in the backend order; empty until {@link loadLandingArticles}. */
  const landingArticles = ref<KbArticleSummary[]>([])

  /** Articles already read per category, keyed by category name. */
  const articlesByCategory = ref<Record<string, KbArticleSummary[]>>({})

  /** Articles already read in full, keyed by article id. */
  const articlesById = ref<Record<string, KbArticle>>({})

  /** The articles the last search found; empty before a search. */
  const searchResults = ref<KbArticleSummary[]>([])

  /** Number of content reads in flight. */
  const inFlight = ref(0)

  /** Whether any content read is in flight. */
  const loading = computed(() => inFlight.value > 0)

  /** Whether a knowledgebase search is in flight. */
  const searching = ref(false)

  /** The sentence for the last failed read; empty when there is none to show. */
  const error = ref('')

  /** The HTTP status of the last failed read (502 when it got no answer); null when the last read did not fail. */
  const errorStatus = ref<number | null>(null)

  /**
   * Runs one content read with the loading flag and the failure sentence handled once.
   *
   * @param read - The read to run.
   * @param fallback - What to return when the read fails.
   * @returns The read's result, or the fallback after a failure ({@link error} then holds the
   * sentence).
   */
  const guarded = async <T>(read: () => Promise<T>, fallback: T): Promise<T> => {
    inFlight.value += 1
    error.value = ''
    errorStatus.value = null
    try {
      return await read()
    } catch (e: unknown) {
      error.value = apiErrorMessage(e)
      errorStatus.value = failureStatus(e)
      return fallback
    } finally {
      inFlight.value -= 1
    }
  }

  /**
   * Reads the announcement list.
   *
   * @returns The announcements; empty after a failure.
   */
  const loadAnnouncements = async (): Promise<PublishedAnnouncement[]> => {
    announcements.value = await guarded(async () => (await api.fetchAnnouncements()).items, [] as PublishedAnnouncement[])
    return announcements.value
  }

  /**
   * Reads one published announcement in full and keeps it.
   *
   * @param id - Announcement id, as it appears in the route.
   * @returns The announcement; null after a failure ({@link error} holds the sentence), which
   * includes an unknown or unpublished id.
   */
  const loadAnnouncement = async (id: string): Promise<PublishedAnnouncement | null> => {
    const announcement = await guarded(() => api.fetchAnnouncement(id), null as PublishedAnnouncement | null)
    if (announcement) {
      announcementsById.value = { ...announcementsById.value, [id]: announcement }
    }
    return announcement
  }

  /**
   * Looks up an announcement {@link loadAnnouncement} already read.
   *
   * @param id - Announcement id, as it appears in the route.
   * @returns The announcement, or null when it was not read.
   */
  const announcementFor = (id: string): PublishedAnnouncement | null => announcementsById.value[id] ?? null

  /**
   * Reads the operator's own text of one legal page and keeps it.
   *
   * @param page - The legal page.
   * @returns The page's `portal.legal.*` rows that have a value; empty when the operator wrote none or the read failed, which
   * leaves the page on its built-in text.
   */
  const loadLegalText = async (page: LegalPage): Promise<Record<string, string>> => {
    const texts = await guarded(() => api.fetchLegalText(page), {} as Record<string, string>)
    legalTexts.value = { ...legalTexts.value, [page]: texts }
    return texts
  }

  /**
   * Looks up the legal text {@link loadLegalText} already read.
   *
   * @param page - The legal page.
   * @returns The page's rows by `portal.legal.*` key; empty when the page was not read.
   */
  const legalTextFor = (page: LegalPage): Record<string, string> => legalTexts.value[page] ?? {}

  /**
   * Reads the latest published announcements for the dashboard card.
   *
   * @returns The announcements; empty after a failure.
   */
  const loadRecentNews = async (): Promise<PublishedAnnouncement[]> => {
    recentNews.value = await guarded(async () => (await api.fetchRecentAnnouncements()).items, [] as PublishedAnnouncement[])
    return recentNews.value
  }

  /**
   * Reads the knowledgebase categories.
   *
   * @returns The categories; empty after a failure.
   */
  const loadKbCategories = async (): Promise<KbCategory[]> => {
    kbCategories.value = await guarded(() => api.fetchKbCategories(), [] as KbCategory[])
    return kbCategories.value
  }

  /**
   * Reads the first page of published articles the landing page lists.
   *
   * @returns The articles in the backend's order; empty after a failure.
   */
  const loadLandingArticles = async (): Promise<KbArticleSummary[]> => {
    const query: KbArticleQuery = { pageSize: LANDING_ARTICLE_COUNT }
    const list = await guarded(() => api.fetchKbArticles(query), EMPTY_ARTICLE_PAGE)
    landingArticles.value = list.items
    return landingArticles.value
  }

  /**
   * Reads the articles of one category and keeps them.
   *
   * @param categoryId - Category id, as it appears in the route.
   * @returns The articles; empty after a failure.
   */
  const loadCategoryArticles = async (categoryId: string): Promise<KbArticleSummary[]> => {
    const query: KbArticleQuery = { category: categoryId, pageSize: CATEGORY_ARTICLE_LIMIT }
    const list = await guarded(() => api.fetchKbArticles(query), EMPTY_ARTICLE_PAGE)
    articlesByCategory.value = { ...articlesByCategory.value, [categoryId]: list.items }
    return articlesByCategory.value[categoryId] ?? []
  }

  /**
   * Looks up the articles {@link loadCategoryArticles} already read.
   *
   * @param categoryId - Category id.
   * @returns The articles; empty when none were read.
   */
  const articlesFor = (categoryId: string): KbArticleSummary[] => articlesByCategory.value[categoryId] ?? []

  /**
   * Finds the category a route slug names among the categories already read. The slug is
   * compared case-insensitively against {@link kbCategorySlug} of each name.
   *
   * @param slug - Category slug from the route.
   * @returns The category, or null when no category has that slug.
   */
  const categoryBySlug = (slug: string): KbCategory | null => {
    const wanted = slug.trim().toLowerCase()
    return kbCategories.value.find(c => kbCategorySlug(c.name) === wanted) ?? null
  }

  /**
   * Reads one article in full and keeps it.
   *
   * @param id - Article id, as it appears in the route.
   * @returns The article; null after a failure ({@link error} holds the sentence).
   */
  const loadArticle = async (id: string): Promise<KbArticle | null> => {
    const article = await guarded(() => api.fetchKbArticle(id), null as KbArticle | null)
    if (article) {
      articlesById.value = { ...articlesById.value, [id]: article }
    }
    return article
  }

  /**
   * Looks up an article {@link loadArticle} already read.
   *
   * @param id - Article id.
   * @returns The article, or null when it was not read.
   */
  const articleFor = (id: string): KbArticle | null => articlesById.value[id] ?? null

  /**
   * Searches the knowledgebase.
   *
   * @param term - What to search for.
   * @returns The matching articles; empty after a failure.
   */
  const searchArticles = async (term: string): Promise<KbArticleSummary[]> => {
    searching.value = true
    try {
      const query: KbArticleQuery = { search: term, pageSize: SEARCH_ARTICLE_LIMIT }
      const list = await guarded(() => api.fetchKbArticles(query), EMPTY_ARTICLE_PAGE)
      searchResults.value = list.items
      return searchResults.value
    } finally {
      searching.value = false
    }
  }

  /** Forgets the last search, returning the landing page to its default view. */
  const clearSearch = (): void => {
    searchResults.value = []
  }

  return {
    announcements,
    announcementsById,
    recentNews,
    kbCategories,
    landingArticles,
    articlesByCategory,
    articlesById,
    searchResults,
    loading,
    searching,
    error,
    errorStatus,
    loadAnnouncements,
    loadAnnouncement,
    announcementFor,
    legalTexts,
    loadLegalText,
    legalTextFor,
    loadRecentNews,
    loadKbCategories,
    loadLandingArticles,
    loadCategoryArticles,
    articlesFor,
    categoryBySlug,
    loadArticle,
    articleFor,
    searchArticles,
    clearSearch
  }
})
