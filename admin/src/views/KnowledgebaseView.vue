<script setup lang="ts">
/**
 * Knowledgebase management: tabs for adding categories (with the category browser) and articles.
 * Every change reports its outcome (success, or the API's sentence) under the header, deleting a
 * category is confirmed first, and a refused load shows the sentence with a Retry.
 */
import { computed, onMounted, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import SupportCheckboxField from '@/components/support/SupportCheckboxField.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiRowActions, { type UiRowAction } from '@/components/ui/UiRowActions.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiSkeleton from '@/components/ui/UiSkeleton.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiTabs from '@/components/ui/UiTabs.vue'
import UiText from '@/components/ui/UiText.vue'
import UiTextarea from '@/components/ui/UiTextarea.vue'
import { useResultBanner } from '@/composables/useResultBanner'
import { useStorefrontLocaleTabs } from '@/composables/useStorefrontLocaleTabs'
import { enumValueOf } from '@/lib/enumValue'
import { MessageLanguage } from '@/locales/messages'
import { blankTranslations, collectTranslations, displayTranslation, formsFromTranslations } from '@/lib/storefrontTranslations'
import { useKnowledgebaseStore } from '@/stores/knowledgebase'
import type { KbArticle, KbArticleTranslation, KbCategory } from '@/types/kbarticle'
import { KnowledgebaseTab } from '@/types/supporttab'
import { Icon } from '@/lib/icons'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiTableDensity } from '@/utils/ui/injection'
import { UiSize } from '@/utils/ui/size'
import { UiTone } from '@/utils/ui/tone'
import type { UiTabItem } from '@/utils/ui/tabs'

const { t, locale } = useI18n()

const store = useKnowledgebaseStore()

/** Number of columns of the category table, spanned by the row of a category under edit. */
const CATEGORY_COLUMN_COUNT = 4

/** Rows of the article body field. */
const ARTICLE_CONTENT_ROWS = 12

/** Currently active tab. */
const activeTab = ref<KnowledgebaseTab>(KnowledgebaseTab.Category)

/** Tab items for the kit's tab strip. */
const tabItems = computed<UiTabItem[]>(() => [
  { value: KnowledgebaseTab.Category, label: t('shared.addCategory'), testid: 'knowledgebase-tab-category' },
  { value: KnowledgebaseTab.Article, label: t('support.knowledgebase.addArticle'), testid: 'knowledgebase-tab-article' },
])

/** New category form fields. */
const newCategory = ref({ name: '', description: '', isHidden: false })

/** Whether a category is being saved. */
const categorySaving = ref(false)

/** ID of the category waiting for delete confirmation; null when no dialog is open. */
const deleteCategoryTarget = ref<number | null>(null)

/** Whether a category deletion is in progress. */
const deletingCategory = ref(false)

/** ID of the category being edited in place; null when none. */
const editingCategoryId = ref<number | null>(null)

/** Edit form data of the category being edited. */
const editingCategory = ref({ name: '', description: '', isHidden: false })

/**
 * Creates a blank title and body.
 *
 * @returns An empty translation.
 */
const blankText = (): KbArticleTranslation => ({ title: '', content: '' })

/** The article form: the category, the published flag (new articles only) and the text per language. */
const articleForm = reactive({ categoryId: 0, isPublished: false, translations: blankTranslations(blankText) })

/** The language tabs of the article form. */
const { activeLocale, tabs: localeTabs, select: selectLocale } = useStorefrontLocaleTabs('knowledgebase-article-locale')

/** ID of the article being edited; null while the form creates a new one. */
const editingArticleId = ref<number | null>(null)

/** Whether an article is being saved. */
const articleSaving = ref(false)

/** The sentence of the last refused load, shown with Retry; null when the categories loaded. */
const loadError = ref<string | null>(null)

/** The sentence of the last refused articles load, shown with Retry; null when the articles loaded. */
const articlesLoadError = ref<string | null>(null)

/** The outcome line of the last change. */
const feedback = useResultBanner()

/** Tone of the outcome line: danger for a refusal, success otherwise. */
const feedbackTone = computed<UiTone>(() => (feedback.isError.value ? UiTone.Danger : UiTone.Success))

/**
 * Shows the outcome of one change: the success sentence, or the API's sentence of the refusal.
 *
 * @param ok - Whether the API accepted the change.
 * @param successKey - Locale key of the success sentence.
 */
const report = (ok: boolean, successKey: string): void => {
  if (ok) {
    feedback.show(t(successKey), false)
  } else {
    feedback.show(store.error ?? t('errors.noAnswer'), true)
  }
}

