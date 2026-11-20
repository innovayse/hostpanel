<script setup lang="ts">
/**
 * Table head drawn from the columns of a skeleton schema.
 *
 * A list screen and the skeleton of that screen share one column definition (`<Screen>.schema.ts`), so the real header
 * and the placeholder one cannot differ: each column becomes a `UiTableHeader` with the caption its locale key names, the
 * width, the breakpoint and the card role the schema sets. An actions column that names no caption gets the shared one
 * (the header shows it to assistive technology only). The data cells stay the screen's own markup.
 */
import { useI18n } from 'vue-i18n'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import type { SkeletonColumn } from '@/utils/ui/skeletonSchema'

/** Resolves the locale keys of the column captions. */
const { t } = useI18n()

/** Props for UiSchemaTableHead. */
defineProps<{
  /** The columns of the table, in display order. */
  columns: readonly SkeletonColumn[]
}>()
</script>

<template>
  <UiTableHead>
    <UiTableRow>
      <UiTableHeader
        v-for="column in columns"
        :key="column.key"
        :align="column.align"
        :size="column.size"
        :hide-below="column.hideBelow"
        :card-role="column.cardRole"
        :numeric="column.numeric"
        :actions="column.actions"
      >
        <template v-if="column.label">{{ t(column.label) }}</template>
        <template v-else-if="column.actions">{{ t('shared.actions') }}</template>
      </UiTableHeader>
    </UiTableRow>
  </UiTableHead>
</template>
