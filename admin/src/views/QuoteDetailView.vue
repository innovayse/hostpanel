<script setup lang="ts">
/**
 * Quote detail: opens an existing quote for editing, or, without an id in the route, starts a
 * new one for the client named in the query. The operator edits the general data, the lines and
 * the three notes, then saves, duplicates, converts the quote to an invoice or deletes it. A refused
 * load offers a retry; each action's outcome shows under the header; deleting is confirmed in a
 * danger prompt.
 */
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import QuoteItemsEditor from '@/components/billing/QuoteItemsEditor.vue'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiDatePicker from '@/components/ui/UiDatePicker.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiField from '@/components/ui/UiField.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiSkeleton from '@/components/ui/UiSkeleton.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import UiTextarea from '@/components/ui/UiTextarea.vue'
import { Icon } from '@/lib/icons'
import { quoteStageTone, statusLabel } from '@/lib/tones'
import { QUOTE_STAGE_OPTIONS } from '@/lib/constants'
import { formatDate } from '@/lib/format'
import { useClientCurrency } from '@/composables/useClientCurrency'
import { useQuotesStore } from '@/stores/quotes'
import { QuoteStage } from '@/types/quote'
import type { QuoteItemUpdate } from '@/types/quote'
import { invoicePath, quotePath } from '@/lib/routes'
import { UiTone } from '@/utils/ui/tone'

const { t } = useI18n()

const route = useRoute()
const router = useRouter()
const store = useQuotesStore()

/**
 * A blank line for a quote that has none yet.
 *
 * @returns The blank line.
 */
const blankLine = (): QuoteItemUpdate => ({ quantity: 1, description: '', unitPrice: 0, discountPercent: 0, taxed: false })

/** True when the route names no quote, so the screen creates one. */
const isNew = computed<boolean>(() => route.params.id === undefined)

/** Id of the quote from the route; 0 when creating. */
const quoteId = computed<number>(() => (isNew.value ? 0 : Number(route.params.id)))

/** Client the new quote is for, from the query; 0 when absent. */
const queryClientId = computed<number>(() => Number(route.query.clientId ?? 0))

/** Billing currency of the client a new quote is for; null for a stored quote, which carries its own. */
const clientCurrency = useClientCurrency(computed(() => (isNew.value ? queryClientId.value : 0)))

/** Currency the lines are priced in: the stored quote's, else the new quote's client's. */
const quoteCurrency = computed<string | null>(() => store.currentQuote?.currency ?? clientCurrency.value)

/** Quote subject. */
const subject = ref('')

/** Quote stage. */
const stage = ref<string>(QuoteStage.Draft)

/** Valid-until date (ISO day). */
const validUntil = ref<string | null>(null)

/** Proposal text shown at the top of the quote. */
const proposalText = ref('')

/** Notes shown to the customer. */
const customerNotes = ref('')

/** Notes only admins see. */
const adminNotes = ref('')

/** The quote lines. */
const items = ref<QuoteItemUpdate[]>([blankLine()])

/** Sentence for a problem found before any request went out, null when none. */
const localError = ref<string | null>(null)

/** True while the delete confirmation is open. */
const deleteOpen = ref(false)

/** True while the convert confirmation is open. */
const convertOpen = ref(false)

/** Stages the select offers (the "all stages" entry is for filters). */
const stageOptions = computed(() => QUOTE_STAGE_OPTIONS.filter((option) => option.value !== ''))

/** The sentence to show: a local problem first, otherwise the store's. */
const errorText = computed<string | null>(() => localError.value ?? store.error)

/** Fills the form from the loaded quote. */
const populate = (): void => {
  const quote = store.currentQuote
  if (quote === null) {
    return
  }
  subject.value = quote.subject
  stage.value = quote.stage
  validUntil.value = quote.validUntil ? (quote.validUntil.split('T')[0] ?? null) : null
  proposalText.value = quote.proposalText ?? ''
  customerNotes.value = quote.customerNotes ?? ''
  adminNotes.value = quote.adminNotes ?? ''
  items.value = quote.items.map((item) => ({
    id: item.id,
    quantity: item.quantity,
    description: item.description,
    unitPrice: item.unitPrice,
    discountPercent: item.discountPercent,
    taxed: item.taxed,
  }))
}

