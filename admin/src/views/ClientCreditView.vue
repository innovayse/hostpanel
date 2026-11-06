<script setup lang="ts">
/**
 * Credit tab of a client's profile: the balance, a form that adds credit or takes it off with a reason, and the credit history,
 * newest first.
 *
 * Every line is a movement of the balance recorded by the backend with its type, its signed change and the balance it left.
 * Movements older than the history are not itemised; the balance is right regardless. An adjustment reports its outcome
 * (success, or the API's sentence) above the form and reloads the first page of the history.
 */
import { computed, onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiForm from '@/components/ui/UiForm.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiNumberInput from '@/components/ui/UiNumberInput.vue'
import UiPagination from '@/components/ui/UiPagination.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiSkeleton from '@/components/ui/UiSkeleton.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatTile from '@/components/ui/UiStatTile.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import { useResultBanner } from '@/composables/useResultBanner'
import { formatDate } from '@/lib/format'
import { LEDGER_PAGE_SIZE } from '@/lib/limits'
import { formatPlainAmount } from '@/lib/money'
import { invoicePath } from '@/lib/routes'
import { useClientCreditStore } from '@/stores/clientCredit'
import { CreditAdjustment } from '@/types/creditmovement'
import { UiAlignItems, UiJustify } from '@/utils/ui/layout'
import { UiTextTone } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'

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
const feedback = useResultBanner()

/** Sentence for a page of the history that could not be read while an earlier page is still on screen; empty when none. */
const pageError = ref('')

/** Tone of the outcome line: danger for a refusal, success otherwise. */
const feedbackTone = computed<UiTone>(() => (feedback.isError.value ? UiTone.Danger : UiTone.Success))

/** Options of the direction select. */
const directionOptions = computed(() => [
  { value: CreditAdjustment.Add, label: t('clients.clientCredit.add') },
  { value: CreditAdjustment.Deduct, label: t('clients.clientCredit.deduct') },
])

/** True when the form can be sent: an amount above zero and a reason. */
const canAdjust = computed<boolean>(() => !processing.value && amount.value > 0 && reason.value.trim() !== '')

/**
 * Formats an amount in the currency the balance is held in.
 *
 * @param value - The amount.
 * @returns The formatted amount.
 */
const money = (value: number): string => formatPlainAmount(value, store.credit?.currency ?? '')

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
  feedback.clear()
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
    <UiHeading :level="1" variant="card">{{ t('clients.clientCredit.credit') }}</UiHeading>

    <UiErrorState
      v-if="store.credit === null && store.error !== null"
      :message="store.error"
      :loading="store.loading"
      testid="client-credit-error"
      @retry="load(1)"
    />

    <UiStack v-else-if="store.credit === null" data-testid="client-credit-loading">
      <UiSkeleton :lines="3" />
    </UiStack>

    <template v-else>
      <UiGrid :columns="2">
        <UiStatTile
          :label="t('clients.clientCredit.balance')"
          :value="money(store.credit.balance)"
          :tone="UiTone.Primary"
          data-testid="client-credit-balance"
        />
        <UiStatTile :label="t('clients.clientCredit.currency')" :value="store.credit.currency" :tone="UiTone.Neutral" />
      </UiGrid>

      <UiCard padding="md">
        <UiForm stacked data-testid="client-credit-form" @submit="adjust">
          <UiHeading :level="3" variant="card">{{ t('clients.clientCredit.adjustTitle') }}</UiHeading>
          <UiField :label="t('clients.clientCredit.direction')" :error="store.fieldErrors.direction">
            <UiSelect
              v-model="direction"
              :options="directionOptions"
              :placeholder="t('clients.clientCredit.direction')"
              data-testid="client-credit-direction"
            />
          </UiField>
          <UiField :label="t('shared.amount')" :error="store.fieldErrors.amount">
            <UiNumberInput v-model="amount" :step="0.01" :min="0" placeholder="0.00" data-testid="client-credit-amount" />
          </UiField>
          <UiField :label="t('clients.clientCredit.reason')" :error="store.fieldErrors.reason">
            <template #default="{ id, invalid }">
              <UiInput
                :id="id"
                v-model="reason"
                type="text"
                :invalid="invalid"
                :placeholder="t('clients.clientCredit.reasonPlaceholder')"
                data-testid="client-credit-reason"
              />
            </template>
          </UiField>
          <UiStatusMessage :message="feedback.message.value" :tone="feedbackTone" data-testid="client-credit-feedback" />
          <UiStack direction="row" :justify="UiJustify.Center">
            <UiButton type="submit" variant="primary" :loading="processing" :disabled="!canAdjust" data-testid="client-credit-submit">
              {{ t('clients.clientCredit.apply') }}
            </UiButton>
          </UiStack>
        </UiForm>
      </UiCard>

      <UiCard class="overflow-x-auto">
        <UiHeading :level="3" variant="card">{{ t('clients.clientCredit.history') }}</UiHeading>
        <UiStatusMessage :message="pageError" :tone="UiTone.Danger" data-testid="client-credit-page-error" />
        <UiText v-if="store.credit.entries.items.length === 0" :tone="UiTextTone.Muted" data-testid="client-credit-empty">
          {{ t('clients.clientCredit.historyEmpty') }}
        </UiText>
        <UiTable v-else density="comfortable" data-testid="client-credit-table">
          <UiTableHead>
            <UiTableRow>
              <UiTableHeader>{{ t('shared.date') }}</UiTableHeader>
              <UiTableHeader>{{ t('shared.type') }}</UiTableHeader>
              <UiTableHeader>{{ t('shared.description') }}</UiTableHeader>
              <UiTableHeader>{{ t('shared.invoice') }}</UiTableHeader>
              <UiTableHeader>{{ t('clients.clientCredit.change') }}</UiTableHeader>
              <UiTableHeader>{{ t('shared.balance') }}</UiTableHeader>
            </UiTableRow>
          </UiTableHead>
          <UiTableBody divided>
            <UiTableRow v-for="entry in store.credit.entries.items" :key="entry.id" hoverable :data-testid="`client-credit-entry-${entry.id}`">
              <UiTableCell muted>{{ formatDate(entry.date) }}</UiTableCell>
              <UiTableCell>{{ t(`clients.clientCredit.movement.${entry.movement}`) }}</UiTableCell>
              <UiTableCell muted>{{ entry.description }}</UiTableCell>
              <UiTableCell>
                <UiLink v-if="entry.invoiceId !== null" :to="invoicePath(entry.invoiceId)">#{{ entry.invoiceId }}</UiLink>
                <UiText v-else :tone="UiTextTone.Muted">-</UiText>
              </UiTableCell>
              <UiTableCell>
                <UiText :tone="entry.change >= 0 ? UiTextTone.Success : UiTextTone.Danger" :data-testid="`client-credit-change-${entry.id}`">
                  {{ (entry.change > 0 ? '+' : '') + money(entry.change) }}
                </UiText>
              </UiTableCell>
              <UiTableCell muted>{{ money(entry.balanceAfter) }}</UiTableCell>
            </UiTableRow>
          </UiTableBody>
        </UiTable>
        <UiStack
          v-if="store.credit.entries.totalCount > LEDGER_PAGE_SIZE"
          direction="row"
          :justify="UiJustify.Between"
          :align="UiAlignItems.Center"
        >
          <UiText :tone="UiTextTone.Muted">{{ t('clients.clientCredit.entriesCount', { count: store.credit.entries.totalCount }) }}</UiText>
          <UiPagination
            :page="page"
            :total="store.credit.entries.totalCount"
            :page-size="LEDGER_PAGE_SIZE"
            testid="client-credit-pagination"
            @update:page="load"
          />
        </UiStack>
      </UiCard>
    </template>
  </UiStack>
</template>
