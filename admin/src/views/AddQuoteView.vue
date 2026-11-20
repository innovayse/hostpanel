<script setup lang="ts">
/**
 * Create quote: pick the client, set the subject, stage and validity, add the lines and the
 * notes, and save. A client named in the query (`?clientId=`) is preselected.
 */
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import QuoteItemsEditor from '@/components/billing/QuoteItemsEditor.vue'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiClientSelect from '@/components/ui/UiClientSelect.vue'
import UiDatePicker from '@/components/ui/UiDatePicker.vue'
import UiDetailCards from '@/components/ui/UiDetailCards.vue'
import { UiDatePickerDirection } from '@/utils/ui/datePicker'
import UiFormActions from '@/components/ui/UiFormActions.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiSchemaForm from '@/components/ui/UiSchemaForm.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTextarea from '@/components/ui/UiTextarea.vue'
import { useClientCurrency } from '@/composables/useClientCurrency'
import { useClientOptions } from '@/composables/useClientOptions'
import { QUOTE_STAGE_OPTIONS } from '@/lib/constants'
import { QUOTE_ITEMS_PART } from '@/lib/quoteSchemaParts'
import { toIsoDay } from '@/lib/format'
import { useQuotesStore } from '@/stores/quotes'
import { QuoteStage } from '@/types/quote'
import { blankQuoteLine, quoteLinesPayload } from '@/lib/quoteLines'
import type { QuoteLineDraft } from '@/types/quote'
import { MS_PER_DAY } from '@/lib/limits'
import { AppRoute, quotePath } from '@/lib/routes'
import { schemaHeader } from '@/lib/schemaHeader'
import schema, { QUOTE_GENERAL_FORM_PART, QUOTE_NOTES_FORM_PART } from '@/views/AddQuoteView.schema'
import { UiActionKind } from '@/utils/ui/actions'

const { t } = useI18n()

/** Days between today and the default validity date of a new quote. */
const DEFAULT_VALIDITY_DAYS = 30

const route = useRoute()
const router = useRouter()
const store = useQuotesStore()
const { clients, load: loadClients } = useClientOptions()

/** Id of the client the quote is for, as the picker holds it. */
const clientId = ref('')

/** Billing currency of the chosen client, the one the quote will be priced in. */
const currency = useClientCurrency(computed(() => Number(clientId.value)))

/** Quote subject. */
const subject = ref('')

/** Quote stage. */
const stage = ref<string>(QuoteStage.Draft)

/** Valid-until date (ISO day). */
const validUntil = ref<string | null>(toIsoDay(new Date(Date.now() + DEFAULT_VALIDITY_DAYS * MS_PER_DAY)))

/** The quote lines, starting with one blank line. */
const items = ref<QuoteLineDraft[]>([blankQuoteLine()])

/** Proposal text shown at the top of the quote. */
const proposalText = ref('')

/** Notes shown to the customer. */
const customerNotes = ref('')

/** Notes only admins see. */
const adminNotes = ref('')

/** Help lines under the notes fields, by field key. */
const hints = computed<Record<string, string>>(() => ({
  proposalText: t('billing.addQuote.displayedAtTheTopOf'),
  customerNotes: t('billing.addQuote.displayedAsAFooterTo'),
  adminNotes: t('billing.addQuote.privateNotes'),
}))

/** Creates the quote and opens it. */
const submit = async (): Promise<void> => {
  const id = await store.createQuote({
    clientId: Number(clientId.value),
    subject: subject.value,
    stage: stage.value as QuoteStage,
    validUntil: validUntil.value ?? '',
    proposalText: proposalText.value || undefined,
    customerNotes: customerNotes.value || undefined,
    adminNotes: adminNotes.value || undefined,
    items: quoteLinesPayload(items.value),
  })
  if (id !== null) {
    await router.push(quotePath(id))
  }
}

onMounted(() => {
  void loadClients()
  const fromQuery = route.query.clientId
  if (typeof fromQuery === 'string') {
    clientId.value = fromQuery
  }
})
</script>

<template>
  <UiStack data-testid="billing-add-quote-view">
    <UiPageHeader v-bind="schemaHeader(schema, t)" />

    <UiAlert v-if="store.error" variant="error" data-testid="billing-add-quote-error">
      {{ store.error }}
    </UiAlert>

    <UiSchemaForm :columns="QUOTE_GENERAL_FORM_PART.columns" :errors="store.fieldErrors" @submit="submit">
      <template #field-subject="{ id, invalid }">
        <UiInput
          :id="id"
          v-model="subject"
          type="text"
          :invalid="invalid"
          :placeholder="t('billing.quoteDetail.quoteSubject')"
          data-testid="billing-add-quote-subject"
        />
      </template>
      <template #field-stage>
        <UiSelect v-model="stage" :options="QUOTE_STAGE_OPTIONS" :placeholder="t('billing.quoteDetail.selectStage')" />
      </template>
      <template #field-clientId>
        <UiClientSelect
          v-model="clientId"
          :clients="clients"
          :placeholder="t('shared.selectAClient')"
        />
      </template>
      <template #field-validUntil>
        <UiDatePicker v-model="validUntil" :direction="UiDatePickerDirection.Future" :placeholder="t('shared.selectDate')" />
      </template>
    </UiSchemaForm>

    <UiDetailCards :columns="QUOTE_ITEMS_PART.columns">
      <template #card-items>
        <QuoteItemsEditor v-model="items" :currency="currency" />
      </template>
    </UiDetailCards>

    <UiSchemaForm :columns="QUOTE_NOTES_FORM_PART.columns" :errors="store.fieldErrors" :hints="hints" @submit="submit">
      <template #field-proposalText>
        <UiTextarea
          v-model="proposalText"
          rows="4"
          :placeholder="t('billing.addQuote.enterProposalText')"
          data-testid="billing-add-quote-proposal"
        />
      </template>
      <template #field-customerNotes>
        <UiTextarea
          v-model="customerNotes"
          rows="3"
          :placeholder="t('billing.addQuote.enterCustomerNotes')"
        />
      </template>
      <template #field-adminNotes>
        <UiTextarea
          v-model="adminNotes"
          rows="3"
          :placeholder="t('billing.addQuote.enterAdminNotes')"
        />
      </template>

      <UiFormActions>
        <UiLink :action="UiActionKind.Cancel" :to="AppRoute.BillingQuotes" variant="ghost" data-testid="billing-add-quote-cancel">
          {{ t('shared.cancel') }}
        </UiLink>
        <UiButton :action="UiActionKind.Add" type="submit" variant="primary" :loading="store.loading" data-testid="billing-add-quote-submit">
          {{ t('billing.addQuote.createQuote') }}
        </UiButton>
      </UiFormActions>
    </UiSchemaForm>
  </UiStack>
</template>
