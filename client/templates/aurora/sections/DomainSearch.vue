<template>
  <UiCard
    tone="bare"
    padding="xl"
    data-testid="home-domain-search"
    class="relative min-w-0 border-line2 bg-card shadow-panel"
  >
    <UiText as="span" size="inherit" tone="inherit" weight="bold" class="font-display text-[20px] -tracking-[0.01em] text-tx">
      {{ t('aurora.domainSearch.title') }}
    </UiText>

    <UiForm data-testid="domain-search-form" spacing="none" class="mt-4" @submit="emit('search', query)">
      <UiStack direction="row" align="end" gap="xs">
        <UiInput
          v-model="query"
          testid="home-domain-search-input"
          :disabled="!hasZones"
          :placeholder="t('aurora.domainSearch.placeholder')"
          class="min-w-0 flex-1"
        />
        <UiButton
          type="submit"
          variant="bare"
          :disabled="!hasZones"
          data-testid="home-domain-search-submit"
          :class="[BRAND_BUTTON, 'px-[22px] py-[15px] text-[15px]']"
        >
          {{ t('aurora.domainSearch.cta') }}
        </UiButton>
      </UiStack>
    </UiForm>

    <!--
      Starting prices for the extensions the search actually covers, so the card
      answers "how much?" before the visitor types anything. Rows the operator
      has not priced never reach here, and the row disappears entirely with them.
    -->
    <UiList v-if="visibleHints.length > 0" list-style="bare" class="mt-3 flex flex-wrap gap-x-4 gap-y-1.5" data-testid="price-hints">
      <UiListItem v-for="hint in visibleHints" :key="hint.tld" class="flex items-baseline gap-1.5 text-[13px]">
        <UiText as="span" size="inherit" tone="inherit" class="font-mono text-tx2">{{ hint.tld }}</UiText>
        <UiText as="span" size="inherit" tone="inherit" weight="semibold" class="text-mut">{{ hint.register }}</UiText>
      </UiListItem>
    </UiList>

    <!--
      The reserved height only applies once a search is under way, so the results
      area stops jumping between rows: before the first search it would just be
      a block of empty card.
    -->
    <UiStack
      gap="xs"
      class="mt-5 border-t border-line pt-[18px]"
      :class="(pending || results.length) ? 'min-h-[236px]' : ''"
    >
      <!--
        With no priced extensions there is nothing to search, and a button that
        appears to work but returns nothing reads as a broken site. Say why.
      -->
      <UiText v-if="!hasZones" size="sm" tone="inherit" class="text-mut2">{{ t('aurora.domainSearch.noZones') }}</UiText>
      <UiSkeleton v-else-if="pending" :lines="3" data-testid="domain-search-pending" />
      <UiText v-else-if="results.length === 0" size="sm" tone="inherit" class="text-mut2">
        {{ t('aurora.domainSearch.empty') }}
      </UiText>

      <template v-else>
        <UiCard
          v-for="result in results"
          :key="result.name"
          tone="bare"
          padding="none"
          :data-testid="`home-domain-result-${result.name}`"
          :data-status="result.status"
          class="border-line bg-surf px-[15px] py-[13px]"
        >
          <UiStack direction="row" wrap align="center" justify="between" gap="md">
            <UiStack direction="row" align="center" gap="sm" class="min-w-0">
              <span class="h-2 w-2 shrink-0 rounded-full" :class="AVAILABILITY_DOT_CLASSES[result.status]" />
              <UiText as="span" size="inherit" tone="inherit" truncate class="font-mono text-[15px] text-tx">{{ result.name }}</UiText>
            </UiStack>
            <UiStack direction="row" align="center" gap="sm" class="shrink-0">
              <UiText as="span" size="inherit" tone="inherit" class="text-[13px]" :class="AVAILABILITY_LABEL_CLASSES[result.status]">
                {{ t(`aurora.domainSearch.${result.status}`) }}
              </UiText>
              <UiText as="span" size="sm" tone="inherit" weight="semibold" class="text-mut">{{ result.price }}</UiText>
            </UiStack>
          </UiStack>
        </UiCard>
      </template>
    </UiStack>
  </UiCard>
</template>

<script setup lang="ts">
/**
 * aurora domain search card.
 *
 * Presentation only: it renders the results it is given and emits the term the
 * visitor typed. The lookup itself belongs to pages/index.vue.
 */
import { BRAND_BUTTON } from '~/templates/aurora/buttons'
import { AVAILABILITY_DOT_CLASSES, AVAILABILITY_LABEL_CLASSES } from '~/utils/domainStatus'
import type { DomainResult } from '~/types/domainresult'
import type { TldPriceRow } from '~/types/tldpricerow'

/** Props with their defaults applied. */
const props = withDefaults(defineProps<{
  /** Lookup results, one row per checked extension. */
  results?: DomainResult[]
  /** Whether a lookup is under way. */
  pending?: boolean
  /** Text the field starts with. */
  initialQuery?: string
  /** False when the operator has priced no extensions; the form then explains itself. */
  hasZones?: boolean
  /** Extensions the search covers, for the starting-price row. */
  priceHints?: TldPriceRow[]
}>(), { results: () => [], pending: false, initialQuery: '', hasZones: true, priceHints: () => [] })

/** Events this component raises. */
const emit = defineEmits<{
  /** The visitor submitted a term. */
  search: [term: string]
}>()

/** Translator for the active locale. */
const { t } = useI18n()

/** What the visitor has typed (v-model of the field). */
const query = ref(props.initialQuery)

/** Hints worth showing: an extension with no price would read as free. */
const visibleHints = computed(() => props.priceHints.filter(hint => hint.registerAmount > 0))
</script>
