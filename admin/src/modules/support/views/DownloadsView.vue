<script setup lang="ts">
/**
 * Downloads management view -- categories and downloadable files.
 * Supports nested category navigation, CRUD for both categories and downloads.
 */
import { ref, computed, onMounted, watch } from 'vue'
import { useApi } from '../../../composables/useApi'
import AppSelect from '../../../components/AppSelect.vue'
import AppCheckbox from '../../../components/AppCheckbox.vue'
import ConfirmModal from '../../../components/ConfirmModal.vue'

const { request } = useApi()

// --- Types ---

/** Shape of a download category returned by the API. */
interface DownloadCategory {
  /** Unique category identifier. */
  id: number
  /** Category display name. */
  name: string
  /** Category description text. */
  description: string
  /** Whether the category is hidden from clients. */
  isHidden: boolean
  /** Parent category ID, null for top-level categories. */
  parentCategoryId: number | null
  /** Number of downloads in this category. */
  downloadCount: number
}

/** Shape of a downloadable file returned by the API. */
interface DownloadItem {
  /** Unique download identifier. */
  id: number
  /** Download title. */
  title: string
  /** Download description text. */
  description: string
  /** File type label (e.g. "ZIP File", "PDF"). */
  type: string
  /** Name of the downloadable file. */
  filename: string
  /** Category this download belongs to. */
  categoryId: number
  /** Category name for display purposes. */
  categoryName: string | null
  /** Total number of times downloaded. */
  downloadCount: number
  /** Whether only logged-in clients can download. */
  clientsOnly: boolean
  /** Whether a product purchase is required to download. */
  productDownload: boolean
  /** Whether the download is hidden from the client area. */
  isHidden: boolean
  /** ISO 8601 creation timestamp. */
  createdAt: string
}

// --- State ---

/** Currently active tab: add category or add download. */
const activeTab = ref<'category' | 'download'>('category')

/** Tab definitions for rendering. */
const tabs = [
  { key: 'category' as const, label: 'Add Category' },
  { key: 'download' as const, label: 'Add Download' },
]

/** All loaded categories (flat list). */
const categories = ref<DownloadCategory[]>([])

/** Downloads at the current navigation level. */
const downloads = ref<DownloadItem[]>([])

/** Whether data is loading. */
const loading = ref(false)

/** Whether a form submission is in progress. */
const saving = ref(false)

/** Error message from the last operation. */
const error = ref<string | null>(null)

/** Current category ID for nested navigation (null = root). */
const currentCategoryId = ref<number | null>(null)

/** Breadcrumb trail for nested navigation. */
const breadcrumbs = ref<Array<{ id: number | null; name: string }>>([])

// --- Add Category form ---

/** New category name. */
const newCategoryName = ref('')

/** New category description. */
const newCategoryDescription = ref('')

/** Whether the new category should be hidden. */
const newCategoryHidden = ref(false)

// --- Add Download form ---

/** New download file type. */
const newDownloadType = ref('ZIP File')

/** New download title. */
const newDownloadTitle = ref('')

/** New download description. */
const newDownloadDescription = ref('')

/** New download filename. */
const newDownloadFilename = ref('')

/** New download category ID. */
const newDownloadCategoryId = ref<number>(0)

/** Whether the new download is clients-only. */
const newDownloadClientsOnly = ref(false)

/** Whether the new download requires a product purchase. */
const newDownloadProductDownload = ref(false)

/** Whether the new download is hidden. */
const newDownloadHidden = ref(false)

// --- Edit Category modal ---

/** Category currently being edited, null when modal is closed. */
const editingCategory = ref<DownloadCategory | null>(null)

/** Edit form: category name. */
const editCategoryName = ref('')

/** Edit form: category description. */
const editCategoryDescription = ref('')

/** Edit form: hidden flag. */
const editCategoryHidden = ref(false)

/** Edit form: parent category ID. */
const editCategoryParentId = ref<number | null>(null)

// --- Edit Download modal ---

/** Download currently being edited, null when modal is closed. */
const editingDownload = ref<DownloadItem | null>(null)

/** Edit form: download title. */
const editDownloadTitle = ref('')

/** Edit form: download description. */
const editDownloadDescription = ref('')

/** Edit form: download type. */
const editDownloadType = ref('')