/**
 * Loads the categories and records whether the load was refused.
 *
 * @returns Resolves when the categories have settled.
 */
const load = async (): Promise<void> => {
  loadError.value = (await store.fetchCategories()) ? null : store.error
}

/**
 * Loads the articles and records whether the load was refused.
 *
 * @returns Resolves when the articles have settled.
 */
const loadArticles = async (): Promise<void> => {
  articlesLoadError.value = (await store.fetchArticles()) ? null : store.error
}

/** Category options for select dropdowns. */
const categoryOptions = computed(() => store.categories.map((category) => ({ value: category.id, label: category.name })))

/** Whether the article form is complete: a category and the English title and body (English is required). */
const canSaveArticle = computed<boolean>(() => {
  const english = articleForm.translations[MessageLanguage.En]
  return articleForm.categoryId !== 0 && english.title.trim() !== '' && english.content.trim() !== ''
})

/**
 * Switches tab.
 *
 * @param value - The tab key the tab strip emitted.
 */
const selectTab = (value: string): void => {
  const tab = enumValueOf(KnowledgebaseTab, value)
  if (tab !== null) {
    activeTab.value = tab
  }
}

/**
 * Creates a category from the add form and clears the form on success.
 */
const addCategory = async (): Promise<void> => {
  if (!newCategory.value.name.trim()) {
    return
  }
  feedback.clear()
  categorySaving.value = true
  const id = await store.createCategory({
    name: newCategory.value.name.trim(),
    description: newCategory.value.description.trim(),
    isHidden: newCategory.value.isHidden,
  })
  categorySaving.value = false
  report(id !== null, 'support.knowledgebase.categorySaved')
  if (id !== null) {
    newCategory.value = { name: '', description: '', isHidden: false }
  }
}

/**
 * Starts editing a category in place.
 *
 * @param category - The category to edit.
 */
const startEditCategory = (category: KbCategory): void => {
  editingCategoryId.value = category.id
  editingCategory.value = {
    name: category.name,
    description: category.description,
    isHidden: category.isHidden,
  }
}

/**
 * Saves the category being edited.
 */
const saveEditCategory = async (): Promise<void> => {
  if (editingCategoryId.value === null) {
    return
  }
  feedback.clear()
  const saved = await store.updateCategory(editingCategoryId.value, { ...editingCategory.value })
  report(saved, 'support.knowledgebase.categorySaved')
  if (saved) {
    editingCategoryId.value = null
  }
}

/**
 * Deletes the category the dialog asked about and shows the outcome.
 */
const handleDeleteCategory = async (): Promise<void> => {
  if (deleteCategoryTarget.value === null) {
    return
  }
  feedback.clear()
  deletingCategory.value = true
  const deleted = await store.deleteCategory(deleteCategoryTarget.value)
  deletingCategory.value = false
  deleteCategoryTarget.value = null
  report(deleted, 'support.knowledgebase.categoryDeleted')
}

/**
 * The row actions of a category: edit in place, and delete last (confirmed first).
 *
 * @param category - The category of the row.
 * @returns The action list passed to UiRowActions.
 */
const categoryActions = (category: KbCategory): UiRowAction[] => {
  /**
   * Opens this category's in-place edit form.
   *
   * @returns Nothing.
   */
  const onEdit = (): void => {
    startEditCategory(category)
  }
  /**
   * Asks for confirmation before this category is deleted.
   *
   * @returns Nothing.
   */
  const onDelete = (): void => {
    deleteCategoryTarget.value = category.id
  }
  return [
    { label: t('shared.edit'), testid: `knowledgebase-category-${category.id}-edit`, onSelect: onEdit },
    { label: t('shared.delete'), testid: `knowledgebase-category-${category.id}-delete`, onSelect: onDelete, danger: true },
  ]
}

/** Closes the delete confirmation; ignored while the delete runs. */
const dismissDeleteCategory = (): void => {
  if (!deletingCategory.value) {
    deleteCategoryTarget.value = null
  }
}

/** The label of the save button: saving while a request runs, otherwise create or save changes. */
const articleSaveLabel = computed<string>(() => {
  if (articleSaving.value) {
    return t('shared.saving')
  }
  return editingArticleId.value === null ? t('support.knowledgebase.saveArticle') : t('shared.saveChanges')
})

/**
 * Collects the translations worth sending: the trimmed texts of the languages that have any.
 *
 * @returns The `translations` object of the payload.
 */
const collectArticleTranslations = () =>
  collectTranslations(
    articleForm.translations,
    (text) => text.title.trim() !== '' || text.content.trim() !== '',
    (text) => ({ title: text.title.trim(), content: text.content.trim() }),
  )

