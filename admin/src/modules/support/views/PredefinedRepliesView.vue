<script setup lang="ts">
/**
 * Predefined replies management view -- provides tabs for managing categories,
 * adding predefined replies, and searching/filtering existing replies.
 */
import { ref, computed, onMounted } from 'vue'
import { useApi } from '../../../composables/useApi'
import AppSelect from '../../../components/AppSelect.vue'
import ConfirmModal from '../../../components/ConfirmModal.vue'

/** Shape of a predefined reply category. */
interface ReplyCategory {
  /** Unique category identifier. */
  id: number
  /** Category display name. */
  name: string
}

/** Shape of a predefined reply. */
interface PredefinedReply {
  /** Unique reply identifier. */
  id: number
  /** Reply name/title. */
  name: string
  /** Category ID this reply belongs to. */
  categoryId: number
  /** Category name for display. */
  categoryName: string
  /** Reply content/body text. */
  content: string
}

const { request } = useApi()

/** Currently active tab. */
const activeTab = ref<'category' | 'reply' | 'search'>('category')

/** Tab definitions for rendering. */
const tabs = [
  { key: 'category' as const, label: 'Add Category' },
  { key: 'reply' as const, label: 'Add Predefined Reply' },
  { key: 'search' as const, label: 'Search/Filter' },
]

/* ---- Category state ---- */

/** All loaded categories. */
const categories = ref<ReplyCategory[]>([])

/** New category name input. */
const newCategoryName = ref('')

/** Whether categories are loading. */
const categoriesLoading = ref(false)

/** Whether a category is being saved. */
const categorySaving = ref(false)

/** ID of category being edited inline (null = none). */
const editingCategoryId = ref<number | null>(null)

/** Inline edit name value. */
const editingCategoryName = ref('')

/** ID of category pending deletion. */
const deleteCategoryTarget = ref<number | null>(null)

/** Whether a category deletion is in progress. */
const deletingCategory = ref(false)

/* ---- Reply state ---- */

/** All loaded predefined replies. */
const replies = ref<PredefinedReply[]>([])

/** New reply name input. */
const newReplyName = ref('')

/** New reply category selection. */
const newReplyCategoryId = ref<number>(0)

/** New reply content textarea. */
const newReplyContent = ref('')

/** Whether replies are loading. */
const repliesLoading = ref(false)

/** Whether a reply is being saved. */
const replySaving = ref(false)

/** ID of reply pending deletion. */
const deleteReplyTarget = ref<number | null>(null)

/** Whether a reply deletion is in progress. */
const deletingReply = ref(false)

/** ID of reply being edited (null = none). */
const editingReplyId = ref<number | null>(null)

/** Inline edit fields for a reply. */
const editingReply = ref({ name: '', categoryId: 0, content: '' })

/* ---- Search state ---- */

/** Search query string. */
const searchQuery = ref('')

/** Search results. */
const searchResults = ref<PredefinedReply[]>([])

/** Whether a search is in progress. */
const searching = ref(false)

/** Category options for select dropdowns. */
const categoryOptions = computed(() =>
  categories.value.map(c => ({ value: c.id, label: c.name }))
)

/** Error message from the last operation. */
const error = ref<string | null>(null)

/* ---- Category methods ---- */

/**
 * Fetches all predefined reply categories.
 */
async function fetchCategories(): Promise<void> {
  categoriesLoading.value = true
  try {
    categories.value = await request<ReplyCategory[]>('/predefined-replies/categories')
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to load categories'
  } finally {
    categoriesLoading.value = false
  }
}

/**
 * Creates a new category.
 */
async function addCategory(): Promise<void> {
  if (!newCategoryName.value.trim()) return
  categorySaving.value = true
  error.value = null
  try {
    await request('/predefined-replies/categories', {
      method: 'POST',
      body: JSON.stringify({ name: newCategoryName.value.trim() }),
    })
    newCategoryName.value = ''
    await fetchCategories()
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to create category'
  } finally {
    categorySaving.value = false
  }
}