/** Edit form: download filename. */
const editDownloadFilename = ref('')

/** Edit form: download category ID. */
const editDownloadCategoryId = ref(0)

/** Edit form: clients-only flag. */
const editDownloadClientsOnly = ref(false)

/** Edit form: product download flag. */
const editDownloadProductDownload = ref(false)

/** Edit form: hidden flag. */
const editDownloadHidden = ref(false)

// --- Delete confirmation ---

/** Whether the delete confirmation modal is visible. */
const showDeleteModal = ref(false)

/** Target for the pending deletion. */
const deleteTarget = ref<{ type: 'category' | 'download'; id: number; name: string } | null>(null)

/** Whether a deletion is in progress. */
const deleting = ref(false)

/** Type options for the download type dropdown. */
const typeOptions = [
  { value: 'ZIP File', label: 'ZIP File' },
  { value: 'Executable', label: 'Executable' },
  { value: 'PDF', label: 'PDF' },
  { value: 'Other', label: 'Other' },
]

/** Category options for dropdowns, including a "Top Level" entry. */
const categoryOptions = computed(() => [
  { value: 0, label: 'Top Level' },
  ...categories.value.map(c => ({ value: c.id, label: c.name })),
])

/** Parent category options for edit modal (excludes the category being edited). */
const parentCategoryOptions = computed(() => [
  { value: 0, label: 'Top Level' },
  ...categories.value
    .filter(c => c.id !== editingCategory.value?.id)
    .map(c => ({ value: c.id, label: c.name })),
])

/** Categories visible at the current navigation level. */
const visibleCategories = computed(() =>
  categories.value.filter(c => c.parentCategoryId === currentCategoryId.value)
)

// --- API methods ---

/**
 * Fetches all download categories from the server.
 */
async function fetchCategories(): Promise<void> {
  try {
    categories.value = await request<DownloadCategory[]>('/downloads/categories')
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to load categories'
  }
}

/**
 * Fetches downloads, optionally filtered by the current category.
 */
async function fetchDownloads(): Promise<void> {
  try {
    const url = currentCategoryId.value
      ? `/downloads?categoryId=${currentCategoryId.value}`
      : '/downloads'
    downloads.value = await request<DownloadItem[]>(url)
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to load downloads'
  }
}

/**
 * Loads both categories and downloads. Shows a loading indicator.
 */
async function fetchAll(): Promise<void> {
  loading.value = true
  error.value = null
  await Promise.all([fetchCategories(), fetchDownloads()])
  loading.value = false
}

/**
 * Creates a new download category at the current navigation level.
 */
async function createCategory(): Promise<void> {
  if (!newCategoryName.value.trim()) return
  saving.value = true
  error.value = null
  try {
    await request('/downloads/categories', {
      method: 'POST',
      body: JSON.stringify({
        name: newCategoryName.value.trim(),
        description: newCategoryDescription.value.trim(),
        isHidden: newCategoryHidden.value,
        parentCategoryId: currentCategoryId.value,
      }),
    })
    newCategoryName.value = ''
    newCategoryDescription.value = ''
    newCategoryHidden.value = false
    await fetchAll()
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to create category'
  } finally {
    saving.value = false
  }
}

/**
 * Opens the edit modal for a category and populates form fields.
 *
 * @param cat - The category to edit.
 */
function openEditCategory(cat: DownloadCategory): void {
  editingCategory.value = cat
  editCategoryName.value = cat.name
  editCategoryDescription.value = cat.description
  editCategoryHidden.value = cat.isHidden
  editCategoryParentId.value = cat.parentCategoryId
}

/**
 * Saves changes to the category being edited.
 */
async function updateCategory(): Promise<void> {
  if (!editingCategory.value) return
  saving.value = true
  error.value = null
  try {
    await request(`/downloads/categories/${editingCategory.value.id}`, {
      method: 'PUT',
      body: JSON.stringify({
        name: editCategoryName.value.trim(),
        description: editCategoryDescription.value.trim(),
        isHidden: editCategoryHidden.value,
        parentCategoryId: editCategoryParentId.value === 0 ? null : editCategoryParentId.value,
      }),
    })
    editingCategory.value = null
    await fetchAll()
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to update category'
  } finally {
    saving.value = false
  }
}

/**
 * Creates a new download file entry.
 */
