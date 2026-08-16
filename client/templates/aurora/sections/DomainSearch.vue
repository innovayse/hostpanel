<template>
  <div class="relative min-w-0 rounded-[22px] border border-line2 bg-card p-7 shadow-panel">
    <div class="text-[15px] font-semibold text-tx">{{ t('aurora.domainSearch.title') }}</div>

    <form class="mt-3.5 flex gap-2.5" @submit.prevent="emit('search', query)">
      <input
        v-model="query"
        type="text"
        :disabled="!hasZones"
        :placeholder="t('aurora.domainSearch.placeholder')"
        class="min-w-0 flex-1 rounded-xl border border-line2 bg-input px-4 py-[15px] text-base text-tx outline-none focus:border-ac1 disabled:opacity-50"
      >
      <button
        type="submit"
        :disabled="!hasZones"
        class="rounded-xl bg-brand px-[22px] py-[15px] text-[15px] font-bold text-[#08090F] hover:brightness-110 disabled:cursor-not-allowed disabled:opacity-40"
      >{{ t('aurora.domainSearch.cta') }}</button>
    </form>

    <!--
      Starting prices for the extensions the search actually covers, so the card
      answers "how much?" before the visitor types anything. Rows the operator
      has not priced never reach here, and the row disappears entirely with them.
    -->
    <ul v-if="visibleHints.length > 0" class="mt-3 flex flex-wrap gap-x-4 gap-y-1.5">
      <li v-for="hint in visibleHints" :key="hint.tld" class="flex items-baseline gap-1.5 text-[13px]">
        <span class="font-mono text-tx2">{{ hint.tld }}</span>
        <span class="font-semibold text-mut">{{ hint.register }}</span>
      </li>
    </ul>

    <!--
      The reserved height only applies once a search is under way, so the results
      area stops jumping between rows — before the first search it would just be
      a block of empty card.
    -->
    <div
      class="mt-5 flex flex-col gap-2.5 border-t border-line pt-[18px]"
      :class="(pending || results.length) ? 'min-h-[236px]' : ''"
    >
      <!--
        With no priced extensions there is nothing to search, and a button that
        appears to work but returns nothing reads as a broken site. Say why.
      -->
      <p v-if="!hasZones" class="text-sm text-mut2">{{ t('aurora.domainSearch.noZones') }}</p>
      <p v-else-if="pending" class="text-sm text-mut2">{{ t('aurora.domainSearch.searching') }}</p>
      <p v-else-if="results.length === 0" class="text-sm text-mut2">
        {{ t('aurora.domainSearch.empty') }}
      </p>

      <div
        v-for="result in results"
        v-else
        :key="result.name"
        class="flex flex-wrap items-center justify-between gap-4 rounded-xl border border-line bg-surf px-[15px] py-[13px]"
      >
        <div class="flex min-w-0 items-center gap-3">
          <span class="h-2 w-2 rounded-full" :class="dotClass(result.status)" />
          <span class="truncate font-mono text-[15px] text-tx">{{ result.name }}</span>
        </div>
        <div class="flex flex-shrink-0 items-center gap-3.5">
          <span class="text-[13px]" :class="labelClass(result.status)">
            {{ t(`aurora.domainSearch.${result.status}`) }}
          </span>
          <span class="text-sm font-semibold text-mut">{{ result.price }}</span>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * aurora domain search card.
 *
 * Presentation only: it renders the results it is given and emits the term the
 * visitor typed. The lookup itself belongs to pages/index.vue.
 */
import type { DomainResult } from '~/templates/aurora/types'
import type { TldPriceRow } from '~/composables/useDomainLookup'

const props = withDefaults(defineProps<{
  results?: DomainResult[]
  pending?: boolean
  initialQuery?: string
  /** False when the operator has priced no extensions; the form then explains itself. */
  hasZones?: boolean
  /** Extensions the search covers, for the starting-price row. */
  priceHints?: TldPriceRow[]
}>(), { results: () => [], pending: false, initialQuery: '', hasZones: true, priceHints: () => [] })

const emit = defineEmits<{ search: [term: string] }>()

const { t } = useI18n()
const query = ref(props.initialQuery)

/** Hints worth showing: an extension with no price would read as free. */
const visibleHints = computed(() => props.priceHints.filter(hint => hint.registerAmount > 0))

/**
 * Status dot colour. `unknown` is muted rather than red — nothing is wrong with
 * the name, the check itself did not complete.
 *
 * @param status Availability state of the row.
 */
const dotClass = (status: DomainResult['status']) =>
  status === 'available' ? 'bg-ok' : status === 'taken' ? 'bg-danger' : 'bg-mut2'

/**
 * Status label colour, matching {@link dotClass}.
 *
 * @param status Availability state of the row.
 */
const labelClass = (status: DomainResult['status']) =>
  status === 'available' ? 'text-ok' : status === 'taken' ? 'text-danger' : 'text-mut2'
</script>