/**
 * Builds the fields the create and update bodies share.
 *
 * @returns Subject, stage, validity and the three notes, with empty notes left out.
 */
const fields = () => ({
  subject: subject.value,
  stage: stage.value as QuoteStage,
  validUntil: validUntil.value ?? '',
  proposalText: proposalText.value || undefined,
  customerNotes: customerNotes.value || undefined,
  adminNotes: adminNotes.value || undefined,
})

/** Loads the quote of the route again, after a refused load. */
const reload = (): void => {
  void store.fetchById(quoteId.value)
}

/** Saves the quote: creates it (and opens it) or updates it. */
const save = async (): Promise<void> => {
  store.successMessage = null
  localError.value = null
  if (isNew.value) {
    if (queryClientId.value === 0) {
      localError.value = t('billing.quoteDetail.clientIdIsRequired')
      return
    }
    const id = await store.createQuote({
      clientId: queryClientId.value,
      ...fields(),
      items: items.value,
    })
    if (id !== null) {
      await router.push(quotePath(id))
    }
    return
  }
  await store.updateQuote(quoteId.value, { ...fields(), items: items.value })
}

/** Duplicates the quote and opens the copy. */
const duplicate = async (): Promise<void> => {
  store.successMessage = null
  const id = await store.duplicateQuote(quoteId.value)
  if (id !== null) {
    await router.push(quotePath(id))
  }
}

/** Converts the quote to an invoice and opens the invoice. */
const convert = async (): Promise<void> => {
  convertOpen.value = false
  const id = await store.convertToInvoice(quoteId.value)
  if (id !== null) {
    await router.push(invoicePath(id))
  }
}

/** Deletes the quote and goes back; a refusal stays on the quote with the API's sentence. */
const remove = async (): Promise<void> => {
  store.successMessage = null
  const deleted = await store.deleteQuote(quoteId.value)
  deleteOpen.value = false
  if (deleted) {
    router.back()
  }
}

/** Closes the delete confirmation; ignored while the delete runs. */
const dismissDelete = (): void => {
  if (!store.loading) {
    deleteOpen.value = false
  }
}

/** Goes back to the previous screen. */
const goBack = (): void => {
  router.back()
}

watch(() => store.currentQuote, populate)
watch(quoteId, (id) => {
  if (id > 0) {
    void store.fetchById(id)
  }
})

onMounted(() => {
  if (!isNew.value && quoteId.value > 0) {
    void store.fetchById(quoteId.value)
  }
})
</script>