/** Empties the article form and leaves edit mode. */
const resetArticleForm = (): void => {
  articleForm.categoryId = 0
  articleForm.isPublished = false
  articleForm.translations = blankTranslations(blankText)
  editingArticleId.value = null
  activeLocale.value = MessageLanguage.En
}

/**
 * Saves the article form: creates a new article, or replaces the translations of the one being edited.
 * The form is cleared and the list and the category counts are refreshed on success.
 */
const saveArticle = async (): Promise<void> => {
  if (!canSaveArticle.value) {
    return
  }
  feedback.clear()
  articleSaving.value = true
  const translations = collectArticleTranslations()
  const editingId = editingArticleId.value
  const saved =
    editingId === null
      ? (await store.createArticle({ categoryId: articleForm.categoryId, translations, isPublished: articleForm.isPublished })) !== null
      : await store.updateArticle(editingId, { categoryId: articleForm.categoryId, translations })
  articleSaving.value = false
  report(saved, 'support.knowledgebase.articleSaved')
  if (saved) {
    resetArticleForm()
    await load()
  }
}

/**
 * Loads an article into the form to edit its category and translations.
 *
 * @param article - The article to edit.
 */
const startEditArticle = (article: KbArticle): void => {
  feedback.clear()
  editingArticleId.value = article.id
  articleForm.categoryId = article.categoryId
  articleForm.isPublished = article.isPublished
  articleForm.translations = formsFromTranslations(article.translations, blankText)
  activeLocale.value = MessageLanguage.En
}

/**
 * The title of an article in the language of the admin panel, else in the first language it has.
 *
 * @param article - The article of the row.
 * @returns The title, or the slug when the article somehow has no text.
 */
const titleOf = (article: KbArticle): string => {
  const own = enumValueOf(MessageLanguage, locale.value)
  return (own !== null ? article.translations[own]?.title : undefined) ?? displayTranslation(article.translations)?.title ?? article.slug
}

/**
 * The row actions of an article: edit.
 *
 * @param article - The article of the row.
 * @returns The action list passed to UiRowActions.
 */
const articleActions = (article: KbArticle): UiRowAction[] => {
  /**
   * Loads this article into the form.
   *
   * @returns Nothing.
   */
  const onEdit = (): void => {
    startEditArticle(article)
  }
  return [{ label: t('shared.edit'), testid: `knowledgebase-article-${article.id}-edit`, onSelect: onEdit }]
}

/** Loads the categories and the articles. */
onMounted(() => {
  void load()
  void loadArticles()
})
</script>

