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
import UiCard from '@/components/ui/UiCard.vue'
import UiClientSelect from '@/components/ui/UiClientSelect.vue'
import UiDatePicker from '@/components/ui/UiDatePicker.vue'
import { UiDatePickerDirection } from '@/utils/ui/datePicker'
import UiField from '@/components/ui/UiField.vue'
import UiForm from '@/components/ui/UiForm.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTextarea from '@/components/ui/UiTextarea.vue'
import { useClientCurrency } from '@/composables/useClientCurrency'
import { useClientOptions } from '@/composables/useClientOptions'
import { QUOTE_STAGE_OPTIONS } from '@/lib/constants'
import { toIsoDay } from '@/lib/format'
import { useQuotesStore } from '@/stores/quotes'
import { QuoteStage } from '@/types/quote'
import type { QuoteItemUpdate } from '@/types/quote'
import { MS_PER_DAY } from '@/lib/limits'
import { AppRoute, quotePath } from '@/lib/routes'

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
const items = ref<QuoteItemUpdate[]>([{ quantity: 1, description: '', unitPrice: 0, discountPercent: 0, taxed: false }])

/** Proposal text shown at the top of the quote. */
const proposalText = ref('')

/** Notes shown to the customer. */
const customerNotes = ref('')

/** Notes only admins see. */
const adminNotes = ref('')

/** Stages the select offers (the "all stages" entry is for filters). */
const stageOptions = computed(() => QUOTE_STAGE_OPTIONS.filter((option) => option.value !== ''))

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
    items: items.value,
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
    <UiPageHeader :title="t('billing.addQuote.createQuote')" :subtitle="t('billing.addQuote.createANewQuoteFor')" />

    <UiForm stacked @submit="submit">
      <UiAlert v-if="store.error" variant="error" data-testid="billing-add-quote-error">
        {{ store.error }}
      </UiAlert>

      <UiCard padding="lg">
        <UiStack>
          <UiHeading :level="3" variant="card">
            {{ t('billing.addQuote.generalInformation') }}
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
                  data-testid="billing-add-quote-subject"
                />
              </template>
            </UiField>
            <UiField :label="t('shared.stage')" :error="store.fieldErrors.stage">
              <UiSelect v-model="stage" :options="stageOptions" :placeholder="t('billing.quoteDetail.selectStage')" />
            </UiField>
            <UiField :label="t('shared.client')" :error="store.fieldErrors.clientId">
              <UiClientSelect
                v-model="clientId"
                :clients="clients"
                :placeholder="t('shared.selectAClient')"
              />
            </UiField>
            <UiField :label="t('shared.validUntil')" :error="store.fieldErrors.validUntil">
              <UiDatePicker v-model="validUntil" :direction="UiDatePickerDirection.Future" :placeholder="t('shared.selectDate')" />
            </UiField>
          </UiGrid>
        </UiStack>
      </UiCard>

      <QuoteItemsEditor v-model="items" :currency="currency" />

      <UiCard padding="lg">
        <UiStack>
          <UiField :label="t('billing.addQuote.proposalText')" :hint="t('billing.addQuote.displayedAtTheTopOf')">
            <UiTextarea
              v-model="proposalText"
              rows="4"
              :placeholder="t('billing.addQuote.enterProposalText')"
              data-testid="billing-add-quote-proposal"
            />
          </UiField>
          <UiField :label="t('billing.addQuote.customerNotes')" :hint="t('billing.addQuote.displayedAsAFooterTo')">
            <UiTextarea
              v-model="customerNotes"
              rows="3"
              :placeholder="t('billing.addQuote.enterCustomerNotes')"
            />
          </UiField>
          <UiField :label="t('billing.addQuote.adminOnlyNotes')" :hint="t('billing.addQuote.privateNotes')">
            <UiTextarea
              v-model="adminNotes"
              rows="3"
              :placeholder="t('billing.addQuote.enterAdminNotes')"
            />
          </UiField>
        </UiStack>
      </UiCard>

      <UiStack direction="row" gap="md" align="center">
        <UiButton type="submit" variant="primary" :loading="store.loading" data-testid="billing-add-quote-submit">
          {{ t('billing.addQuote.createQuote') }}
        </UiButton>
        <UiLink :to="AppRoute.BillingQuotes" variant="ghost" data-testid="billing-add-quote-cancel">
          {{ t('shared.cancel') }}
        </UiLink>
      </UiStack>
    </UiForm>
  </UiStack>
</template>
