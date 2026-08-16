<template>
  <section class="relative px-[clamp(20px,5vw,48px)] pb-[clamp(56px,8vw,84px)]">
    <h2 class="mb-2 font-display text-[clamp(25px,3.6vw,36px)] font-bold -tracking-[0.02em] text-tx">
      {{ t('aurora.tldTable.title') }}
    </h2>
    <p class="mb-7 text-base text-mut">{{ t('aurora.tldTable.lead') }}</p>

    <div v-if="categories.length > 1" class="mb-6 flex flex-wrap gap-2">
      <button
        v-for="category in categories"
        :key="category"
        type="button"
        class="rounded-full px-3.5 py-2 text-[13px] font-semibold"
        :class="category === active ? 'bg-brand text-[#08090F]' : 'border border-line2 text-mut'"
        @click="active = category"
      >{{ category === ALL ? t('aurora.tldTable.all') : category }}</button>
    </div>

    <div class="overflow-x-auto rounded-[18px] border border-line bg-surf">
      <table class="w-full min-w-[640px] border-collapse">
        <thead>
          <tr class="border-b border-line text-left text-[13px] uppercase tracking-[0.06em] text-mut2">
            <th class="px-[22px] py-4 font-normal">{{ t('aurora.tldTable.zone') }}</th>
            <th class="px-[22px] py-4 font-normal">{{ t('aurora.tldTable.register') }}</th>
            <th class="px-[22px] py-4 font-normal">{{ t('aurora.tldTable.renew') }}</th>
            <th class="px-[22px] py-4 font-normal">{{ t('aurora.tldTable.transfer') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="row in visibleRows" :key="row.tld" class="border-b border-line last:border-0">
            <td class="px-[22px] py-[15px] font-mono text-base text-tx">{{ row.tld }}</td>
            <td class="px-[22px] py-[15px] text-[15px] font-semibold text-tx">{{ row.register }}</td>
            <td class="px-[22px] py-[15px] text-[15px] text-tx2">{{ row.renew }}</td>
            <td class="px-[22px] py-[15px] text-[15px] text-tx2">{{ row.transfer }}</td>
          </tr>
          <tr v-if="visibleRows.length === 0">
            <td colspan="4" class="px-[22px] py-8 text-center text-[15px] text-mut2">
              {{ t('aurora.tldTable.empty') }}
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </section>
</template>

<script setup lang="ts">
/**
 * aurora TLD price table.
 *
 * Rows and their categories come from the API, so the filter reflects whatever
 * the operator's registrar actually offers rather than a hard-coded list.
 */
import type { TldPriceRow } from '~/composables/useDomainLookup'

const props = withDefaults(defineProps<{ rows?: TldPriceRow[] }>(), { rows: () => [] })

const { t } = useI18n()

/** Sentinel for the unfiltered view; never collides with a real category name. */
const ALL = '__all__'

const active = ref(ALL)

const categories = computed(() => {
  const found = new Set<string>()
  for (const row of props.rows) for (const category of row.categories) found.add(category)
  return [ALL, ...[...found].sort()]
})

const visibleRows = computed(() =>
  active.value === ALL
    ? props.rows
    : props.rows.filter(row => row.categories.includes(active.value)))
</script>
