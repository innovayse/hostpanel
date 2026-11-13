<template>
  <UiStack as="section" gap="none" class="relative pb-[clamp(56px,8vw,84px)]">
    <UiStack gap="xs" class="w-full">
      <UiText v-if="!hasZones" tone="inherit" class="text-center text-[15px] text-mut2" data-testid="domain-results-no-zones">
        {{ t('aurora.domainSearch.noZones') }}
      </UiText>
      <UiSkeleton v-else-if="pending" :lines="3" data-testid="domain-results-pending" />
      <UiText v-else-if="results.length === 0" tone="inherit" class="text-center text-[15px] text-mut2" data-testid="domain-results-empty">
        {{ t('aurora.domainSearch.empty') }}
      </UiText>

      <template v-else>
        <UiCard
          v-for="result in results"
          :key="result.name"
          tone="bare"
          padding="none"
          :data-testid="`domain-result-${result.name}`"
          :data-status="result.status"
          class="bg-surf px-5 py-[18px]"
          :class="result.status === DomainResultStatus.Available ? 'border-ok' : 'border-line'"
        >
          <UiStack direction="row" wrap align="center" justify="between" gap="md">
            <UiStack direction="row" align="center" gap="md" class="min-w-0">
              <span class="h-[9px] w-[9px] shrink-0 rounded-full" :class="AVAILABILITY_DOT_CLASSES[result.status]" />
              <UiText as="span" size="inherit" tone="inherit" class="break-all font-mono text-[clamp(15px,2vw,18px)] text-tx">
                {{ result.name }}
              </UiText>
              <UiBadge size="sm" rounded :variant="AVAILABILITY_TONES[result.status]">
                {{ t(`aurora.domainSearch.${result.status}`) }}
              </UiBadge>
            </UiStack>

            <UiStack direction="row" align="center" gap="lg">
              <UiText as="span" size="inherit" tone="inherit" weight="bold" class="text-[17px] text-tx">{{ result.price }}</UiText>

              <!--
                Available names go to the cart; taken ones offer transfer instead,
                which is the only thing a visitor can actually do with them here.
              -->
              <UiButton
                v-if="result.status === DomainResultStatus.Available"
                variant="bare"
                :data-testid="`domain-add-${result.name}`"
                :disabled="inCart.includes(result.name)"
                :class="[BRAND_BUTTON, 'px-[18px] py-[11px] text-sm disabled:opacity-50']"
                @click="emit('add', result.name)"
              >
                {{ inCart.includes(result.name) ? t('aurora.domainSearch.inCart') : t('aurora.domainSearch.add') }}
              </UiButton>
              <UiButton
                v-else-if="result.status === DomainResultStatus.Taken"
                variant="bare"
                :to="`${localePath('/domains/transfer')}?domain=${result.name}`"
                :data-testid="`domain-transfer-${result.name}`"
                :class="[LINE_BUTTON, 'px-[18px] py-[11px] text-sm font-bold']"
              >
                {{ t('aurora.domainSearch.transfer') }}
              </UiButton>
            </UiStack>
          </UiStack>
        </UiCard>
      </template>
    </UiStack>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * aurora domain results: one full-width row per checked extension.
 *
 * Presentation only: adding to the cart is emitted upward, because a template
 * never writes to a store.
 */
import { BRAND_BUTTON, LINE_BUTTON } from '~/templates/aurora/buttons'
import { AVAILABILITY_DOT_CLASSES, AVAILABILITY_TONES } from '~/utils/domainStatus'
import { DomainResultStatus } from '~/types/domainresultstatus'
import type { DomainResult } from '~/types/domainresult'

withDefaults(defineProps<{
  /** Lookup results, one row per checked extension. */
  results?: DomainResult[]
  /** Whether a lookup is under way. */
  pending?: boolean
  /** False when the operator has priced no extensions. */
  hasZones?: boolean
  /** Domains already in the cart, so their button reads as done rather than repeating. */
  inCart?: string[]
}>(), { results: () => [], pending: false, hasZones: true, inCart: () => [] })

/** Events this component raises. */
const emit = defineEmits<{
  /** The visitor added an available domain to the cart. */
  add: [domain: string]
}>()

/** Translator for the active locale. */
const { t } = useI18n()
/** Prefixes a path with the active locale. */
const localePath = useLocalePath()
</script>
