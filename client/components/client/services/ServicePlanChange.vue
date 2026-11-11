<template>
  <UiCard data-testid="service-plan-change">
    <UiCardHeader :title="$t('client.services.planChangeTitle')" :icon="ArrowUpDown" />

    <UiStack v-if="result" gap="sm" data-testid="service-plan-change-result">
      <UiAlert variant="success" data-testid="service-plan-change-success">
        {{ $t(RESULT_KEYS[result.status], { amount: money(result.prorationAmount, result.currencyCode) }) }}
      </UiAlert>
      <UiLink v-if="result.invoiceId !== null" :to="clientInvoicePayPath(result.invoiceId)" testid="service-plan-change-pay">
        {{ $t('client.services.planChangePayInvoice') }}
      </UiLink>
    </UiStack>

    <UiSkeleton v-else-if="loading" shape="block" class="h-24" data-testid="service-plan-change-loading" />

    <UiAlert v-else-if="loadError" variant="error" data-testid="service-plan-change-load-error">{{ loadError }}</UiAlert>

    <UiText v-else-if="options.length === 0" size="sm" tone="muted" data-testid="service-plan-change-empty">
      {{ $t('client.services.planChangeNone') }}
    </UiText>

    <template v-else>
      <UiText size="sm" tone="muted" class="mb-5">{{ $t('client.services.planChangeDesc') }}</UiText>

      <UiForm data-testid="service-plan-change-form" :error="submitError" spacing="sm" @submit="submit">
        <UiStack gap="sm">
          <UiRadio
            v-for="option in options"
            :key="option.productId"
            name="plan-change-option"
            :value="option.productId"
            :model-value="selectedProductId ?? undefined"
            :label="option.name"
            :description="optionDescription(option)"
            :testid="`service-plan-change-option-${option.productId}`"
            @update:model-value="selectedProductId = option.productId"
          />
        </UiStack>

        <template #actions>
          <UiButton
            type="submit"
            :loading="submitting"
            :disabled="selectedProductId === null"
            data-testid="service-plan-change-submit"
          >
            {{ submitting ? $t('client.services.planChangeSubmitting') : $t('client.services.planChangeBtn') }}
          </UiButton>
        </template>
      </UiForm>
    </template>
  </UiCard>
</template>

<script setup lang="ts">
/**
 * ServicePlanChange - moves one service to another plan: lists the plans it can move to, each
 * with the prorated amount and whether the move is an upgrade (charged on an invoice) or a
 * downgrade (credited), sends the chosen one and shows what the request came to. The options,
 * the request, its in-flight flag and the API's refusal belong to the plan change store.
 */
import { ref } from 'vue'
import { ArrowUpDown } from 'lucide-vue-next'
import { storeToRefs } from 'pinia'
import { clientInvoicePayPath } from '~/constants/clientRoutes'
import { useServicePlanChangeStore } from '~/stores/serviceplanchange'
import { ServiceChangeStatus } from '~/types/servicechangestatus'
import { ServiceChangeType } from '~/types/servicechangetype'
import type { ServicePlanChangeOption } from '~/types/serviceplanchangeoption'
import { formatCurrency } from '~/utils/formatCurrency'

/** Props of ServicePlanChange. */
interface Props {
  /** The id of the service to move. */
  serviceId: string
}

/** The props of the component. */
const props = defineProps<Props>()

/** Translates a key into the active locale. */
const { t } = useI18n()

/** The plan change store: the options, the request flags and the API's refusal sentence. */
const store = useServicePlanChangeStore()
/** Reactive state of the store this component renders. */
const { options, loading, loadError, submitting, submitError, result } = storeToRefs(store)

/** The sentence that names what each request status means for the client. */
const RESULT_KEYS: Record<ServiceChangeStatus, string> = {
  [ServiceChangeStatus.Completed]: 'client.services.planChangeDone',
  [ServiceChangeStatus.Approved]: 'client.services.planChangeInvoiced',
  [ServiceChangeStatus.Pending]: 'client.services.planChangePending',
  [ServiceChangeStatus.Rejected]: 'client.services.planChangeRejected',
}

/** The sentence that describes each kind of move. */
const OPTION_KEYS: Record<ServiceChangeType, string> = {
  [ServiceChangeType.Upgrade]: 'client.services.planChangeUpgradeDesc',
  [ServiceChangeType.Downgrade]: 'client.services.planChangeDowngradeDesc',
}

/** The product of the plan the client picked; null until one is. */
const selectedProductId = ref<number | null>(null)

/**
 * Formats an amount in a currency.
 *
 * @param amount - The amount.
 * @param code - ISO 4217 code of the currency.
 * @returns The amount as the reader's locale writes it.
 */
const money = (amount: number, code: string): string => formatCurrency(amount, { code })

/**
 * The line under a plan's name: upgrade or downgrade, the prorated amount and the new price.
 *
 * @param option - The plan.
 * @returns The sentence.
 */
const optionDescription = (option: ServicePlanChangeOption): string =>
  t(OPTION_KEYS[option.type], {
    amount: money(option.prorationAmount, option.currencyCode),
    price: money(option.newRecurringAmount, option.currencyCode),
  })

/**
 * Sends the chosen plan.
 *
 * @returns Resolves when the attempt has ended; the outcome is rendered from the store.
 */
const submit = async (): Promise<void> => {
  if (selectedProductId.value === null) {
    return
  }
  await store.submit(props.serviceId, selectedProductId.value)
}

store.reset()
onMounted(() => store.load(props.serviceId))
</script>