async function createDownload(): Promise<void> {
  if (!newDownloadTitle.value.trim()) return
  saving.value = true
  error.value = null
  try {
    await request('/downloads', {
      method: 'POST',
      body: JSON.stringify({
        title: newDownloadTitle.value.trim(),
        description: newDownloadDescription.value.trim(),
        type: newDownloadType.value,
        filename: newDownloadFilename.value.trim(),
        categoryId: newDownloadCategoryId.value || null,
        clientsOnly: newDownloadClientsOnly.value,
        productDownload: newDownloadProductDownload.value,
        isHidden: newDownloadHidden.value,
      }),
    })
    resetDownloadForm()
    await fetchAll()
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to create download'
  } finally {
    saving.value = false
  }
}

/**
 * Resets the add download form to its default state.
 */
function resetDownloadForm(): void {
  newDownloadType.value = 'ZIP File'
  newDownloadTitle.value = ''
  newDownloadDescription.value = ''
  newDownloadFilename.value = ''
  newDownloadCategoryId.value = currentCategoryId.value ?? 0
  newDownloadClientsOnly.value = false
  newDownloadProductDownload.value = false
  newDownloadHidden.value = false
}

/**
 * Opens the edit modal for a download and populates form fields.
 *
 * @param dl - The download item to edit.
 */
function openEditDownload(dl: DownloadItem): void {
  editingDownload.value = dl
  editDownloadTitle.value = dl.title
  editDownloadDescription.value = dl.description
  editDownloadType.value = dl.type
  editDownloadFilename.value = dl.filename
  editDownloadCategoryId.value = dl.categoryId
  editDownloadClientsOnly.value = dl.clientsOnly
  editDownloadProductDownload.value = dl.productDownload
  editDownloadHidden.value = dl.isHidden
}

/**
 * Saves changes to the download being edited.
 */
async function updateDownload(): Promise<void> {
  if (!editingDownload.value) return
  saving.value = true
  error.value = null
  try {
    await request(`/downloads/${editingDownload.value.id}`, {
      method: 'PUT',
      body: JSON.stringify({
        title: editDownloadTitle.value.trim(),
        description: editDownloadDescription.value.trim(),
        type: editDownloadType.value,
        filename: editDownloadFilename.value.trim(),
        categoryId: editDownloadCategoryId.value || null,
        clientsOnly: editDownloadClientsOnly.value,
        productDownload: editDownloadProductDownload.value,
        isHidden: editDownloadHidden.value,
      }),
    })
    editingDownload.value = null
    await fetchAll()
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to update download'
  } finally {
    saving.value = false
  }
}

/**
 * Opens the delete confirmation modal for a category or download.
 *
 * @param type - Whether deleting a category or download.
 * @param id - The ID of the item to delete.
 * @param name - Display name for the confirmation message.
 */
function confirmDelete(type: 'category' | 'download', id: number, name: string): void {
  deleteTarget.value = { type, id, name }
  showDeleteModal.value = true
}

/**
 * Executes the pending deletion after confirmation.
 */
async function handleDelete(): Promise<void> {
  if (!deleteTarget.value) return
  deleting.value = true
  error.value = null
  try {
    const { type, id } = deleteTarget.value
    const endpoint = type === 'category'
      ? `/downloads/categories/${id}`
      : `/downloads/${id}`
    await request(endpoint, { method: 'DELETE' })
    showDeleteModal.value = false
    deleteTarget.value = null
    await fetchAll()
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to delete item'
  } finally {
    deleting.value = false
  }
}

/**
 * Navigates into a sub-category, updating breadcrumbs and fetching its contents.
 *
 * @param cat - The category to navigate into.
 */
function navigateToCategory(cat: DownloadCategory): void {
  breadcrumbs.value.push({ id: cat.id, name: cat.name })
  currentCategoryId.value = cat.id
  fetchDownloads()
}

/**
 * Navigates back to the root (Download Home), clearing breadcrumbs.
 */
function navigateHome(): void {
  currentCategoryId.value = null
  breadcrumbs.value = []
  fetchDownloads()
}

/**
 * Navigates to a specific breadcrumb level, trimming deeper entries.
 *
 * @param index - The breadcrumb index to navigate to.
 */
