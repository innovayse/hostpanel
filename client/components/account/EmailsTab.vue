<template>
  <UiStack gap="md" data-testid="account-emails-tab">
    <UiStack direction="row" align="center" justify="between" gap="sm" wrap>
      <UiText size="sm" tone="muted">
        {{ $t('ui.pagination.showing', { from, to, total: filtered.length }) }}
      </UiText>
      <UiSearchInput
        v-model="search"
        :placeholder="$t('client.emails.search')"
        class="sm:w-64"
        testid="account-emails-search"
      />
    </UiStack>

    <UiTable
      scrollable
      :label="$t('client.emails.colSubject')"
      :columns="3"
      :loading="emailsLoading"
      :empty="!filtered.length"
      :empty-text="search ? $t('client.emails.noResults') : $t('client.emails.empty')"
      :empty-icon="Mail"
    >
      <template #head>
        <UiTableHead>
          <UiTableRow :hoverable="false">
            <UiTableTh>{{ $t('client.emails.colDate') }}</UiTableTh>
            <UiTableTh>{{ $t('client.emails.colSubject') }}</UiTableTh>
            <UiTableTh actions />
          </UiTableRow>
        </UiTableHead>
      </template>
      <UiTableBody>
        <UiTableRow v-for="email in paged" :key="email.id" :data-testid="`account-email-${email.id}`">
          <UiTableTd class="text-gray-500 dark:text-gray-400 whitespace-nowrap text-sm">{{ formatDateTime(email.sentAt) }}</UiTableTd>
          <UiTableTd class="text-gray-900 dark:text-white font-medium">{{ email.subject }}</UiTableTd>
          <UiTableTd align="right">
            <UiButton data-testid="emails-view" size="sm" variant="subtle" :to="clientEmailPath(email.id)">
              {{ $t('common.view') }}
            </UiButton>
          </UiTableTd>
        </UiTableRow>
      </UiTableBody>
    </UiTable>

    <UiPagination
      v-if="!emailsLoading && filtered.length > 0"
      v-model="page"
      v-model:per-page="perPage"
      :total-pages="totalPages"
    />
  </UiStack>
</template>

<script setup lang="ts">
/**
 * EmailsTab. The "Email History" tab: a searchable, paged list of the mail sent to the account.
 *
 * The list lives in the account store; search and paging are view state of this tab only.
 *
 * @module components/account/EmailsTab
 */

import { Mail } from 'lucide-vue-next'
import { storeToRefs } from 'pinia'
import { clientEmailPath } from '~/constants/clientRoutes'
import { useAccountStore } from '~/stores/account'
import { formatDateTime } from '~/utils/formatDate'

/** The mail sent to the account and whether it is being read. */
const { emails, emailsLoading } = storeToRefs(useAccountStore())

/** The subject filter typed so far. */
const search = ref('')

/** Rows per page. */
const perPage = ref(10)

/** The current page, 1-based. */
const page = ref(1)

/** The emails whose subject contains the filter, case-insensitively. */
const filtered = computed(() => {
  const query = search.value.trim().toLowerCase()
  return query ? emails.value.filter(email => email.subject.toLowerCase().includes(query)) : emails.value
})

/** How many pages the filtered list needs; never fewer than one. */
const totalPages = computed<number>(() => Math.max(1, Math.ceil(filtered.value.length / perPage.value)))

/** The rows of the current page. */
const paged = computed(() => {
  const start = (page.value - 1) * perPage.value
  return filtered.value.slice(start, start + perPage.value)
})

/** 1-based index of the first row on the page, or 0 when nothing matches. */
const from = computed<number>(() => filtered.value.length === 0 ? 0 : (page.value - 1) * perPage.value + 1)

/** 1-based index of the last row on the page. */
const to = computed<number>(() => Math.min(page.value * perPage.value, filtered.value.length))

// A new search starts from the first page.
watch(search, () => {
  page.value = 1
})
</script>
