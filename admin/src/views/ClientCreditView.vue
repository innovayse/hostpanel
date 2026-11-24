<script setup lang="ts">
/**
 * Credit tab of a client's profile: the balance, a form that adds credit or takes it off with a reason, and the credit history,
 * newest first.
 *
 * The blocks (the two tiles, the adjustment form, the history card) are the parts of `ClientCreditView.schema`, which
 * `UiSkeletonOf` draws too, so the screen and its skeleton are one shape. Every line is a movement of the balance recorded by
 * the backend with its type, its signed change and the balance it left. Movements older than the history are not itemised; the
 * balance is right regardless. An adjustment reports its outcome (success, or the API's sentence) above the form and reloads
 * the first page of the history.
 */
import { computed, onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import UiButton from '@/components/ui/UiButton.vue'
import UiDataTable from '@/components/ui/UiDataTable.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiFormActions from '@/components/ui/UiFormActions.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiNumberInput from '@/components/ui/UiNumberInput.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiSchemaForm from '@/components/ui/UiSchemaForm.vue'
import UiSchemaTableHead from '@/components/ui/UiSchemaTableHead.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatRow from '@/components/ui/UiStatRow.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import { useOutcomeToast } from '@/composables/useOutcomeToast'
import { formatDate } from '@/lib/format'
import { LEDGER_PAGE_SIZE } from '@/lib/limits'
import { formatPlainAmount } from '@/lib/money'
import { invoicePath } from '@/lib/routes'
import { schemaHeader } from '@/lib/schemaHeader'
import { useClientCreditStore } from '@/stores/clientCredit'
import { CreditAdjustment } from '@/types/creditmovement'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiTableDensity } from '@/utils/ui/injection'
import type { UiStatFigure } from '@/utils/ui/statRow'
import { UiHeadingVariant, UiTextTone } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'
import schema, { CREDIT_FORM_PART, CREDIT_HISTORY_TABLE, CREDIT_STATS_PART } from '@/views/ClientCreditView.schema'
import { UiActionKind } from '@/utils/ui/actions'

/** Translates a key into the active locale. */
const { t } = useI18n()

/** Current route, source of the client id. */
const route = useRoute()

/** Client credit store, the only caller of the credit endpoints. */
const store = useClientCreditStore()

/** Client ID from route params. */
const clientId = computed(() => route.params.id as string)

/** Current page of the history (1-based). */
const page = ref(1)

/** Which way the adjustment goes. */
const direction = ref<CreditAdjustment>(CreditAdjustment.Add)

/** The amount to add or take. */
const amount = ref(0)

/** Why the balance is adjusted. */
const reason = ref('')

/** True while an adjustment is in flight. */
const processing = ref(false)

/** The outcome line of the last adjustment. */
const feedback = useOutcomeToast()

/** Sentence for a page of the history that could not be read while an earlier page is still on screen; empty when none. */
const pageError = ref('')

/** Options of the direction select. */
const directionOptions = computed(() => [
  { value: CreditAdjustment.Add, label: t('clients.clientCredit.add') },
  { value: CreditAdjustment.Deduct, label: t('clients.clientCredit.deduct') },
])

/** True when the form can be sent: an amount above zero and a reason. */
const canAdjust = computed<boolean>(() => !processing.value && amount.value > 0 && reason.value.trim() !== '')

/** True until the first read of the credit has answered: the blocks of the schema draw as placeholders meanwhile. */
const loading = computed<boolean>(() => store.credit === null)

/** The history lines of the shown page; empty while loading. */
const entries = computed(() => store.credit?.entries.items ?? [])

/** Total movements of the history; zero while loading. */
const entriesTotal = computed<number>(() => store.credit?.entries.totalCount ?? 0)

/**
 * Formats an amount in the currency the balance is held in.
 *
 * @param value - The amount.
 * @returns The formatted amount.
 */
const money = (value: number): string => formatPlainAmount(value, store.credit?.currency ?? '')

/** The figures of the tiles by key: the balance and the currency it is held in; empty while loading. */
const figures = computed((): Record<string, UiStatFigure> =>
  store.credit === null
    ? {}
    : {
        balance: { value: money(store.credit.balance), tone: UiTone.Primary },
        currency: { value: store.credit.currency, tone: UiTone.Neutral },
      },
)

/**
 * Loads one page of the credit.
 *
 * @param target - Page to load (1-based).
 * @returns Resolves when the page is loaded or refused.
 */
const load = async (target = page.value): Promise<void> => {
  const loaded = await store.fetchCredit(clientId.value, target)
  if (loaded) {
    page.value = target
    pageError.value = ''
    return
  }
  // A refused first read is the error state below; a refused later page leaves the loaded one on screen and says why.
  pageError.value = store.credit === null ? '' : store.error ?? t('errors.noAnswer')
}