function navigateToBreadcrumb(index: number): void {
  const crumb = breadcrumbs.value[index]
  breadcrumbs.value = breadcrumbs.value.slice(0, index + 1)
  currentCategoryId.value = crumb.id
  fetchDownloads()
}

/** Sync the new download category ID when navigating into a category. */
watch(currentCategoryId, (id) => {
  newDownloadCategoryId.value = id ?? 0
})

onMounted(() => {
  fetchAll()
})
</script>

<template>
  <div class="p-4 sm:p-6 lg:p-8 w-full">

    <!-- Header -->
    <div class="mb-5">
      <h1 class="font-display font-bold text-[1.25rem] text-text-primary leading-none">Downloads</h1>
      <p class="text-[0.78rem] text-text-secondary mt-1">Manage downloadable files and categories</p>
    </div>

    <!-- Error -->
    <div
      v-if="error"
      class="text-sm text-status-red bg-status-red/8 border border-status-red/20 rounded-xl p-4 mb-4"
    >
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
    <div v-if="activeTab === 'category'" class="mb-5">
      <div class="bg-surface-card border border-border rounded-2xl p-5 space-y-4">

        <!-- Name + Hidden -->
        <div class="flex flex-col sm:flex-row sm:items-end gap-4">
          <div class="flex-1">
            <label class="block text-[0.75rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">
              Category Name
            </label>
            <input
              v-model="newCategoryName"
              type="text"
              placeholder="Enter category name"
              class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.82rem] text-text-primary placeholder:text-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
            />
          </div>
          <div class="flex items-center gap-3 sm:pb-0.5">
            <AppCheckbox v-model="newCategoryHidden" />
            <span
              class="text-[0.82rem] text-text-secondary cursor-pointer whitespace-nowrap"
              @click="newCategoryHidden = !newCategoryHidden"
            >
              Check to Hide
            </span>
          </div>
        </div>

        <!-- Description -->
        <div>
          <label class="block text-[0.75rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">
            Description
          </label>
          <input
            v-model="newCategoryDescription"
            type="text"
            placeholder="Category description"
            class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.82rem] text-text-primary placeholder:text-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
          />
        </div>

        <!-- Submit -->
        <div class="flex justify-center">
          <button
            type="button"
            :disabled="saving || !newCategoryName.trim()"
            class="gradient-brand px-5 py-2 text-[0.84rem] font-semibold text-white rounded-[10px] transition-opacity disabled:opacity-50"
            @click="createCategory"
          >
            <span v-if="saving" class="flex items-center gap-2">
              <span class="w-3.5 h-3.5 rounded-full border-2 border-white/20 border-t-white animate-spin" />
              Saving...
            </span>
            <span v-else>Add Category</span>
          </button>
        </div>
      </div>
    </div>

    <!-- ===== Add Download Tab ===== -->
    <div v-if="activeTab === 'download'" class="mb-5">
      <div class="bg-surface-card border border-border rounded-2xl p-5 space-y-4">

        <!-- Type -->
        <div>
          <label class="block text-[0.75rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">
            Type
          </label>
          <AppSelect v-model="newDownloadType" :options="typeOptions" />
        </div>

        <!-- Title -->
        <div>
          <label class="block text-[0.75rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">
            Title
          </label>
          <input
            v-model="newDownloadTitle"
            type="text"
            placeholder="Enter download title"
            class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.82rem] text-text-primary placeholder:text-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
          />
        </div>

        <!-- Description -->
        <div>
          <label class="block text-[0.75rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">
            Description
          </label>
          <textarea
            v-model="newDownloadDescription"
            rows="3"
            placeholder="Enter description..."
            class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.82rem] text-text-primary placeholder:text-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors resize-y"
          />
        </div>

        <!-- Filename -->
        <div>
          <label class="block text-[0.75rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">
            Filename
          </label>
          <input
            v-model="newDownloadFilename"
            type="text"
            placeholder="Enter filename..."
            class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.82rem] text-text-primary placeholder:text-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
          />
        </div>

        <!-- Category -->
        <div>
          <label class="block text-[0.75rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">
            Category
          </label>
          <AppSelect v-model="newDownloadCategoryId" :options="categoryOptions" placeholder="Select category" />
        </div>

        <!-- Checkboxes -->
        <div class="flex flex-col sm:flex-row sm:flex-wrap gap-4">
          <div class="flex items-center gap-3">
            <AppCheckbox v-model="newDownloadClientsOnly" />
            <span
              class="text-[0.82rem] text-text-secondary cursor-pointer"
              @click="newDownloadClientsOnly = !newDownloadClientsOnly"
            >
              Clients Only
            </span>
          </div>
          <div class="flex items-center gap-3">
            <AppCheckbox v-model="newDownloadProductDownload" />
            <span
              class="text-[0.82rem] text-text-secondary cursor-pointer"
              @click="newDownloadProductDownload = !newDownloadProductDownload"
            >
              Product Download
            </span>
          </div>
          <div class="flex items-center gap-3">
            <AppCheckbox v-model="newDownloadHidden" />
            <span
              class="text-[0.82rem] text-text-secondary cursor-pointer"
              @click="newDownloadHidden = !newDownloadHidden"
            >
              Hidden
            </span>
          </div>
        </div>
        <p class="text-[0.72rem] text-text-muted -mt-2">
          Clients Only restricts to logged-in clients. Product Download requires a product or addon purchase. Hidden removes from client area.
        </p>

        <!-- Buttons -->
        <div class="flex items-center gap-3">
          <button
            type="button"
            :disabled="saving || !newDownloadTitle.trim()"
            class="gradient-brand px-5 py-2 text-[0.84rem] font-semibold text-white rounded-[10px] transition-opacity disabled:opacity-50"
            @click="createDownload"
          >
            <span v-if="saving" class="flex items-center gap-2">
              <span class="w-3.5 h-3.5 rounded-full border-2 border-white/20 border-t-white animate-spin" />
              Saving...
            </span>
            <span v-else>Add Download</span>
          </button>
          <button
            type="button"
            class="px-5 py-2 text-[0.84rem] font-medium text-text-secondary bg-white/[0.04] border border-border rounded-[10px] hover:bg-white/[0.06] transition-colors"
            @click="resetDownloadForm"
          >
            Cancel
          </button>
        </div>
      </div>
    </div>

    <!-- Breadcrumb -->
    <div class="text-[0.78rem] text-text-muted mb-4">
      <span>You are here: </span>
      <button
        type="button"
        class="font-medium transition-colors"
        :class="breadcrumbs.length > 0 ? 'text-primary-400 hover:text-primary-300' : 'text-text-secondary'"
        @click="navigateHome"
      >
        Download Home
      </button>
      <template v-for="(crumb, idx) in breadcrumbs" :key="crumb.id">
        <span class="mx-1.5 text-text-muted">&gt;</span>
        <button
          v-if="idx < breadcrumbs.length - 1"
          type="button"
          class="text-primary-400 hover:text-primary-300 font-medium transition-colors"
          @click="navigateToBreadcrumb(idx)"
        >
          {{ crumb.name }}
        </button>
        <span v-else class="text-text-secondary font-medium">{{ crumb.name }}</span>
      </template>
    </div>

    <!-- Loading -->
    <div v-if="loading" class="flex items-center gap-3 text-text-secondary text-sm py-8">
      <span class="w-4 h-4 rounded-full border-2 border-primary-500/20 border-t-primary-500 animate-spin" />
      Loading...
    </div>

    <template v-else>

      <!-- ===== Categories Section ===== -->
      <template v-if="visibleCategories.length > 0">
        <div class="bg-surface-card border border-border rounded-2xl overflow-hidden mb-5">
          <!-- Section header -->
          <div class="px-5 py-3 bg-white/[0.02] border-b border-border">
            <h2 class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Categories</h2>
          </div>

          <!-- Category grid -->
          <div class="p-5">
            <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
              <div
                v-for="cat in visibleCategories"
                :key="cat.id"
                class="bg-white/[0.02] border border-border rounded-xl p-4 hover:bg-white/[0.04] transition-colors"
              >
                <!-- Header row -->
                <div class="flex items-start justify-between gap-3 mb-1.5">
                  <div class="flex items-center gap-2.5 min-w-0">
                    <!-- Folder icon -->
                    <svg
                      class="w-5 h-5 text-primary-400 shrink-0"
                      viewBox="0 0 24 24"
                      fill="none"
                      stroke="currentColor"
                      stroke-width="2"
                      stroke-linecap="round"
                      stroke-linejoin="round"
                    >
                      <path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z" />
                    </svg>
                    <button
                      type="button"
                      class="text-[0.88rem] font-medium text-text-primary hover:text-primary-400 transition-colors truncate"
                      @click="navigateToCategory(cat)"
                    >
                      {{ cat.name }}
                      <span class="text-text-muted text-[0.78rem] font-normal">({{ cat.downloadCount }})</span>
                    </button>
                  </div>
                  <div class="flex items-center gap-1.5 shrink-0">
                    <!-- Edit button -->
                    <button
                      type="button"
                      class="text-text-muted hover:text-primary-400 transition-colors p-0.5"
                      @click="openEditCategory(cat)"
                    >
                      <svg
                        class="w-3.5 h-3.5"
                        viewBox="0 0 24 24"
                        fill="none"
                        stroke="currentColor"
                        stroke-width="2"
                        stroke-linecap="round"
                        stroke-linejoin="round"
                      >
                        <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7" />
                        <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z" />
                      </svg>
                    </button>
                    <!-- Delete button -->
                    <button
                      type="button"
                      class="text-text-muted hover:text-status-red transition-colors p-0.5"
                      @click="confirmDelete('category', cat.id, cat.name)"
                    >
                      <svg
                        class="w-3.5 h-3.5"
                        viewBox="0 0 24 24"
                        fill="none"
                        stroke="currentColor"
                        stroke-width="2"
                        stroke-linecap="round"
                        stroke-linejoin="round"
                      >
                        <circle cx="12" cy="12" r="10" />
                        <line x1="15" y1="9" x2="9" y2="15" />
                        <line x1="9" y1="9" x2="15" y2="15" />
                      </svg>
                    </button>
                  </div>
                </div>
                <!-- Description -->
                <p v-if="cat.description" class="text-[0.78rem] text-text-muted ml-7.5 line-clamp-2">
                  {{ cat.description }}
                </p>
                <!-- Hidden badge -->
                <span
                  v-if="cat.isHidden"
                  class="inline-block mt-2 ml-7.5 px-2 py-0.5 rounded-full text-[0.68rem] font-medium text-status-yellow bg-status-yellow/10 border border-status-yellow/20"
                >
                  Hidden
                </span>
              </div>
            </div>
          </div>
        </div>
      </template>

      <!-- ===== Files Section ===== -->
      <div class="bg-surface-card border border-border rounded-2xl overflow-hidden">
        <!-- Section header -->
        <div class="px-5 py-3 bg-white/[0.02] border-b border-border">
          <h2 class="text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Files</h2>
        </div>

        <!-- Download list -->
        <div v-if="downloads.length > 0" class="divide-y divide-border">
          <div
            v-for="dl in downloads"
            :key="dl.id"
            class="px-5 py-4 hover:bg-white/[0.02] transition-colors"
          >
            <div class="flex items-start justify-between gap-3">
              <div class="flex items-center gap-2.5 min-w-0">
                <!-- File icon -->
                <svg
                  class="w-5 h-5 text-text-muted shrink-0"
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="currentColor"
                  stroke-width="2"
                  stroke-linecap="round"
                  stroke-linejoin="round"
                >
                  <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
                  <polyline points="14 2 14 8 20 8" />
                </svg>
                <button
                  type="button"
                  class="text-[0.88rem] font-medium text-text-primary hover:text-primary-400 transition-colors truncate"
                  @click="openEditDownload(dl)"
                >
                  {{ dl.title }}
                </button>
              </div>
              <button
                type="button"
                class="text-status-red hover:text-status-red/80 transition-colors p-0.5 shrink-0"
                @click="confirmDelete('download', dl.id, dl.title)"
              >
                <svg
                  class="w-3.5 h-3.5"
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="currentColor"
                  stroke-width="2"
                  stroke-linecap="round"
                  stroke-linejoin="round"
                >
                  <polyline points="3 6 5 6 21 6" />
                  <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
                </svg>
              </button>
            </div>
            <p v-if="dl.description" class="text-[0.78rem] text-text-muted ml-7.5 mt-1 line-clamp-2">
              {{ dl.description }}
            </p>
            <div class="flex items-center gap-3 ml-7.5 mt-1.5">
              <span class="text-[0.72rem] text-text-muted/60">Downloads: {{ dl.downloadCount }}</span>
              <span
                v-if="dl.clientsOnly"
                class="px-1.5 py-0.5 rounded text-[0.65rem] font-medium text-primary-400 bg-primary-500/10"
              >
                Clients Only
              </span>
              <span
                v-if="dl.isHidden"
                class="px-1.5 py-0.5 rounded text-[0.65rem] font-medium text-status-yellow bg-status-yellow/10"
              >
                Hidden
              </span>
            </div>
          </div>
        </div>

        <!-- Empty state -->
        <div v-else class="flex flex-col items-center justify-center py-12 gap-2">
          <p class="text-[0.875rem] font-medium text-text-secondary">No Downloads Found</p>
          <p class="text-[0.78rem] text-text-muted">Add a download using the form above.</p>
        </div>
      </div>
    </template>

    <!-- ===== Edit Category Modal ===== -->
    <Teleport to="body">
      <div
        v-if="editingCategory"
        class="fixed inset-0 z-50 flex items-center justify-center bg-black/40"
        @click.self="editingCategory = null"
      >
        <div class="bg-surface-card border border-border rounded-2xl shadow-2xl w-full max-w-md p-6 space-y-4">
          <div class="flex items-center justify-between mb-1">
            <h2 class="text-text-primary font-semibold text-[1rem]">Edit Category</h2>
            <button
              type="button"
              class="text-text-muted hover:text-text-primary transition-colors"
              @click="editingCategory = null"
            >
              <svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <line x1="18" y1="6" x2="6" y2="18" />
                <line x1="6" y1="6" x2="18" y2="18" />
              </svg>
            </button>
          </div>

          <!-- Parent Category -->
          <div>
            <label class="block text-[0.75rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">
              Parent Category
            </label>
            <AppSelect
              :model-value="editCategoryParentId ?? 0"
              :options="parentCategoryOptions"
              @update:model-value="editCategoryParentId = $event === 0 ? null : $event"
            />
          </div>

          <!-- Name -->
          <div>
            <label class="block text-[0.75rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">
              Category Name
            </label>
            <input
              v-model="editCategoryName"
              type="text"
              class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.82rem] text-text-primary placeholder:text-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
            />
          </div>

          <!-- Description -->
          <div>
            <label class="block text-[0.75rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">
              Description
            </label>
            <input
              v-model="editCategoryDescription"
              type="text"
              placeholder="Category description"
              class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.82rem] text-text-primary placeholder:text-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
            />
          </div>

          <!-- Hidden -->
          <div class="flex items-center gap-3">
            <AppCheckbox v-model="editCategoryHidden" />
            <span
              class="text-[0.82rem] text-text-secondary cursor-pointer"
              @click="editCategoryHidden = !editCategoryHidden"
            >
              Hidden
            </span>
          </div>

          <!-- Buttons -->
          <div class="flex items-center gap-3 pt-2">
            <button
              type="button"
              :disabled="saving || !editCategoryName.trim()"
              class="gradient-brand px-5 py-2 text-[0.84rem] font-semibold text-white rounded-[10px] transition-opacity disabled:opacity-50"
              @click="updateCategory"
            >
              Save Changes
            </button>
            <button
              type="button"
              class="px-5 py-2 text-[0.84rem] font-medium text-text-secondary bg-white/[0.04] border border-border rounded-[10px] hover:bg-white/[0.06] transition-colors"
              @click="editingCategory = null"
            >
              Cancel
            </button>
          </div>
        </div>
      </div>
    </Teleport>

    <!-- ===== Edit Download Modal ===== -->
    <Teleport to="body">
      <div
        v-if="editingDownload"
        class="fixed inset-0 z-50 flex items-center justify-center bg-black/40 overflow-y-auto py-8"
        @click.self="editingDownload = null"
      >
        <div class="bg-surface-card border border-border rounded-2xl shadow-2xl w-full max-w-lg p-6 space-y-4">
          <div class="flex items-center justify-between mb-1">
            <h2 class="text-text-primary font-semibold text-[1rem]">Edit Download</h2>
            <button
              type="button"
              class="text-text-muted hover:text-text-primary transition-colors"
              @click="editingDownload = null"
            >
              <svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <line x1="18" y1="6" x2="6" y2="18" />
                <line x1="6" y1="6" x2="18" y2="18" />
              </svg>
            </button>
          </div>

          <!-- Category -->
          <div>
            <label class="block text-[0.75rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">
              Category
            </label>
            <AppSelect v-model="editDownloadCategoryId" :options="categoryOptions" placeholder="Select category" />
          </div>

          <!-- Type -->
          <div>
            <label class="block text-[0.75rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">
              Type
            </label>
            <AppSelect v-model="editDownloadType" :options="typeOptions" />
          </div>

          <!-- Title -->
          <div>
            <label class="block text-[0.75rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">
              Title
            </label>
            <input
              v-model="editDownloadTitle"
              type="text"
              class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.82rem] text-text-primary placeholder:text-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
            />
          </div>

          <!-- Description -->
          <div>
            <label class="block text-[0.75rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">
              Description
            </label>
            <textarea
              v-model="editDownloadDescription"
              rows="3"
              placeholder="Description..."
              class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.82rem] text-text-primary placeholder:text-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors resize-y"
            />
          </div>

          <!-- Filename -->
          <div>
            <label class="block text-[0.75rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">
              Filename
            </label>
            <input
              v-model="editDownloadFilename"
              type="text"
              class="w-full bg-white/[0.04] border border-border rounded-[10px] px-3 py-2.5 text-[0.82rem] text-text-primary placeholder:text-text-muted focus:outline-none focus:border-primary-500/50 focus:ring-1 focus:ring-primary-500/10 transition-colors"
            />
          </div>

          <!-- Downloads count (read-only) -->
          <div>
            <label class="block text-[0.75rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1.5">
              Downloads
            </label>
            <input
              :value="editingDownload.downloadCount"
              type="number"
              disabled
              class="w-full bg-white/[0.02] border border-border rounded-[10px] px-3 py-2.5 text-[0.82rem] text-text-muted cursor-not-allowed"
            />
          </div>

          <!-- Checkboxes -->
          <div class="flex flex-col sm:flex-row sm:flex-wrap gap-4">
            <div class="flex items-center gap-3">
              <AppCheckbox v-model="editDownloadClientsOnly" />
              <span
                class="text-[0.82rem] text-text-secondary cursor-pointer"
                @click="editDownloadClientsOnly = !editDownloadClientsOnly"
              >
                Clients Only
              </span>
            </div>
            <div class="flex items-center gap-3">
              <AppCheckbox v-model="editDownloadProductDownload" />
              <span
                class="text-[0.82rem] text-text-secondary cursor-pointer"
                @click="editDownloadProductDownload = !editDownloadProductDownload"
              >
                Product Download
              </span>
            </div>
            <div class="flex items-center gap-3">
              <AppCheckbox v-model="editDownloadHidden" />
              <span
                class="text-[0.82rem] text-text-secondary cursor-pointer"
                @click="editDownloadHidden = !editDownloadHidden"
              >
                Hidden
              </span>
            </div>
          </div>

          <!-- Buttons -->
          <div class="flex items-center gap-3 pt-2">
            <button
              type="button"
              :disabled="saving || !editDownloadTitle.trim()"
              class="gradient-brand px-5 py-2 text-[0.84rem] font-semibold text-white rounded-[10px] transition-opacity disabled:opacity-50"
              @click="updateDownload"
            >
              Save Changes
            </button>
            <button
              type="button"
              class="px-5 py-2 text-[0.84rem] font-medium text-text-secondary bg-white/[0.04] border border-border rounded-[10px] hover:bg-white/[0.06] transition-colors"
              @click="editingDownload = null"
            >
              Cancel
            </button>
          </div>
        </div>
      </div>
    </Teleport>

    <!-- ===== Delete Confirmation Modal ===== -->
    <ConfirmModal
      v-if="showDeleteModal && deleteTarget"
      :title="`Delete ${deleteTarget.type === 'category' ? 'Category' : 'Download'}`"
      :message="`Are you sure you want to delete '${deleteTarget.name}'? This action cannot be undone.`"
      confirm-label="Delete"
      loading-label="Deleting..."
      :loading="deleting"
      variant="danger"
      @confirm="handleDelete"
      @close="showDeleteModal = false; deleteTarget = null"
    />

  </div>
</template>
