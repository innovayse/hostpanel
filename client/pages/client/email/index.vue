<template>
  <div>
    <div class="mb-6 flex items-center justify-between gap-4 flex-wrap">
      <div>
        <h1 class="text-2xl font-bold text-gray-900 dark:text-white">Email Domains</h1>
        <p class="text-gray-500 dark:text-gray-400 text-sm mt-1">Manage your email domains and mailboxes</p>
      </div>
      <button
        class="px-5 py-2.5 rounded-xl bg-cyan-500/10 border border-cyan-500/20 text-cyan-400 text-sm font-medium hover:bg-cyan-500/20 transition-colors flex items-center gap-2"
        @click="showAddModal = true"
      >
        <Plus :size="16" :stroke-width="2" />
        Add Email Domain
      </button>
    </div>

    <!-- Loading -->
    <div v-if="pending" class="space-y-3">
      <div v-for="i in 4" :key="i" class="h-20 rounded-xl bg-white/5 border border-white/10 animate-pulse" />
    </div>

    <template v-else>
      <!-- Toolbar -->
      <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3 mb-4">
        <p class="text-sm text-gray-500 dark:text-gray-400">
          Showing {{ pageFrom }}–{{ pageTo }} of {{ filtered.length }}
        </p>
        <UiSearchInput
          v-model="search"
          placeholder="Search email domains..."
          class="sm:w-64"
          @update:model-value="page = 1"
        />
      </div>

      <!-- Empty -->
      <div v-if="!filtered.length" class="text-center py-20">
        <Mail :size="48" :stroke-width="2" class="text-gray-300 dark:text-gray-600 mx-auto mb-4" />
        <p class="text-gray-500 dark:text-gray-400 mb-4">No email domains found.</p>
        <button
          v-if="!search"
          class="px-6 py-2.5 rounded-xl bg-cyan-500 text-white font-semibold text-sm hover:bg-cyan-400 transition-colors"
          @click="showAddModal = true"
        >
          Add your first email domain
        </button>
      </div>

      <!-- Email domains table -->
      <UiTable v-else>
        <UiTableHead>
          <UiTableRow :hoverable="false">
            <UiTableTh>Domain</UiTableTh>
            <UiTableTh align="center">Status</UiTableTh>
            <UiTableTh class="hidden md:table-cell">Mailboxes</UiTableTh>
            <UiTableTh class="hidden lg:table-cell">Created</UiTableTh>
            <UiTableTh align="right">Actions</UiTableTh>
          </UiTableRow>
        </UiTableHead>
        <UiTableBody>
          <UiTableRow
            v-for="domain in paged"
            :key="domain.id"
            group
          >
            <UiTableTd>
              <div class="flex items-center gap-3">
                <Mail :size="16" :stroke-width="2" class="text-primary-400 flex-shrink-0" />
                <span class="text-gray-900 dark:text-white font-medium text-sm">{{ domain.domainName }}</span>
              </div>
            </UiTableTd>
            <UiTableTd align="center">
              <ClientStatusBadge :status="domain.status" />
            </UiTableTd>
            <UiTableTd class="hidden md:table-cell text-gray-500 dark:text-gray-400 text-sm">
              {{ domain.mailboxCount }} / {{ domain.maxMailboxes }}
            </UiTableTd>
            <UiTableTd class="hidden lg:table-cell text-gray-500 dark:text-gray-400 text-sm">
              {{ formatDate(domain.createdAt) }}
            </UiTableTd>
            <UiTableTd align="right">
              <NuxtLink
                :to="`/client/email/${domain.id}`"
                class="px-3 py-1.5 rounded-lg border border-gray-200 dark:border-white/10 text-gray-500 dark:text-gray-400 text-xs hover:border-cyan-500/30 hover:text-gray-900 dark:hover:text-white transition-all"
              >
                Manage
              </NuxtLink>
            </UiTableTd>
          </UiTableRow>
        </UiTableBody>
      </UiTable>

      <UiPagination
        v-if="filtered.length > 0"
        v-model="page"
        v-model:per-page="perPage"
        :total-pages="totalPages"
      />
    </template>

    <!-- Add Email Domain Modal -->
    <UiModal v-model="showAddModal" title="Add Email Domain" size="md">
      <UiForm :error="createError" @submit="submitCreate">
        <UiInput
          v-model="newDomain"
          label="Domain Name"
          placeholder="example.com"
          type="text"
          required
          :disabled="creating"
        />
        <template #actions>
          <button
            type="button"
            class="px-4 py-2 rounded-lg border border-gray-200 dark:border-white/10 text-gray-500 dark:text-gray-400 text-sm hover:text-gray-900 dark:hover:text-white transition-colors"
            :disabled="creating"
            @click="closeModal"
          >
            Cancel
          </button>
          <UiButton type="submit" :loading="creating">
            Add Domain
          </UiButton>
        </template>
      </UiForm>
    </UiModal>
  </div>
</template>

<script setup lang="ts">
import { Mail, Plus } from 'lucide-vue-next'

definePageMeta({ layout: 'client', middleware: 'client-auth' })

// ── Data fetching ─────────────────────────────────────────────────────────────

interface EmailDomain {
  id: string
  domainName: string
  status: string
  mailboxCount: number
  maxMailboxes: number
  createdAt: string
}

const { data: emailData, pending, refresh } = await useApi<EmailDomain[]>('/api/portal/client/email', {
  default: () => []
})

const domains = computed(() => emailData.value ?? [])

// ── Filters / pagination ───────────────────────────────────────────────────────

const search  = ref('')
const page    = ref(1)
const perPage = ref(10)

watch(search, () => { page.value = 1 })

const filtered = computed(() => {
  const q = search.value.trim().toLowerCase()
  if (!q) return domains.value
  return domains.value.filter(d => d.domainName.toLowerCase().includes(q))
})

const totalPages = computed(() => Math.max(1, Math.ceil(filtered.value.length / perPage.value)))
const paged      = computed(() => filtered.value.slice((page.value - 1) * perPage.value, page.value * perPage.value))
const pageFrom   = computed(() => filtered.value.length === 0 ? 0 : (page.value - 1) * perPage.value + 1)
const pageTo     = computed(() => Math.min(page.value * perPage.value, filtered.value.length))

// ── Date helper ───────────────────────────────────────────────────────────────

function formatDate(d: string): string {
  if (!d || isNaN(new Date(d).getTime())) return '—'
  return new Date(d).toLocaleDateString()
}

// ── Add domain modal ──────────────────────────────────────────────────────────

const showAddModal = ref(false)
const newDomain    = ref('')
const creating     = ref(false)
const createError  = ref('')

function closeModal() {
  showAddModal.value = false
  newDomain.value    = ''
  createError.value  = ''
}

watch(showAddModal, (open) => {
  if (!open) {
    newDomain.value   = ''
    createError.value = ''
  }
})

async function submitCreate() {
  const name = newDomain.value.trim()
  if (!name) return

  creating.value    = true
  createError.value = ''

  try {
    const result = await apiFetch<{ id: number }>('/api/portal/client/email/create', {
      method: 'POST',
      body: { domain: name }
    })
    closeModal()
    await navigateTo(`/client/email/${result.id}`)
  } catch (err: unknown) {
    const e = err as { data?: { statusMessage?: string }; statusMessage?: string }
    createError.value = e?.data?.statusMessage ?? e?.statusMessage ?? 'Failed to add email domain. Please try again.'
  } finally {
    creating.value = false
  }
}
</script>