/**
 * Enters inline edit mode for a category.
 *
 * @param cat - The category to edit.
 */
function startEditCategory(cat: ReplyCategory): void {
  editingCategoryId.value = cat.id
  editingCategoryName.value = cat.name
}

/**
 * Saves the inline category edit.
 */
async function saveEditCategory(): Promise<void> {
  if (editingCategoryId.value === null || !editingCategoryName.value.trim()) return
  error.value = null
  try {
    await request(`/predefined-replies/categories/${editingCategoryId.value}`, {
      method: 'PUT',
      body: JSON.stringify({ name: editingCategoryName.value.trim() }),
    })
    editingCategoryId.value = null
    editingCategoryName.value = ''
    await fetchCategories()
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to update category'
  }
}

/**
 * Cancels inline category editing.
 */
function cancelEditCategory(): void {
  editingCategoryId.value = null
  editingCategoryName.value = ''
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
    await request(`/predefined-replies/categories/${deleteCategoryTarget.value}`, { method: 'DELETE' })
    deleteCategoryTarget.value = null
    await fetchCategories()
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to delete category'
  } finally {
    deletingCategory.value = false
  }
}

/* ---- Reply methods ---- */

/**
 * Fetches all predefined replies.
 */
async function fetchReplies(): Promise<void> {
  repliesLoading.value = true
  try {
    replies.value = await request<PredefinedReply[]>('/predefined-replies')
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to load replies'
  } finally {
    repliesLoading.value = false
  }
}

/**
 * Creates a new predefined reply.
 */
async function addReply(): Promise<void> {
  if (!newReplyName.value.trim() || !newReplyCategoryId.value || !newReplyContent.value.trim()) return
  replySaving.value = true
  error.value = null
  try {
    await request('/predefined-replies', {
      method: 'POST',
      body: JSON.stringify({
        name: newReplyName.value.trim(),
        categoryId: newReplyCategoryId.value,
        content: newReplyContent.value.trim(),
      }),
    })
    newReplyName.value = ''
    newReplyCategoryId.value = 0
    newReplyContent.value = ''
    await fetchReplies()
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to create reply'
  } finally {
    replySaving.value = false
  }
}

/**
 * Enters edit mode for a reply.
 *
 * @param reply - The reply to edit.
 */
function startEditReply(reply: PredefinedReply): void {
  editingReplyId.value = reply.id
  editingReply.value = {
    name: reply.name,
    categoryId: reply.categoryId,
    content: reply.content,
  }
}

/**
 * Saves the reply edit.
 */
async function saveEditReply(): Promise<void> {
  if (editingReplyId.value === null) return
  error.value = null
  try {
    await request(`/predefined-replies/${editingReplyId.value}`, {
      method: 'PUT',
      body: JSON.stringify(editingReply.value),
    })
    editingReplyId.value = null
    await fetchReplies()
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to update reply'
  }
}

/**
 * Cancels reply editing.
 */
function cancelEditReply(): void {
  editingReplyId.value = null
}

/**
 * Opens the delete confirmation modal for a reply.
 *
 * @param id - Reply ID to delete.
 */
function confirmDeleteReply(id: number): void {
  deleteReplyTarget.value = id
}

/**
 * Executes the reply deletion.
 */
async function handleDeleteReply(): Promise<void> {
  if (deleteReplyTarget.value === null) return
  deletingReply.value = true
  try {
    await request(`/predefined-replies/${deleteReplyTarget.value}`, { method: 'DELETE' })
    deleteReplyTarget.value = null
    await fetchReplies()
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to delete reply'
  } finally {
    deletingReply.value = false
  }
}

/* ---- Search methods ---- */

/** Debounce timer for search. */
let searchTimer: ReturnType<typeof setTimeout> | undefined

/**
 * Performs a debounced search for predefined replies.
 */