<template>
  <UiStack data-testid="billing-quote-detail-view">
    <UiSkeleton v-if="store.loading && store.currentQuote === null && !isNew" shape="block" data-testid="billing-quote-loading" />

    <UiErrorState
      v-else-if="store.error && store.currentQuote === null && !isNew"
      :message="store.error"
      :loading="store.loading"
      testid="billing-quote-load-error"
      @retry="reload"
    />

    <template v-else>
      <UiPageHeader :title="isNew ? t('billing.quoteDetail.newQuote') : t('billing.quoteDetail.quoteNumber', { id: quoteId })">
        <UiBadge v-if="!isNew" :tone="quoteStageTone(stage)" data-testid="billing-quote-stage">{{ statusLabel(QUOTE_STAGE_OPTIONS, stage) }}</UiBadge>
        <template #actions>
          <UiButton variant="ghost" data-testid="billing-quote-back" @click="goBack">
            <UiIcon :markup="Icon.Back" size="sm" />
            {{ t('shared.back') }}
          </UiButton>
          <UiButton
            variant="primary"
            :loading="store.loading"
            data-testid="billing-quote-save"
            @click="save"
          >
            {{ t('shared.saveChanges') }}
          </UiButton>
          <template v-if="!isNew">
            <UiButton variant="secondary" :disabled="store.loading" data-testid="billing-quote-duplicate" @click="duplicate">
              {{ t('billing.quoteDetail.duplicate') }}
            </UiButton>
            <UiButton variant="secondary" :disabled="store.loading" data-testid="billing-quote-convert" @click="convertOpen = true">
              {{ t('billing.quoteDetail.convertToInvoice') }}
            </UiButton>
            <UiButton variant="danger" :disabled="store.loading" data-testid="billing-quote-delete" @click="deleteOpen = true">
              {{ t('shared.delete') }}
            </UiButton>
          </template>
        </template>
      </UiPageHeader>

      <UiAlert v-if="store.successMessage" variant="success" data-testid="billing-quote-success">
        {{ store.successMessage }}
      </UiAlert>
      <UiAlert v-if="errorText" variant="error" data-testid="billing-quote-error">
        {{ errorText }}
      </UiAlert>

      <UiCard padding="md">
        <UiStack>
          <UiHeading :level="3" variant="card">
            {{ t('billing.quoteDetail.generalInformation') }}
          </UiHeading>
          <UiGrid :columns="2">
            <UiField :label="t('shared.subject')" :error="store.fieldErrors.subject">
              <template #default="{ id, invalid }">
                <UiInput
                  :id="id"
                  v-model="subject"
                  type="text"
                  :invalid="invalid"
                  :placeholder="t('billing.quoteDetail.quoteSubject')"
                  data-testid="billing-quote-subject"
                />
              </template>
            </UiField>
            <UiField :label="t('shared.stage')" :error="store.fieldErrors.stage">
              <UiSelect v-model="stage" :options="stageOptions" :placeholder="t('billing.quoteDetail.selectStage')" />
            </UiField>
            <UiField v-if="!isNew && store.currentQuote" :label="t('billing.quoteDetail.dateCreated')">
              <UiText tone="secondary" size="md" data-testid="billing-quote-created">
                {{ formatDate(store.currentQuote.dateCreated) }}
              </UiText>
            </UiField>
            <UiField :label="t('shared.validUntil')" :error="store.fieldErrors.validUntil">
              <UiDatePicker v-model="validUntil" :placeholder="t('shared.selectDate')" />
            </UiField>
          </UiGrid>
        </UiStack>
      </UiCard>

      <QuoteItemsEditor v-model="items" :currency="quoteCurrency" />

      <UiCard padding="md">
        <UiStack>
          <UiHeading :level="3" variant="card">
            {{ t('shared.notes') }}
          </UiHeading>
          <UiField :label="t('billing.quoteDetail.proposalText')">
            <UiTextarea
              v-model="proposalText"
              rows="4"
              :placeholder="t('billing.quoteDetail.proposalTextVisibleToThe')"
              data-testid="billing-quote-proposal"
            />
          </UiField>
          <UiField :label="t('billing.quoteDetail.customerNotes')">
            <UiTextarea
              v-model="customerNotes"
              rows="3"
              :placeholder="t('billing.quoteDetail.notesVisibleToTheCustomer')"
              data-testid="billing-quote-customer-notes"
            />
          </UiField>
          <UiField :label="t('billing.quoteDetail.adminOnlyNotes')">
            <UiTextarea
              v-model="adminNotes"
              rows="3"
              :placeholder="t('billing.quoteDetail.internalNotesNotVisibleTo')"
              data-testid="billing-quote-admin-notes"
            />
          </UiField>
        </UiStack>
      </UiCard>
    </template>

    <UiDialog
      variant="confirm"
      :open="deleteOpen"
      :title="t('billing.quoteDetail.deleteQuote')"
      :message="t('billing.quoteDetail.deleteMessage', { id: quoteId })"
      :confirm-label="t('shared.delete')"
      :loading-label="t('shared.deleting')"
      :loading="store.loading"
      :tone="UiTone.Danger"
      @confirm="remove"
      @close="dismissDelete"
    />

    <UiDialog
      variant="confirm"
      :open="convertOpen"
      :title="t('billing.quoteDetail.convertToInvoice')"
      :message="t('billing.quoteDetail.convertMessage', { id: quoteId })"
      :confirm-label="t('billing.quoteDetail.convert')"
      :loading-label="t('shared.converting')"
      :loading="store.loading"
      tone="primary"
      @confirm="convert"
      @close="convertOpen = false"
    />
  </UiStack>
</template>
