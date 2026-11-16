<script setup lang="ts">
/**
 * The "Rate source" choice of the currencies page: which bank the exchange rates are taken from.
 *
 * The row is the `billing.exchange_rates.provider` setting, saved through the same draft-then-save
 * path as every other settings row. The choice applies to the next refresh, by the button and by the
 * daily job alike; a row the server never seeded is replaced by a notice, because the panel cannot
 * create settings.
 */
import { computed, toRef } from 'vue'
import { useI18n } from 'vue-i18n'
import { useSettingDraft } from '@/composables/useSettingDraft'
import SettingOutcome from '@/components/settings/SettingOutcome.vue'
import SettingSaveButton from '@/components/settings/SettingSaveButton.vue'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { ExchangeRateProvider } from '@/types/currency'
import { BillingSettingKey } from '@/types/setting'
import type { Setting, SettingSave } from '@/types/setting'

/** Translation function. */
const { t } = useI18n()

/** Props for ExchangeRateProviderCard. */
const props = defineProps<{
  /** All system settings, as loaded from the backend. */
  settings: Setting[]
  /** Persists one row and answers how it went; the card shows the outcome next to the select. */
  save: SettingSave
}>()

/** The settings list as a ref, which is what the draft composable watches. */
const settings = toRef(props, 'settings')

/** Draft of the row; an empty row reads as the national bank, the server's default. */
const draft = useSettingDraft(settings, BillingSettingKey.RateProvider, ExchangeRateProvider.Cba)

/** Options of the select: one per bank. */
const options = computed<{ value: ExchangeRateProvider; label: string }[]>(() =>
  Object.values(ExchangeRateProvider).map(value => ({ value, label: t(`currencies.rateProvider.options.${value}`) })),
)

/**
 * The draft as a provider. The draft holds free text, so a stored value that is no provider reads as the
 * national bank, the server's default; a pick writes the provider's own string back.
 */
const provider = computed<ExchangeRateProvider>({
  /**
   * Reads the draft as a provider.
   *
   * @returns The provider the draft holds, or the national bank when it holds none.
   */
  get: () => Object.values(ExchangeRateProvider).find(candidate => candidate === draft.value.value) ?? ExchangeRateProvider.Cba,
  /**
   * Writes the picked provider back into the draft as its own string.
   *
   * @param next - The provider the operator picked.
   */
  set: (next) => {
    draft.value.value = next
  },
})
</script>

<template>
  <UiCard padding="md" class="mb-5" data-testid="exchange-rate-provider">
    <UiStack gap="sm">
      <UiHeading :level="2" variant="section">{{ t('currencies.rateProvider.title') }}</UiHeading>
      <UiText tone="secondary" size="md">{{ t('currencies.rateProvider.description') }}</UiText>

      <UiAlert v-if="!draft.setting.value" variant="warning" data-testid="exchange-rate-provider-not-seeded">
        {{ t('currencies.rateProvider.notSeeded', { key: BillingSettingKey.RateProvider }) }}
      </UiAlert>
      <UiStack v-else direction="row" align="center" wrap>
        <UiStack gap="xs" class="min-w-56 flex-1">
          <UiText tone="primary" size="md">{{ t('currencies.rateProvider.label') }}</UiText>
          <UiText tone="muted" size="sm">{{ t('currencies.rateProvider.hint') }}</UiText>
        </UiStack>
        <UiSelect
          v-model="provider"
          :options="options"
          :placeholder="t('currencies.rateProvider.placeholder')"
          class="w-72"
          data-testid="exchange-rate-provider-select"
        />
        <SettingSaveButton
          :dirty="draft.dirty.value"
          :saving="draft.saving.value"
          data-testid="exchange-rate-provider-save"
          @click="draft.submit(save)"
        />
        <SettingOutcome :outcome="draft.outcome.value" />
      </UiStack>
    </UiStack>
  </UiCard>
</template>