function handleSearch(): void {
  clearTimeout(searchTimer)
  searchTimer = setTimeout(async () => {
    if (!searchQuery.value.trim()) {
      searchResults.value = []
      return
    }
    searching.value = true
    try {
      searchResults.value = await request<PredefinedReply[]>(
        `/predefined-replies/search?q=${encodeURIComponent(searchQuery.value.trim())}`
      )
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'Search failed'
    } finally {
      searching.value = false
    }
  }, 300)
}

onMounted(async () => {
  await Promise.all([fetchCategories(), fetchReplies()])
})
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full">

    <!-- Header -->
    <div class="mb-5">
      <h1 class="font-display font-bold text-[1.25rem] text-text-primary leading-none">Predefined Replies</h1>
      <p class="text-[0.78rem] text-text-secondary mt-1">Manage predefined ticket reply templates</p>
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

      <!-- Add form -->
      <div class="bg-surface-card border border-border rounded-2xl p-5 mb-5">
        <div class="flex items-end gap-3">
          <div class="flex-1">
            <label class="block text-[0.75rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">
              Category Name
            </label>
            <input
              v-model="newCategoryName"
              type="text"
              placeholder="Enter category name"
              class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.82rem] text-text-primary placeholder:text-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
              @keyup.enter="addCategory"
            />
          </div>
          <button
            type="button"
            :disabled="categorySaving || !newCategoryName.trim()"
            class="gradient-brand px-5 py-2.5 text-[0.84rem] font-semibold text-white rounded-[10px] transition-opacity disabled:opacity-50 shrink-0"
            @click="addCategory"
          >
            Add Category
          </button>
        </div>
      </div>

      <!-- Breadcrumb -->
      <div class="text-[0.78rem] text-text-muted mb-3">
        You are here: <span class="text-text-secondary font-medium">Top Level</span>
      </div>

      <!-- Loading -->
      <div v-if="categoriesLoading" class="flex items-center gap-3 text-text-secondary text-sm">
        <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
        Loading categories...
      </div>

      <!-- Categories list -->
      <div v-else-if="categories.length > 0" class="bg-surface-card border border-border rounded-2xl overflow-hidden">
        <div
          v-for="cat in categories"
          :key="cat.id"
          class="flex items-center justify-between gap-3 px-5 py-3.5 border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors"
        >
          <!-- Inline edit mode -->
          <template v-if="editingCategoryId === cat.id">
            <input
              v-model="editingCategoryName"
              type="text"
              class="flex-1 bg-white/[0.04] border border-border rounded-[10px] px-3 py-2 text-[0.82rem] text-text-primary focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
              @keyup.enter="saveEditCategory"
              @keyup.escape="cancelEditCategory"
            />
            <div class="flex items-center gap-2">
              <button
                type="button"
                class="text-[0.78rem] text-primary-400 hover:underline"
                @click="saveEditCategory"
              >
                Save
              </button>
              <button
                type="button"
                class="text-[0.78rem] text-text-muted hover:text-text-secondary"
                @click="cancelEditCategory"
              >
                Cancel
              </button>
            </div>
          </template>

          <!-- Display mode -->
          <template v-else>
            <span class="text-[0.82rem] text-text-secondary">{{ cat.name }}</span>
            <div class="flex items-center gap-2">
              <button
                type="button"
                class="text-text-muted hover:text-primary-400 transition-colors"
                @click="startEditCategory(cat)"
              >
                <svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                  <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7" />
                  <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z" />
                </svg>
              </button>
              <button
                type="button"
                class="text-text-muted hover:text-status-red transition-colors"
                @click="confirmDeleteCategory(cat.id)"
              >
                <svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                  <polyline points="3 6 5 6 21 6" />
                  <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
                </svg>
              </button>
            </div>
          </template>
        </div>
      </div>

      <!-- Empty state -->
      <div v-else class="bg-surface-card border border-border rounded-2xl flex flex-col items-center justify-center py-12 gap-3">
        <p class="text-[0.875rem] font-medium text-text-secondary">No Categories Found</p>
        <p class="text-[0.78rem] text-text-muted">Create your first category above.</p>
      </div>
    </div>

    <!-- ===== Add Predefined Reply Tab ===== -->
    <div v-if="activeTab === 'reply'">

      <!-- Add reply form -->
      <div class="bg-surface-card border border-border rounded-2xl p-5 mb-5 space-y-4">

        <!-- Name -->
        <div>
          <label class="block text-[0.75rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Name</label>
          <input
            v-model="newReplyName"
            type="text"
            placeholder="Reply name"
            class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.82rem] text-text-primary placeholder:text-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
          />
        </div>

        <!-- Category -->
        <div>
          <label class="block text-[0.75rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Category</label>
          <AppSelect
            v-model="newReplyCategoryId"
            :options="categoryOptions"
            placeholder="Select category"
          />
        </div>

        <!-- Content -->
        <div>
          <label class="block text-[0.75rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">Content</label>
          <textarea
            v-model="newReplyContent"
            placeholder="Reply content..."
            class="w-full min-h-[200px] bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.82rem] text-text-primary placeholder:text-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors resize-y"
          />
        </div>

        <button
          type="button"
          :disabled="replySaving || !newReplyName.trim() || !newReplyCategoryId || !newReplyContent.trim()"
          class="gradient-brand px-5 py-2 text-[0.84rem] font-semibold text-white rounded-[10px] transition-opacity disabled:opacity-50"
          @click="addReply"
        >
          Add Reply
        </button>
      </div>

      <!-- Loading -->
      <div v-if="repliesLoading" class="flex items-center gap-3 text-text-secondary text-sm">
        <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
        Loading replies...
      </div>

      <!-- Replies list -->
      <div v-else-if="replies.length > 0" class="bg-surface-card border border-border rounded-2xl overflow-hidden">

        <!-- Header -->
        <div class="hidden sm:grid grid-cols-[1fr_0.8fr_40px_40px] gap-3 px-5 py-3 border-b border-border bg-white/[0.02]">
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Name</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Category</span>
          <span />
          <span />
        </div>

        <div
          v-for="reply in replies"
          :key="reply.id"
          class="border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors"
        >
          <!-- Edit mode -->
          <div v-if="editingReplyId === reply.id" class="p-5 space-y-3">
            <input
              v-model="editingReply.name"
              type="text"
              class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2 text-[0.82rem] text-text-primary focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
            />
            <AppSelect
              v-model="editingReply.categoryId"
              :options="categoryOptions"
              placeholder="Select category"
            />
            <textarea
              v-model="editingReply.content"
              class="w-full min-h-[120px] bg-white/[0.04] border border-border rounded-[10px] px-3 py-2 text-[0.82rem] text-text-primary focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors resize-y"
            />
            <div class="flex items-center gap-2">
              <button
                type="button"
                class="gradient-brand px-4 py-1.5 text-[0.78rem] font-semibold text-white rounded-[10px] transition-opacity"
                @click="saveEditReply"
              >
                Save
              </button>
              <button
                type="button"
                class="px-4 py-1.5 text-[0.78rem] font-medium text-text-secondary bg-white/[0.04] border border-border rounded-[10px] hover:bg-white/[0.06] transition-colors"
                @click="cancelEditReply"
              >
                Cancel
              </button>
            </div>
          </div>

          <!-- Display mode -->
          <div v-else class="grid grid-cols-1 sm:grid-cols-[1fr_0.8fr_40px_40px] gap-3 px-5 py-3.5 items-center">
            <span class="text-[0.82rem] text-text-secondary">{{ reply.name }}</span>
            <span class="text-[0.82rem] text-text-muted">{{ reply.categoryName }}</span>
            <div class="flex items-center justify-center">
              <button
                type="button"
                class="text-text-muted hover:text-primary-400 transition-colors"
                @click="startEditReply(reply)"
              >
                <svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                  <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7" />
                  <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z" />
                </svg>
              </button>
            </div>
            <div class="flex items-center justify-center">
              <button
                type="button"
                class="text-text-muted hover:text-status-red transition-colors"
                @click="confirmDeleteReply(reply.id)"
              >
                <svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                  <polyline points="3 6 5 6 21 6" />
                  <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
                </svg>
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- Empty state -->
      <div v-else-if="!repliesLoading" class="bg-surface-card border border-border rounded-2xl flex flex-col items-center justify-center py-12 gap-3">
        <p class="text-[0.875rem] font-medium text-text-secondary">No Predefined Replies Found</p>
        <p class="text-[0.78rem] text-text-muted">Create your first reply using the form above.</p>
      </div>
    </div>

    <!-- ===== Search/Filter Tab ===== -->
    <div v-if="activeTab === 'search'">

      <!-- Search input -->
      <div class="mb-5">
        <input
          v-model="searchQuery"
          type="text"
          placeholder="Search predefined replies by name or content..."
          class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.82rem] text-text-primary placeholder:text-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
          @input="handleSearch"
        />
      </div>

      <!-- Searching -->
      <div v-if="searching" class="flex items-center gap-3 text-text-secondary text-sm">
        <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
        Searching...
      </div>

      <!-- Search results -->
      <div v-else-if="searchResults.length > 0" class="bg-surface-card border border-border rounded-2xl overflow-hidden">

        <div class="hidden sm:grid grid-cols-[1fr_0.8fr_2fr] gap-3 px-5 py-3 border-b border-border bg-white/[0.02]">
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Name</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Category</span>
          <span class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Content</span>
        </div>

        <div
          v-for="result in searchResults"
          :key="result.id"
          class="grid grid-cols-1 sm:grid-cols-[1fr_0.8fr_2fr] gap-3 px-5 py-3.5 border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors items-start"
        >
          <span class="text-[0.82rem] text-text-secondary">{{ result.name }}</span>
          <span class="text-[0.82rem] text-text-muted">{{ result.categoryName }}</span>
          <span class="text-[0.82rem] text-text-muted line-clamp-2">{{ result.content }}</span>
        </div>
      </div>

      <!-- Empty search -->
      <div v-else-if="searchQuery.trim() && !searching" class="bg-surface-card border border-border rounded-2xl flex flex-col items-center justify-center py-12 gap-3">
        <p class="text-[0.875rem] font-medium text-text-secondary">No Results Found</p>
        <p class="text-[0.78rem] text-text-muted">Try a different search term.</p>
      </div>

      <!-- Initial state -->
      <div v-else-if="!searchQuery.trim()" class="bg-surface-card border border-border rounded-2xl flex flex-col items-center justify-center py-12 gap-3">
        <p class="text-[0.875rem] font-medium text-text-secondary">Search Predefined Replies</p>
        <p class="text-[0.78rem] text-text-muted">Type above to search by name or content.</p>
      </div>
    </div>

    <!-- Delete Category Modal -->
    <ConfirmModal
      v-if="deleteCategoryTarget !== null"
      title="Delete Category"
      message="Are you sure you want to delete this category? All replies in this category may be affected."
      confirm-label="Delete"
      loading-label="Deleting..."
      :loading="deletingCategory"
      variant="danger"
      @confirm="handleDeleteCategory"
      @close="deleteCategoryTarget = null"
    />

    <!-- Delete Reply Modal -->
    <ConfirmModal
      v-if="deleteReplyTarget !== null"
      title="Delete Predefined Reply"
      message="Are you sure you want to delete this predefined reply? This action cannot be undone."
      confirm-label="Delete"
      loading-label="Deleting..."
      :loading="deletingReply"
      variant="danger"
      @confirm="handleDeleteReply"
      @close="deleteReplyTarget = null"
    />

  </div>
</template>
