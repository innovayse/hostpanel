<script setup lang="ts">
/**
 * Knowledgebase management view -- provides tabs for managing categories
 * and articles, with a category grid and tag browser.
 */
import { ref, computed, onMounted } from 'vue'
import { useApi } from '../../../composables/useApi'
import AppSelect from '../../../components/AppSelect.vue'
import AppCheckbox from '../../../components/AppCheckbox.vue'
import ConfirmModal from '../../../components/ConfirmModal.vue'

/** Shape of a knowledgebase category. */
interface KbCategory {
  /** Unique category identifier. */
  id: number
  /** Category display name. */
  name: string
  /** Category description text. */
  description: string
  /** Whether the category is hidden from clients. */
  hidden: boolean
  /** Number of articles in this category. */
  articleCount: number
}

/** Shape of a knowledgebase article. */
interface KbArticle {
  /** Unique article identifier. */
  id: number
  /** Article title. */
  title: string
  /** ID of the category this article belongs to. */
  categoryId: number
  /** Category name for display. */
  categoryName: string
  /** Article body content. */
  content: string
  /** Whether the article is published. */
  published: boolean
}

const { request } = useApi()

/** Currently active tab. */
const activeTab = ref<'category' | 'article'>('category')

/** Tab definitions for rendering. */
const tabs = [
  { key: 'category' as const, label: 'Add Category' },
  { key: 'article' as const, label: 'Add Article' },
]

/* ---- Category state ---- */

/** All loaded categories. */
const categories = ref<KbCategory[]>([])

/** New category form fields. */
const newCategory = ref({ name: '', description: '', hidden: false })

/** Whether categories are loading. */
const categoriesLoading = ref(false)

/** Whether a category is being saved. */
const categorySaving = ref(false)

/** ID of category pending deletion. */
const deleteCategoryTarget = ref<number | null>(null)

/** Whether a category deletion is in progress. */
const deletingCategory = ref(false)

/** ID of category being edited. */
const editingCategoryId = ref<number | null>(null)

/** Inline edit form data for a category. */
const editingCategory = ref({ name: '', description: '', hidden: false })

/* ---- Article state ---- */

/** New article form fields. */
const newArticle = ref({ title: '', categoryId: 0, content: '', published: false })

/** Whether an article is being saved. */
const articleSaving = ref(false)

/** Error message from the last operation. */
const error = ref<string | null>(null)

/** Category options for select dropdowns. */
const categoryOptions = computed(() =>
  categories.value.map(c => ({ value: c.id, label: c.name }))
)

/* ---- Category methods ---- */

/**
 * Fetches all knowledgebase categories.
 */
async function fetchCategories(): Promise<void> {
  categoriesLoading.value = true
  try {
    categories.value = await request<KbCategory[]>('/admin/knowledgebase/categories')
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to load categories'
  } finally {
    categoriesLoading.value = false
  }
}

/**
 * Creates a new knowledgebase category.
 */
async function addCategory(): Promise<void> {
  if (!newCategory.value.name.trim()) return
  categorySaving.value = true
  error.value = null
  try {
    await request('/admin/knowledgebase/categories', {
      method: 'POST',
      body: JSON.stringify({
        name: newCategory.value.name.trim(),
        description: newCategory.value.description.trim(),
        hidden: newCategory.value.hidden,
      }),
    })
    newCategory.value = { name: '', description: '', hidden: false }
    await fetchCategories()
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to create category'
  } finally {
    categorySaving.value = false
  }
}

/**
 * Enters edit mode for a category.
 *
 * @param cat - The category to edit.
 */
function startEditCategory(cat: KbCategory): void {
  editingCategoryId.value = cat.id
  editingCategory.value = {
    name: cat.name,
    description: cat.description,
    hidden: cat.hidden,
  }
}

/**
 * Saves the category edit.
 */
async function saveEditCategory(): Promise<void> {
  if (editingCategoryId.value === null) return
  error.value = null
  try {
    await request(`/admin/knowledgebase/categories/${editingCategoryId.value}`, {
      method: 'PUT',
      body: JSON.stringify(editingCategory.value),
    })
    editingCategoryId.value = null
    await fetchCategories()
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to update category'
  }
}

/**
 * Cancels category editing.
 */
function cancelEditCategory(): void {
  editingCategoryId.value = null
}

/**
 * Opens the delete confirmation modal for a category.
 *
 * @param id - Category ID to delete.
 */
function confirmDeleteCategory(id: number): void {
  deleteCategoryTarget.value = id
}

/**
 * Executes the category deletion.
 */
async function handleDeleteCategory(): Promise<void> {
  if (deleteCategoryTarget.value === null) return
  deletingCategory.value = true
  try {
    await request(`/admin/knowledgebase/categories/${deleteCategoryTarget.value}`, { method: 'DELETE' })
    deleteCategoryTarget.value = null
    await fetchCategories()
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to delete category'
  } finally {
    deletingCategory.value = false
  }
}

/* ---- Article methods ---- */

