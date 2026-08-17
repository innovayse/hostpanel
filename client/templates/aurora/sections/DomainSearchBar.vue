<template>
  <div>
    <form
      class="mx-auto flex max-w-[720px] flex-wrap justify-center gap-2.5"
      @submit.prevent="emit('search', query)"
    >
      <input
        v-model="query"
        type="text"
        :disabled="!hasZones"
        :placeholder="t('aurora.domainSearch.placeholder')"
        class="min-w-[200px] flex-[1_1_260px] rounded-xl border border-line2 bg-input px-[18px] py-4 text-base text-tx outline-none focus:border-ac1 disabled:opacity-50"
      >
      <button
        type="submit"
        :disabled="!hasZones"
        class="rounded-xl bg-brand px-[26px] py-4 text-base font-bold text-[#08090F] hover:brightness-110 disabled:cursor-not-allowed disabled:opacity-40"
      >{{ t('aurora.domainSearch.cta') }}</button>
    </form>

    <!--
      Categories come from the registrar data, so the filter reflects the zones an
      operator actually sells rather than a fixed list. Hidden when the backend
      supplies none, which is also when there is nothing to filter.
    -->
    <div v-if="categories.length > 1" class="mt-4 flex flex-wrap justify-center gap-2">
      <button
        v-for="entry in categories"
        :key="entry"
        type="button"
        class="rounded-full px-3.5 py-2 text-[13px] font-semibold"
        :class="entry === active ? 'bg-brand text-[#08090F]' : 'border border-line2 text-mut'"
        @click="emit('update:category', entry)"
      >{{ entry === ALL_CATEGORY ? t('aurora.tldTable.all') : entry }}</button>
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * aurora domain search bar — the wide field and category filter that head the
 * domains page. Presentation only: it emits the term and the chosen category.
 */
import { ALL_CATEGORY } from '~/templates/aurora/types'

withDefaults(defineProps<{
  /** Category names offered by the filter, including the "all" sentinel. */
  categories?: string[]
  /** Currently selected category. */
  active?: string
  /** False when no extension is priced; the field then explains itself upstream. */
  hasZones?: boolean
  initialQuery?: string
}>(), { categories: () => [], active: ALL_CATEGORY, hasZones: true, initialQuery: '' })

const emit = defineEmits<{
  search: [term: string]
  'update:category': [category: string]
}>()

const { t } = useI18n()
const query = ref('')
</script>
