<template>
  <section class="relative px-[clamp(20px,5vw,48px)] pb-[clamp(56px,8vw,84px)]">
    <div class="mx-auto flex max-w-[980px] flex-col gap-2.5">
      <p v-if="!hasZones" class="text-center text-[15px] text-mut2">
        {{ t('aurora.domainSearch.noZones') }}
      </p>
      <p v-else-if="pending" class="text-center text-[15px] text-mut2">
        {{ t('aurora.domainSearch.searching') }}
      </p>
      <p v-else-if="results.length === 0" class="text-center text-[15px] text-mut2">
        {{ t('aurora.domainSearch.empty') }}
      </p>

      <div
        v-for="result in results"
        v-else
        :key="result.name"
        class="flex flex-wrap items-center justify-between gap-3.5 rounded-[14px] border px-5 py-[18px]"
        :class="result.status === 'available' ? 'border-ok bg-surf' : 'border-line bg-surf'"
      >
        <div class="flex min-w-0 items-center gap-3.5">
          <span class="h-[9px] w-[9px] shrink-0 rounded-full" :class="dotClass(result.status)" />
          <span class="break-all font-mono text-[clamp(15px,2vw,18px)] text-tx">{{ result.name }}</span>
          <span
            class="whitespace-nowrap rounded-full border border-line2 px-2.5 py-[3px] text-xs"
            :class="labelClass(result.status)"
          >{{ t(`aurora.domainSearch.${result.status}`) }}</span>
        </div>

        <div class="flex items-center gap-[18px]">
          <span class="text-[17px] font-bold text-tx">{{ result.price }}</span>

          <!--
            Available names go to the cart; taken ones offer transfer instead,
            which is the only thing a visitor can actually do with them here.
          -->
          <button
            v-if="result.status === 'available'"
            type="button"
            :disabled="inCart.includes(result.name)"
            class="rounded-[10px] border border-line2 bg-brand px-[18px] py-[11px] text-sm font-bold text-[#08090F] hover:brightness-110 disabled:cursor-not-allowed disabled:opacity-50"
            @click="emit('add', result.name)"
          >
            {{ inCart.includes(result.name) ? t('aurora.domainSearch.inCart') : t('aurora.domainSearch.add') }}
          </button>
          <NuxtLink
            v-else-if="result.status === 'taken'"
            :to="`${localePath('/domains/transfer')}?domain=${result.name}`"
            class="rounded-[10px] border border-line2 px-[18px] py-[11px] text-sm font-bold text-tx hover:border-ac1 hover:text-ac1"
          >{{ t('aurora.domainSearch.transfer') }}</NuxtLink>
        </div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
/**
 * aurora domain results — one full-width row per checked extension.
 *
 * Presentation only: adding to the cart is emitted upward, because a template
 * never writes to a store.
 */
import type { DomainResult } from '~/templates/aurora/types'

withDefaults(defineProps<{
  results?: DomainResult[]
  pending?: boolean
  hasZones?: boolean
  /** Domains already in the cart, so their button reads as done rather than repeating. */
  inCart?: string[]
}>(), { results: () => [], pending: false, hasZones: true, inCart: () => [] })

const emit = defineEmits<{ add: [domain: string] }>()

const { t } = useI18n()
const localePath = useLocalePath()

/**
 * Status dot colour. `unknown` is muted rather than red — the name is fine, the
 * check did not complete.
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
