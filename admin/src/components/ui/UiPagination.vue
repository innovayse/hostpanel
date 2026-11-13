<script setup lang="ts">
/**
 * Page switcher for a paged list.
 *
 * Shows the "x to y of n" range, an optional rows-per-page select, arrow buttons for the previous
 * and next page and a window of page numbers with gaps (see `pageWindow`). On a phone the numbers give
 * way to a single "Page x of n" caption between the arrows. The parent owns the current page
 * (`v-model:page`) and the page size (`v-model:page-size`) and loads the data; this component only
 * reports the choice. Renders nothing for a single page, unless a rows-per-page select is offered and
 * the list is longer than its smallest option (so the size can still be lowered). While `loading` it keeps its
 * layout (a bar stands in for the range, every control is disabled), so the page does not change shape when the
 * data arrives.
 */
import { computed, useId } from 'vue'
import { useI18n } from 'vue-i18n'
import UiButton from '@/components/ui/UiButton.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'
import { Icon } from '@/lib/icons'
import { pageCount as countPages, pageEntryKey, pageFirstRow, pageLastRow, pageWindow } from '@/lib/paging'
import { UiButtonVariant } from '@/utils/ui/button'

const { t } = useI18n()

/** Props for UiPagination. */
const props = withDefaults(defineProps<{
  /** Current page, starting at 1 (two-way bound with `v-model:page`). */
  page: number
  /** Total number of items. */
  total: number
  /** Items per page (two-way bound with `v-model:page-size`). */
  pageSize: number
  /** Page sizes the user can pick from; the rows-per-page select is shown only when this is set. */
  pageSizeOptions?: number[]
  /** Keeps the layout and disables every control while the list loads. */
  loading?: boolean
  /** Prefix of the `data-testid`s (`<prefix>-prev`, `<prefix>-next`, `<prefix>-range`, `<prefix>-page-<n>`, `<prefix>-page-size`). */
  testid?: string
}>(), {
  pageSizeOptions: undefined,
  loading: false,
  testid: 'pagination',
})

const emit = defineEmits<{
  /** Emitted with the page the user chose. */
  'update:page': [page: number]
  /** Emitted with the page size the user chose; the parent returns to the first page. */
  'update:pageSize': [pageSize: number]
}>()

/** Id tying the "Rows per page" caption to the select's trigger. */
const sizeId = useId()

/** Number of pages. */
const pageCount = computed<number>(() => countPages(props.total, props.pageSize))

/** Page numbers to show; `null` marks a gap between non-adjacent numbers. */
const entries = computed<(number | null)[]>(() => pageWindow(props.page, pageCount.value))

/** Whether the pager is shown: while loading, more than one page, or a size select that can still shrink the page. */
const visible = computed<boolean>(() => {
  if (props.loading) {
    return true
  }
  if (pageCount.value > 1) {
    return true
  }
  return props.pageSizeOptions !== undefined && props.total > Math.min(...props.pageSizeOptions)
})

/** The "x to y of n" line. */
const rangeText = computed<string>(() => t('ui.pagination.range', {
  from: pageFirstRow(props.page, props.pageSize),
  to: pageLastRow(props.page, props.pageSize, props.total),
  total: props.total,
}))

/** Options of the rows-per-page select. */
const sizeOptions = computed<{ value: number; label: string }[]>(() =>
  (props.pageSizeOptions ?? []).map(size => ({ value: size, label: String(size) })),
)

/**
 * Reports a page choice if it differs from the current one and is in range.
 *
 * @param next - Requested page.
 */
const go = (next: number): void => {
  if (next !== props.page && next >= 1 && next <= pageCount.value) {
    emit('update:page', next)
  }
}
</script>

<template>
  <div v-if="visible" class="flex flex-wrap items-center justify-between gap-x-6 gap-y-2">
    <UiSkeletonBar v-if="loading" class="h-3 w-36 rounded" :data-testid="`${testid}-range`" aria-hidden="true" />
    <p v-else class="text-xs tabular-nums text-text-muted" :data-testid="`${testid}-range`">
      {{ rangeText }}
    </p>
    <div v-if="pageSizeOptions" class="hidden items-center gap-2 text-xs text-text-muted sm:flex">
      <label :for="sizeId">{{ t('ui.pagination.rowsPerPage') }}</label>
      <UiSelect
        :id="sizeId"
        compact
        :disabled="loading"
        :model-value="pageSize"
        :options="sizeOptions"
        :placeholder="t('ui.pagination.rowsPerPage')"
        :data-testid="`${testid}-page-size`"
        @update:model-value="emit('update:pageSize', $event)"
      />
    </div>
    <nav class="flex items-center gap-1" :aria-label="t('ui.pagination.navigation')">
      <UiIconButton
        size="sm"
        :label="t('ui.pagination.previous')"
        :disabled="loading || page <= 1"
        :data-testid="`${testid}-prev`"
        @click="go(page - 1)"
      >
        <UiIcon :markup="Icon.Back" size="sm" />
      </UiIconButton>
      <!-- A phone has no room for the numbers: one caption stands between the arrows. -->
      <span class="px-2 text-xs tabular-nums text-text-secondary sm:hidden">
        {{ t('ui.pagination.pageOf', { page, count: pageCount }) }}
      </span>
      <div class="hidden items-center gap-1 sm:flex">
        <template v-for="(entry, index) in entries" :key="pageEntryKey(entries, index, page)">
          <span v-if="entry === null" class="px-1 text-text-muted" aria-hidden="true">…</span>
          <UiButton
            v-else
            :variant="entry === page ? UiButtonVariant.Primary : UiButtonVariant.Ghost"
            size="sm"
            class="min-w-8 tabular-nums"
            :disabled="loading"
            :aria-current="entry === page ? 'page' : undefined"
            :aria-label="`${t('ui.pagination.page')} ${entry}`"
            :data-testid="`${testid}-page-${entry}`"
            @click="go(entry)"
          >
            {{ entry }}
          </UiButton>
        </template>
      </div>
      <UiIconButton
        size="sm"
        :label="t('ui.pagination.next')"
        :disabled="loading || page >= pageCount"
        :data-testid="`${testid}-next`"
        @click="go(page + 1)"
      >
        <UiIcon :markup="Icon.Chevron" size="sm" />
      </UiIconButton>
    </nav>
  </div>
</template>