/**
 * Creates a new knowledgebase article.
 */
async function addArticle(): Promise<void> {
  if (!newArticle.value.title.trim() || !newArticle.value.categoryId || !newArticle.value.content.trim()) return
  articleSaving.value = true
  error.value = null
  try {
    await request('/admin/knowledgebase', {
      method: 'POST',
      body: JSON.stringify({
        title: newArticle.value.title.trim(),
        categoryId: newArticle.value.categoryId,
        content: newArticle.value.content.trim(),
        published: newArticle.value.published,
      }),
    })
    newArticle.value = { title: '', categoryId: 0, content: '', published: false }
    await fetchCategories()
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to create article'
  } finally {
    articleSaving.value = false
  }
}

onMounted(() => {
  fetchCategories()
})
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full">

    <!-- Header -->
    <div class="mb-5">
      <h1 class="font-display font-bold text-[1.25rem] text-text-primary leading-none">Knowledgebase</h1>
      <p class="text-[0.78rem] text-text-secondary mt-1">Manage knowledgebase articles and categories</p>
    </div>

    <!-- Error -->
    <div v-if="error" class="text-sm text-status-red bg-status-red/8 border border-status-red/20 rounded-xl p-4 mb-4">
      {{ error }}
    </div>

    <!-- Tab strip -->
    <div class="flex flex-wrap gap-2 mb-5">
      <button
        v-for="tab in tabs"
        :key="tab.key"
        type="button"
        class="px-3.5 py-1.5 text-[0.78rem] font-medium border rounded-[10px] transition-colors"
        :class="activeTab === tab.key
          ? 'bg-primary-500/10 text-primary-400 border-primary-500/20'
          : 'bg-white/[0.04] text-text-secondary border-border hover:bg-white/[0.06]'"
        @click="activeTab = tab.key"
      >
        {{ tab.label }}
      </button>
    </div>

    <!-- ===== Add Category Tab ===== -->
    <div v-if="activeTab === 'category'">

      <!-- Add category form -->
      <div class="bg-surface-card border border-border rounded-2xl p-5 mb-5 space-y-4">

        <!-- Name -->
        <div>
          <label class="block text-[0.75rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">
            Category Name
          </label>
          <input
            v-model="newCategory.name"
            type="text"
            placeholder="Enter category name"
            class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.82rem] text-text-primary placeholder:text-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
          />
        </div>

        <!-- Hidden checkbox -->
        <div class="flex items-center gap-3">
          <AppCheckbox v-model="newCategory.hidden" />
          <span class="text-[0.82rem] text-text-secondary cursor-pointer" @click="newCategory.hidden = !newCategory.hidden">
            Check to Hide
          </span>
        </div>

        <!-- Description -->
        <div>
          <label class="block text-[0.75rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">
            Description
          </label>
          <input
            v-model="newCategory.description"
            type="text"
            placeholder="Category description"
            class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.82rem] text-text-primary placeholder:text-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
          />
        </div>

        <button
          type="button"
          :disabled="categorySaving || !newCategory.name.trim()"
          class="gradient-brand px-5 py-2 text-[0.84rem] font-semibold text-white rounded-[10px] transition-opacity disabled:opacity-50"
          @click="addCategory"
        >
          Add Category
        </button>
      </div>

      <!-- Breadcrumb -->
      <div class="text-[0.78rem] text-text-muted mb-4">
        You are here: <span class="text-text-secondary font-medium">Knowledgebase Home</span>
      </div>

      <!-- Loading -->
      <div v-if="categoriesLoading" class="flex items-center gap-3 text-text-secondary text-sm">
        <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
        Loading categories...
      </div>

      <!-- Browse by Category -->
      <template v-else>
        <h2 class="text-[0.88rem] font-semibold text-text-primary mb-3">Browse by Category</h2>

        <div v-if="categories.length > 0" class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4 mb-8">
          <div
            v-for="cat in categories"
            :key="cat.id"
            class="bg-surface-card border border-border rounded-2xl p-5 hover:bg-white/[0.02] transition-colors"
          >
            <!-- Edit mode -->
            <div v-if="editingCategoryId === cat.id" class="space-y-3">
              <input
                v-model="editingCategory.name"
                type="text"
                class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2 text-[0.82rem] text-text-primary focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
              />
              <input
                v-model="editingCategory.description"
                type="text"
                placeholder="Description"
                class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2 text-[0.82rem] text-text-primary placeholder:text-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
              />
              <div class="flex items-center gap-3">
                <AppCheckbox v-model="editingCategory.hidden" />
                <span class="text-[0.78rem] text-text-secondary cursor-pointer" @click="editingCategory.hidden = !editingCategory.hidden">Hidden</span>
              </div>
              <div class="flex items-center gap-2">
                <button
                  type="button"
                  class="gradient-brand px-3 py-1.5 text-[0.78rem] font-semibold text-white rounded-[10px] transition-opacity"
                  @click="saveEditCategory"
                >
                  Save
                </button>
                <button
                  type="button"
                  class="px-3 py-1.5 text-[0.78rem] font-medium text-text-secondary bg-white/[0.04] border border-border rounded-[10px] hover:bg-white/[0.06] transition-colors"
                  @click="cancelEditCategory"
                >
                  Cancel
                </button>
              </div>
            </div>

            <!-- Display mode -->
            <div v-else>
              <div class="flex items-start justify-between gap-3 mb-2">
                <div class="flex items-center gap-2.5">
                  <svg class="w-5 h-5 text-primary-400 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                    <path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z" />
                  </svg>
                  <span class="text-[0.88rem] font-medium text-text-primary">
                    {{ cat.name }}
                    <span class="text-text-muted text-[0.78rem] font-normal">({{ cat.articleCount }})</span>
                  </span>
                </div>
                <div class="flex items-center gap-1.5 shrink-0">
                  <button
                    type="button"
                    class="text-text-muted hover:text-primary-400 transition-colors p-0.5"
                    @click="startEditCategory(cat)"
                  >
                    <svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                      <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7" />
                      <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z" />
                    </svg>
                  </button>
                  <button
                    type="button"
                    class="text-text-muted hover:text-status-red transition-colors p-0.5"
                    @click="confirmDeleteCategory(cat.id)"
                  >
                    <svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                      <polyline points="3 6 5 6 21 6" />
                      <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
                    </svg>
                  </button>
                </div>
              </div>
              <p v-if="cat.description" class="text-[0.78rem] text-text-muted ml-7.5">{{ cat.description }}</p>
              <span
                v-if="cat.hidden"
                class="inline-block mt-2 ml-7.5 px-2 py-0.5 rounded-full text-[0.68rem] font-medium text-status-yellow bg-status-yellow/10 border border-status-yellow/20"
              >
                Hidden
              </span>
            </div>
          </div>
        </div>

        <!-- Empty categories -->
        <div v-else class="bg-surface-card border border-border rounded-2xl flex flex-col items-center justify-center py-12 gap-3 mb-8">
          <p class="text-[0.875rem] font-medium text-text-secondary">No Categories Found</p>
          <p class="text-[0.78rem] text-text-muted">Create your first category above.</p>
        </div>

        <!-- Browse by Tag -->
        <h2 class="text-[0.88rem] font-semibold text-text-primary mb-3">Browse by Tag</h2>
        <div class="bg-surface-card border border-border rounded-2xl flex flex-col items-center justify-center py-12 gap-3">
          <p class="text-[0.875rem] font-medium text-text-secondary">No Tags Found</p>
          <p class="text-[0.78rem] text-text-muted">Tags will appear here when articles are tagged.</p>
        </div>
      </template>
    </div>

    <!-- ===== Add Article Tab ===== -->
    <div v-if="activeTab === 'article'">

      <div class="bg-surface-card border border-border rounded-2xl p-5 space-y-4">

        <!-- Title -->
        <div>
          <label class="block text-[0.75rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Title</label>
          <input
            v-model="newArticle.title"
            type="text"
            placeholder="Article title"
            class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.82rem] text-text-primary placeholder:text-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
          />
        </div>

        <!-- Category -->
        <div>
          <label class="block text-[0.75rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Category</label>
          <AppSelect
            v-model="newArticle.categoryId"
            :options="categoryOptions"
            placeholder="Select category"
          />
        </div>

        <!-- Content -->
        <div>
          <label class="block text-[0.75rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Content</label>
          <textarea
            v-model="newArticle.content"
            placeholder="Article content..."
            class="w-full min-h-[300px] bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.82rem] text-text-primary placeholder:text-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors resize-y"
          />
        </div>

        <!-- Published -->
        <div class="flex items-center gap-3">
          <AppCheckbox v-model="newArticle.published" />
          <span class="text-[0.82rem] text-text-secondary cursor-pointer" @click="newArticle.published = !newArticle.published">Published</span>
        </div>

        <button
          type="button"
          :disabled="articleSaving || !newArticle.title.trim() || !newArticle.categoryId || !newArticle.content.trim()"
          class="gradient-brand px-5 py-2 text-[0.84rem] font-semibold text-white rounded-[10px] transition-opacity disabled:opacity-50"
          @click="addArticle"
        >
          <span v-if="articleSaving" class="flex items-center gap-2">
            <span class="w-3.5 h-3.5 rounded-full border-2 border-white/20 border-t-white animate-spin" />
            Saving...
          </span>
          <span v-else>Save Article</span>
        </button>
      </div>
    </div>

    <!-- Delete Category Modal -->
    <ConfirmModal
      v-if="deleteCategoryTarget !== null"
      title="Delete Category"
      message="Are you sure you want to delete this category? All articles in this category may be affected."
      confirm-label="Delete"
      loading-label="Deleting..."
      :loading="deletingCategory"
      variant="danger"
      @confirm="handleDeleteCategory"
      @close="deleteCategoryTarget = null"
    />

  </div>
</template>