/**
 * Sends the adjustment, shows the outcome and clears the form on success.
 *
 * @returns Resolves when the request has settled.
 */
const adjust = async (): Promise<void> => {
  if (!canAdjust.value) {
    return
  }
  processing.value = true
  const ok = await store.adjust(clientId.value, direction.value, amount.value, reason.value.trim())
  processing.value = false
  if (ok) {
    page.value = 1
    amount.value = 0
    reason.value = ''
    feedback.show(t('clients.clientCredit.adjusted'), false)
  } else {
    feedback.show(store.error ?? t('errors.noAnswer'), true)
  }
}

onMounted(() => load(1))
</script>

<template>
  <UiStack data-testid="client-credit">
    <UiPageHeader v-bind="schemaHeader(schema, t)" />

    <UiErrorState
      v-if="store.credit === null && store.error !== null"
      :message="store.error"
      :loading="store.loading"
      testid="client-credit-error"
      @retry="load(1)"
    />

    <template v-else>
      <UiStatRow
        :stats="CREDIT_STATS_PART.stats"
        :columns="CREDIT_STATS_PART.columns"
        :figures="figures"
        :loading="loading"
        testid-prefix="client-credit"
      />

      <UiSchemaForm
        :columns="CREDIT_FORM_PART.columns"
        :loading="loading"
        :errors="store.fieldErrors"
        data-testid="client-credit-form"
        @submit="adjust"
      >
        <template #field-direction>
          <UiSelect
            v-model="direction"
            :options="directionOptions"
            :placeholder="t('clients.clientCredit.direction')"
            data-testid="client-credit-direction"
          />
        </template>
        <template #field-amount>
          <UiNumberInput v-model="amount" :step="0.01" :min="0" placeholder="0.00" data-testid="client-credit-amount" />
        </template>
        <template #field-reason="{ id, invalid }">
          <UiInput
            :id="id"
            v-model="reason"
            type="text"
            :invalid="invalid"
            :placeholder="t('clients.clientCredit.reasonPlaceholder')"
            data-testid="client-credit-reason"
          />
        </template>
        <UiFormActions>
          <UiButton :action="UiActionKind.Apply" type="submit" :variant="UiButtonVariant.Primary" :loading="processing" :disabled="loading || !canAdjust" data-testid="client-credit-submit">
            {{ t('shared.apply') }}
          </UiButton>
        </UiFormActions>
      </UiSchemaForm>

      <UiDataTable
        :density="UiTableDensity.Comfortable"
        :loading="loading"
        :empty="!loading && entries.length === 0"
        :empty-text="t('clients.clientCredit.historyEmpty')"
        :page="page"
        :total="entriesTotal"
        :page-size="LEDGER_PAGE_SIZE"
        pagination-testid="client-credit-pagination"
        data-testid="client-credit-table"
        @update:page="load"
      >
        <template #header>
          <UiStack>
            <UiHeading :level="2" :variant="UiHeadingVariant.Card">{{ t('clients.clientCredit.history') }}</UiHeading>
            <UiStatusMessage :message="pageError" :tone="UiTone.Danger" data-testid="client-credit-page-error" />
          </UiStack>
        </template>
        <UiSchemaTableHead :columns="CREDIT_HISTORY_TABLE.columns" />
        <UiTableBody>
          <UiTableRow v-for="entry in entries" :key="entry.id" hoverable :data-testid="`client-credit-entry-${entry.id}`">
            <UiTableCell muted>{{ formatDate(entry.date) }}</UiTableCell>
            <UiTableCell>{{ t(`clients.clientCredit.movement.${entry.movement}`) }}</UiTableCell>
            <UiTableCell muted>{{ entry.description }}</UiTableCell>
            <UiTableCell>
              <UiLink v-if="entry.invoiceId !== null" :to="invoicePath(entry.invoiceId)">#{{ entry.invoiceId }}</UiLink>
              <UiText v-else :tone="UiTextTone.Muted">-</UiText>
            </UiTableCell>
            <UiTableCell numeric>
              <UiText :tone="entry.change >= 0 ? UiTextTone.Success : UiTextTone.Danger" :data-testid="`client-credit-change-${entry.id}`">
                {{ (entry.change > 0 ? '+' : '') + money(entry.change) }}
              </UiText>
            </UiTableCell>
            <UiTableCell muted numeric>{{ money(entry.balanceAfter) }}</UiTableCell>
          </UiTableRow>
        </UiTableBody>
      </UiDataTable>
    </template>
  </UiStack>
</template>