<template>
  <UiStack>
    <UiPageHeader
      :title="t('support.knowledgebase.knowledgebase')"
      :subtitle="t('support.knowledgebase.manageKnowledgebaseArticlesAndCategories')"
    />

    <UiStatusMessage :message="feedback.message.value" :tone="feedbackTone" data-testid="knowledgebase-feedback" />

    <UiErrorState
      v-if="loadError !== null"
      :message="loadError"
      :loading="store.loading"
      testid="knowledgebase-error"
      @retry="load"
    />

    <UiTabs :items="tabItems" :model-value="activeTab" @update:model-value="selectTab" />

    <template v-if="activeTab === KnowledgebaseTab.Category">
      <UiCard padding="md">
        <UiStack>
          <UiField :label="t('shared.categoryName')" :error="store.fieldErrors.name">
            <UiInput
              v-model="newCategory.name"
              :placeholder="t('shared.enterCategoryName')"
              data-testid="knowledgebase-category-name"
            />
          </UiField>
          <SupportCheckboxField
            v-model="newCategory.isHidden"
            :label="t('support.knowledgebase.checkToHide')"
            testid="knowledgebase-category-hidden"
          />
          <UiField :label="t('shared.description')">
            <UiInput
              v-model="newCategory.description"
              :placeholder="t('support.knowledgebase.categoryDescription')"
              data-testid="knowledgebase-category-description"
            />
          </UiField>
          <UiStack direction="row">
            <UiButton
              :variant="UiButtonVariant.Primary"
              :loading="categorySaving"
              :disabled="!newCategory.name.trim()"
              data-testid="knowledgebase-category-add"
              @click="addCategory"
            >
              {{ t('shared.addCategory') }}
            </UiButton>
          </UiStack>
        </UiStack>
      </UiCard>

      <UiStack direction="row" gap="xs" align="center">
        <UiText tone="muted" size="md">{{ t('shared.youAreHere') }}</UiText>
        <UiText tone="secondary" size="md" class="font-medium">{{ t('support.knowledgebase.knowledgebaseHome') }}</UiText>
      </UiStack>

      <UiSkeleton v-if="store.loading" :lines="3" data-testid="knowledgebase-loading" />

      <template v-else>
        <UiHeading :level="2" variant="card">{{ t('support.knowledgebase.browseByCategory') }}</UiHeading>

        <UiCard v-if="store.categories.length > 0" data-testid="knowledgebase-categories">
          <UiTable :density="UiTableDensity.Comfortable">
            <UiTableHead>
              <UiTableRow>
                <UiTableHeader>{{ t('shared.name') }}</UiTableHeader>
                <UiTableHeader>{{ t('shared.description') }}</UiTableHeader>
                <UiTableHeader>{{ t('shared.status') }}</UiTableHeader>
                <UiTableHeader actions>{{ t('shared.actions') }}</UiTableHeader>
              </UiTableRow>
            </UiTableHead>
            <UiTableBody divided>
              <UiTableRow
                v-for="category in store.categories"
                :key="category.id"
                hoverable
                :data-testid="`knowledgebase-category-${category.id}`"
              >
                <!-- The category under edit spans the whole row with its in-place form -->
                <UiTableCell v-if="editingCategoryId === category.id" :colspan="CATEGORY_COLUMN_COUNT">
                  <UiStack gap="sm">
                    <UiInput
                      v-model="editingCategory.name"
                      :placeholder="t('shared.categoryName2')"
                      data-testid="knowledgebase-edit-name"
                    />
                    <UiInput
                      v-model="editingCategory.description"
                      :placeholder="t('shared.description')"
                      data-testid="knowledgebase-edit-description"
                    />
                    <SupportCheckboxField
                      v-model="editingCategory.isHidden"
                      :label="t('shared.hidden')"
                      testid="knowledgebase-edit-hidden"
                    />
                    <UiStack direction="row" gap="sm">
                      <UiButton
                        :variant="UiButtonVariant.Primary"
                        :size="UiSize.Sm"
                        data-testid="knowledgebase-edit-save"
                        @click="saveEditCategory"
                      >
                        {{ t('shared.save') }}
                      </UiButton>
                      <UiButton
                        :variant="UiButtonVariant.Secondary"
                        :size="UiSize.Sm"
                        data-testid="knowledgebase-edit-cancel"
                        @click="editingCategoryId = null"
                      >
                        {{ t('shared.cancel') }}
                      </UiButton>
                    </UiStack>
                  </UiStack>
                </UiTableCell>

                <template v-else>
                  <UiTableCell>
                    <UiStack direction="row" gap="sm" align="center">
                      <UiIcon :markup="Icon.Folder" />
                      <UiText tone="primary" size="md">{{ category.name }}</UiText>
                      <UiText tone="muted" size="sm">({{ category.articleCount }})</UiText>
                    </UiStack>
                  </UiTableCell>
                  <UiTableCell muted>{{ category.description || '—' }}</UiTableCell>
                  <UiTableCell>
                    <UiBadge v-if="category.isHidden" :tone="UiTone.Warning" small>{{ t('shared.hidden') }}</UiBadge>
                  </UiTableCell>
                  <UiTableCell actions>
                    <UiRowActions :actions="categoryActions(category)" :testid="`knowledgebase-category-${category.id}-actions`" />
                  </UiTableCell>
                </template>
              </UiTableRow>
            </UiTableBody>
          </UiTable>
        </UiCard>

        <UiCard v-else>
          <UiEmptyState
            :title="t('support.knowledgebase.noCategoriesFound')"
            :description="t('support.knowledgebase.createYourFirstCategoryAbove')"
            data-testid="knowledgebase-categories-empty"
          />
        </UiCard>

        <UiHeading :level="2" variant="card">{{ t('support.knowledgebase.browseByTag') }}</UiHeading>
        <UiCard>
          <UiEmptyState
            :title="t('support.knowledgebase.noTagsFound')"
            :description="t('support.knowledgebase.tagsWillAppearHereWhen')"
          />
        </UiCard>
      </template>
    </template>

    <template v-if="activeTab === KnowledgebaseTab.Article">
      <UiCard padding="md" data-testid="knowledgebase-article-form">
        <UiStack>
          <UiHeading v-if="editingArticleId !== null" :level="2" variant="card">{{ t('support.knowledgebase.editArticle') }}</UiHeading>
          <UiField :label="t('shared.category')" :error="store.fieldErrors.categoryId">
            <UiSelect
              v-model="articleForm.categoryId"
              :options="categoryOptions"
              :placeholder="t('shared.selectCategory')"
              data-testid="knowledgebase-article-category"
            />
          </UiField>
          <UiTabs :items="localeTabs" :model-value="activeLocale" @update:model-value="selectLocale" />
          <UiText tone="muted" size="sm">{{ t('support.knowledgebase.englishRequired') }}</UiText>
          <UiField :label="t('shared.title')" :error="store.fieldErrors.translations">
            <UiInput
              v-model="articleForm.translations[activeLocale].title"
              :placeholder="t('support.knowledgebase.articleTitle')"
              data-testid="knowledgebase-article-title"
            />
          </UiField>
          <UiField :label="t('support.knowledgebase.content')">
            <UiTextarea
              v-model="articleForm.translations[activeLocale].content"
              :rows="ARTICLE_CONTENT_ROWS"
              :placeholder="t('support.knowledgebase.articleContent')"
              data-testid="knowledgebase-article-content"
            />
          </UiField>
          <SupportCheckboxField
            v-if="editingArticleId === null"
            v-model="articleForm.isPublished"
            :label="t('shared.published')"
            testid="knowledgebase-article-published"
          />
          <UiStack direction="row" gap="sm">
            <UiButton
              :variant="UiButtonVariant.Primary"
              :loading="articleSaving"
              :disabled="!canSaveArticle"
              data-testid="knowledgebase-article-save"
              @click="saveArticle"
            >
              {{ articleSaveLabel }}
            </UiButton>
            <UiButton
              v-if="editingArticleId !== null"
              :variant="UiButtonVariant.Secondary"
              data-testid="knowledgebase-article-cancel"
              @click="resetArticleForm"
            >
              {{ t('shared.cancel') }}
            </UiButton>
          </UiStack>
        </UiStack>
      </UiCard>

      <UiHeading :level="2" variant="card">{{ t('support.knowledgebase.articles') }}</UiHeading>

      <UiErrorState
        v-if="articlesLoadError !== null"
        :message="articlesLoadError"
        :loading="store.articlesLoading"
        testid="knowledgebase-articles-error"
        @retry="loadArticles"
      />

      <UiSkeleton v-else-if="store.articlesLoading" :lines="3" data-testid="knowledgebase-articles-loading" />

      <UiCard v-else-if="store.articles.length > 0" data-testid="knowledgebase-articles">
        <UiTable :density="UiTableDensity.Comfortable">
          <UiTableHead>
            <UiTableRow>
              <UiTableHeader>{{ t('shared.title') }}</UiTableHeader>
              <UiTableHeader>{{ t('shared.category') }}</UiTableHeader>
              <UiTableHeader>{{ t('shared.status') }}</UiTableHeader>
              <UiTableHeader actions>{{ t('shared.actions') }}</UiTableHeader>
            </UiTableRow>
          </UiTableHead>
          <UiTableBody divided>
            <UiTableRow
              v-for="article in store.articles"
              :key="article.id"
              hoverable
              :data-testid="`knowledgebase-article-${article.id}`"
            >
              <UiTableCell>
                <UiStack gap="xs">
                  <UiText tone="primary" size="md">{{ titleOf(article) }}</UiText>
                  <UiText tone="muted" size="sm">{{ article.slug }}</UiText>
                </UiStack>
              </UiTableCell>
              <UiTableCell muted>{{ article.categoryName }}</UiTableCell>
              <UiTableCell>
                <UiBadge :tone="article.isPublished ? UiTone.Success : UiTone.Warning" small>
                  {{ article.isPublished ? t('shared.published') : t('support.knowledgebase.draft') }}
                </UiBadge>
              </UiTableCell>
              <UiTableCell actions>
                <UiRowActions :actions="articleActions(article)" :testid="`knowledgebase-article-${article.id}-actions`" />
              </UiTableCell>
            </UiTableRow>
          </UiTableBody>
        </UiTable>
      </UiCard>

      <UiCard v-else>
        <UiEmptyState
          :title="t('support.knowledgebase.noArticlesFound')"
          :description="t('support.knowledgebase.createYourFirstArticleAbove')"
          data-testid="knowledgebase-articles-empty"
        />
      </UiCard>
    </template>

    <UiDialog
      variant="confirm"
      :open="deleteCategoryTarget !== null"
      :title="t('support.knowledgebase.deleteCategory')"
      :message="t('support.knowledgebase.areYouSureYouWant')"
      :confirm-label="t('shared.delete')"
      :loading-label="t('shared.deleting')"
      :loading="deletingCategory"
      tone="danger"
      @confirm="handleDeleteCategory"
      @close="dismissDeleteCategory"
    />
  </UiStack>
</template>
