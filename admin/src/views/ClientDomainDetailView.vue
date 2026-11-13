<script setup lang="ts">
/**
 * Editable domain detail page: displays and allows editing all domain fields.
 * Supports registrar commands, management tool toggles, DNS records, email forwarding, and reminders.
 *
 * A glass header card names the domain with its status badge and holds Save and Cancel; three tabs split the page:
 * details (domain info, dates and billing), registrar (nameservers and registrar commands) and management (tools,
 * notes, email forwarding, DNS records and reminders). The form is one: a field on a tab that is not on show keeps
 * its value and is saved with the rest.
 */
import { ref, computed, onMounted, onBeforeUnmount, provide, watch } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useDomainsStore } from '@/stores/domains'
import { DOMAIN_STATUS_OPTIONS, PAYMENT_METHOD_OPTIONS } from '@/lib/constants'
import { domainStatusTone, statusLabel } from '@/lib/tones'
import { Icon } from '@/lib/icons'
import { DomainStatus } from '@/types/domain'
import { formatDate, toDateInputValue, EMPTY_VALUE } from '@/lib/format'
import DnsRecordsTable from '@/components/clients/DnsRecordsTable.vue'
import DomainContactModal from '@/components/clients/DomainContactModal.vue'
import DomainRemindersTable from '@/components/clients/DomainRemindersTable.vue'
import EmailForwardingTable from '@/components/clients/EmailForwardingTable.vue'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiDatePicker from '@/components/ui/UiDatePicker.vue'
import { UiDatePickerDirection } from '@/utils/ui/datePicker'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiForm from '@/components/ui/UiForm.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiLabel from '@/components/ui/UiLabel.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiNumberInput from '@/components/ui/UiNumberInput.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTabs from '@/components/ui/UiTabs.vue'
import UiText from '@/components/ui/UiText.vue'
import UiTextarea from '@/components/ui/UiTextarea.vue'
import UiToggle from '@/components/ui/UiToggle.vue'
import { UiButtonVariant } from '@/utils/ui/button'
import { UI_FIELD_LOADING } from '@/utils/ui/injection'
import { UiAlignItems, UiJustify } from '@/utils/ui/layout'
import { UiSize } from '@/utils/ui/size'
import type { UiTabItem } from '@/utils/ui/tabs'
import { UiLabelVariant, UiTextSize, UiTextTone } from '@/utils/ui/text'
import { NOTICE_VISIBLE_MS } from '@/lib/limits'
import { ClientSection, clientSectionPath } from '@/lib/routes'

/** The tabs of the page. */
enum DomainTab {
  /** Domain info, dates and billing. */
  Details = 'details',
  /** Nameservers and the registrar commands. */
  Registrar = 'registrar',
  /** Management tools, notes, email forwarding, DNS records and reminders. */
  Management = 'management',
}

const { t } = useI18n()

/** The tab on show. */
const tab = ref<DomainTab>(DomainTab.Details)

/** The tab strip. */
const tabItems = computed<UiTabItem[]>(() => [
  { value: DomainTab.Details, label: t('clients.clientDomainDetail.domainInfo'), testid: 'client-domain-tab-details' },
  { value: DomainTab.Registrar, label: t('shared.registrar'), testid: 'client-domain-tab-registrar' },
  { value: DomainTab.Management, label: t('clients.clientDomainDetail.managementTools'), testid: 'client-domain-tab-management' },
])

/** Longest registration or renewal period, in years. */
const MAX_YEARS = 10

/** Number of nameserver fields. */
const NAMESERVER_COUNT = 5

/** Current route, source of the client and domain ids. */
const route = useRoute()

/** Domains store, the only caller of the domain endpoints. */
const store = useDomainsStore()

/** Client ID from route params. */
const clientId = computed(() => route.params.id as string)

/** Domain ID from route params. */
const domainId = computed(() => route.params.domainId as string)

/** Fetched domain detail, null until loaded (owned by the store). */
const domain = computed(() => store.current)

/** True while the initial load is in flight (owned by the store). */
const loading = computed(() => store.loading)

/** True while the domain is read for the first time: the page keeps its real layout and draws it as placeholders. */
const initialLoading = computed<boolean>(() => loading.value && domain.value === null)

// Every field of the page stays a placeholder until the domain has arrived.
provide(UI_FIELD_LOADING, initialLoading)

/** Error message from the initial load (owned by the store). */
const error = computed(() => store.error)

/** True while save is in flight. */
const saving = ref(false)

/** Success sentence of the last save or registrar command, null when none is on show. */
const saveSuccess = ref<string | null>(null)

/** Timer that hides the success message. */
let savedTimer: ReturnType<typeof setTimeout> | undefined

/** Error message after save or a registrar command (owned by the store). */
const saveError = computed(() => store.actionError)

/** True while a registrar command is in flight. */
const commandLoading = ref(false)

/** Whether the contact modification modal is visible. */
const showContactModal = ref(false)

/** Whether the EPP code modal is visible. */
const showEppModal = ref(false)

/** Whether the renew modal is visible. */
const showRenewModal = ref(false)

/** Whether the register confirmation modal is visible. */
const showRegisterModal = ref(false)

/** Whether the cancel-transfer confirmation modal is visible. */
const showCancelTransferModal = ref(false)

/** Number of years to renew, bound to the renew modal input. */
const renewYears = ref(1)

/** EPP authorization code returned by the registrar. */
const eppCode = ref<string | null>(null)

/** True while contact modification is being saved. */
const contactSaving = ref(false)

/** First payment amount. */
const firstPaymentAmount = ref(0)

/** Recurring renewal amount. */
const recurringAmount = ref(0)

/** Payment method. */
const paymentMethod = ref('')

/** Promotion code. */
const promotionCode = ref('')

/** Subscription ID. */
const subscriptionId = ref('')

/** Internal admin notes. */
const adminNotes = ref('')

/** Expiry date (ISO date string for the date picker). */
const expiresAt = ref('')

/** Next due date (ISO date string for the date picker). */
const nextDueDate = ref('')

/** Registration period in years. */
const registrationPeriod = ref(1)

/** Domain status. */
const status = ref<DomainStatus>(DomainStatus.Active)

/** The nameserver hosts, one per field. */
const nameservers = ref<string[]>(Array.from({ length: NAMESERVER_COUNT }, () => ''))

/** Captions of the nameserver fields. */
const nameserverLabels = computed<string[]>(() => [
  t('clients.clientDomainDetail.nameserver1'),
  t('clients.clientDomainDetail.nameserver2'),
  t('clients.clientDomainDetail.nameserver3'),
  t('clients.clientDomainDetail.nameserver4'),
  t('clients.clientDomainDetail.nameserver5'),
])

/**
 * Copies the EPP code to the clipboard.
 *
 * @returns Promise that resolves when the text is copied.
 */
const copyEppCode = async (): Promise<void> => {
  if (eppCode.value) {
    await navigator.clipboard.writeText(eppCode.value)
  }
}

/**
 * Populates all form refs from the fetched domain detail.
 *
 * @returns Nothing.
 */
const populateForm = (): void => {
  const d = domain.value
  if (!d) {
    return
  }

  firstPaymentAmount.value = d.billing.firstPaymentAmount
  recurringAmount.value = d.billing.recurringAmount
  paymentMethod.value = d.billing.paymentMethod ?? ''
  promotionCode.value = d.billing.promotionCode ?? ''
  subscriptionId.value = d.billing.subscriptionId ?? ''
  adminNotes.value = d.adminNotes ?? ''
  expiresAt.value = toDateInputValue(d.term.expiresAt)
  nextDueDate.value = toDateInputValue(d.term.nextDueDate)
  registrationPeriod.value = d.term.registrationPeriod
  status.value = d.status || DomainStatus.Active

  const ns = d.nameservers ?? []
  nameservers.value = Array.from({ length: NAMESERVER_COUNT }, (_, i) => ns[i]?.host ?? '')
}

/**
 * Fetches the domain detail through the store and populates the form.
 *
 * @returns Promise that resolves when the domain is loaded.
 */
const fetchDomain = async (): Promise<void> => {
  await store.fetchById(domainId.value)
  populateForm()
}

/**
 * Shows a success sentence next to the Save button for a few seconds.
 *
 * @param message - The sentence to show.
 */
const flashSuccess = (message: string): void => {
  saveSuccess.value = message
  clearTimeout(savedTimer)
  savedTimer = setTimeout(() => {
    saveSuccess.value = null
  }, NOTICE_VISIBLE_MS)
}

/**
 * Resets form to the last saved state.
 *
 * @returns Nothing.
 */
const handleCancel = (): void => {
  populateForm()
  store.actionError = null
  saveSuccess.value = null
}

/**
 * Submits all editable field changes to the API.
 *
 * @returns Promise that resolves when the save completes.
 */
const handleSave = async (): Promise<void> => {
  saveSuccess.value = null
  saving.value = true

  const saved = await store.update(domainId.value, {
    firstPaymentAmount: firstPaymentAmount.value,
    recurringAmount: recurringAmount.value,
    paymentMethod: paymentMethod.value.trim() || null,
    promotionCode: promotionCode.value.trim() || null,
    subscriptionId: subscriptionId.value.trim() || null,
    adminNotes: adminNotes.value.trim() || null,
    expiresAt: expiresAt.value || null,
    nextDueDate: nextDueDate.value || null,
    registrationPeriod: registrationPeriod.value,
    status: status.value,
    nameservers: nameservers.value.filter(Boolean),
  })
  if (saved) {
    flashSuccess(t('shared.changesSavedSuccessfully'))
    await fetchDomain()
  }
  saving.value = false
}

/**
 * Opens the register confirmation modal. Only stages the intent: {@link confirmRegister} sends
 * the command, so the question is asked by the app's own modal rather than the browser's dialog.
 *
 * @returns Nothing.
 */
const handleRegister = (): void => {
  showRegisterModal.value = true
}

/**
 * Runs a registrar command with the shared busy flag, reports success next to the Save button and
 * reloads the domain when it succeeds; a refusal shows the store's sentence in the same place.
 *
 * @param command - The store call to run.
 * @returns Promise that resolves when the command and the reload finish.
 */
const runCommand = async (command: () => Promise<boolean>): Promise<void> => {
  saveSuccess.value = null
  commandLoading.value = true
  if (await command()) {
    flashSuccess(t('clients.clientDomainDetail.registrarCommandDone'))
    await fetchDomain()
  }
  commandLoading.value = false
}

/**
 * Sends a register command to the registrar API and closes the confirmation modal.
 *
 * The modal is dismissed before the request so the page's own `commandLoading` state drives the
 * button row, exactly as the renew command beside it does.
 *
 * @returns Promise that resolves when the action completes.
 */
const confirmRegister = async (): Promise<void> => {
  showRegisterModal.value = false
  await runCommand(() => store.register(domainId.value))
}

/**
 * Opens the renew modal with a one-year default.
 *
 * @returns Nothing.
 */
const handleRenew = (): void => {
  renewYears.value = 1
  showRenewModal.value = true
}

/**
 * Confirms the renew modal and submits the renewal request.
 *
 * @returns Promise that resolves when the renewal completes.
 */
const confirmRenew = async (): Promise<void> => {
  const years = renewYears.value
  if (isNaN(years) || years < 1) {
    return
  }
  showRenewModal.value = false
  await runCommand(() => store.renew(domainId.value, years))
}

/**
 * Sends the cancel-transfer command and closes the confirmation modal. A registrar that offers no
 * cancel-transfer operation refuses, and its sentence shows beside the Save button.
 *
 * @returns Promise that resolves when the action completes.
 */
const confirmCancelTransfer = async (): Promise<void> => {
  showCancelTransferModal.value = false
  await runCommand(() => store.cancelTransfer(domainId.value))
}

/**
 * Requests the EPP authorization code from the registrar and shows it.
 *
 * @returns Promise that resolves when the action completes.
 */
const handleGetEppCode = async (): Promise<void> => {
  commandLoading.value = true
  const code = await store.getEppCode(domainId.value)
  if (code !== null) {
    eppCode.value = code
    showEppModal.value = true
  }
  commandLoading.value = false
}

/**
 * Toggles WHOIS ID protection via the registrar API.
 *
 * @returns Promise that resolves when the action completes.
 */
const handleToggleIdProtection = async (): Promise<void> => {
  await runCommand(() => store.setWhoisPrivacy(domainId.value, !domain.value?.features.whoisPrivacy))
}

/**
 * Saves contact details from the modal to the registrar API.
 *
 * @param contact - Contact payload from DomainContactModal.
 * @returns Promise that resolves when the action completes.
 */
const handleModifyContact = async (contact: Record<string, string>): Promise<void> => {
  saveSuccess.value = null
  contactSaving.value = true
  if (await store.modifyContact(domainId.value, contact)) {
    flashSuccess(t('clients.clientDomainDetail.contactSaved'))
    await fetchDomain()
    showContactModal.value = false
  }
  contactSaving.value = false
}

/**
 * Toggles DNS management for the domain.
 *
 * @param val - The desired toggle state.
 * @returns Promise that resolves when the action completes.
 */
const handleToggleDnsManagement = async (val: boolean): Promise<void> => {
  await runCommand(() => store.setDnsManagement(domainId.value, val))
}

/**
 * Toggles email forwarding for the domain.
 *
 * @param val - The desired toggle state.
 * @returns Promise that resolves when the action completes.
 */
const handleToggleEmailForwarding = async (val: boolean): Promise<void> => {
  await runCommand(() => store.setEmailForwarding(domainId.value, val))
}

/**
 * Toggles auto-renew for the domain.
 *
 * @param val - The desired toggle state.
 * @returns Promise that resolves when the action completes.
 */
const handleToggleAutoRenew = async (val: boolean): Promise<void> => {
  await runCommand(() => store.setAutoRenew(domainId.value, val))
}

/**
 * Toggles registrar lock for the domain.
 *
 * @param val - The desired toggle state.
 * @returns Promise that resolves when the action completes.
 */
const handleToggleLock = async (val: boolean): Promise<void> => {
  await runCommand(() => store.setLock(domainId.value, val))
}

// Populate form when domain data changes
watch(domain, () => populateForm())

onMounted(() => fetchDomain())

onBeforeUnmount(() => {
  clearTimeout(savedTimer)
})
</script>

<template>
  <UiStack>
    <UiErrorState
      v-if="error && !domain"
      :message="error"
      :loading="loading"
      testid="client-domain-error"
      @retry="fetchDomain()"
    />

    <template v-else>
      <UiForm stacked :aria-busy="initialLoading ? 'true' : undefined" @submit="handleSave">
        <UiCard padding="md" :data-testid="domain ? 'client-domain-header' : 'client-domain-loading'">
          <UiStack direction="row" wrap :justify="UiJustify.Between" :align="UiAlignItems.Center">
            <UiStack direction="row" wrap :align="UiAlignItems.Center">
              <UiLink
                :to="clientSectionPath(clientId, ClientSection.Domains)"
                :variant="UiButtonVariant.Secondary"
                data-testid="client-domain-back"
              >
                <UiIcon :markup="Icon.Back" :size="UiSize.Sm" />
                {{ t('shared.back') }}
              </UiLink>
              <template v-if="domain">
                <UiHeading :level="1" variant="page">
                  {{ t('clients.clientDomainDetail.domainName', { name: domain.name }) }}
                </UiHeading>
                <UiBadge :tone="domainStatusTone(domain.status)" data-testid="client-domain-status-badge">
                  {{ statusLabel(DOMAIN_STATUS_OPTIONS, domain.status) }}
                </UiBadge>
              </template>
              <UiStack v-else direction="row" :align="UiAlignItems.Center" aria-hidden="true">
                <UiSkeletonBar class="h-9 w-56 rounded-md" />
                <UiSkeletonBar class="h-5 w-20 rounded-full" />
              </UiStack>
            </UiStack>
            <UiStack direction="row" wrap :align="UiAlignItems.Center" gap="sm">
              <UiAlert v-if="saveSuccess" variant="success" data-testid="client-domain-saved">
                {{ saveSuccess }}
              </UiAlert>
              <UiAlert v-if="saveError" variant="error" data-testid="client-domain-action-error">
                {{ saveError }}
              </UiAlert>
              <UiButton
                type="button"
                :variant="UiButtonVariant.Secondary"
                data-testid="client-domain-cancel"
                @click="handleCancel"
              >
                {{ t('shared.cancel') }}
              </UiButton>
              <UiButton
                type="submit"
                :variant="UiButtonVariant.Primary"
                :loading="saving"
                :disabled="initialLoading"
                data-testid="client-domain-save"
              >
                {{ t('shared.saveChanges') }}
              </UiButton>
            </UiStack>
          </UiStack>
        </UiCard>

        <UiTabs v-model="tab" :items="tabItems" :aria-label="t('clients.clientDomainDetail.domainInfo')" />

        <UiGrid v-if="tab === DomainTab.Details" :columns="2">
          <UiCard padding="md">
            <UiStack>
              <UiHeading :level="2" variant="card">{{ t('clients.clientDomainDetail.domainInfo') }}</UiHeading>
              <UiSkeletonBar v-if="!domain" class="h-4 w-full max-w-md rounded-md" aria-hidden="true" />
              <UiStack v-else direction="row" gap="lg" wrap>
                <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">
                  {{ t('clients.clientDomainDetail.order') }} {{ domain.origin.orderId ?? EMPTY_VALUE }}
                </UiText>
                <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">
                  {{ t('clients.clientDomainDetail.orderType') }} {{ domain.origin.orderType }}
                </UiText>
                <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">
                  {{ t('clients.clientDomainDetail.domain') }} {{ domain.name }}
                </UiText>
                <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">
                  {{ t('clients.clientDomainDetail.registrar') }} {{ domain.registrar.module ?? EMPTY_VALUE }}
                </UiText>
              </UiStack>
              <UiGrid :columns="2" gap="sm">
                <UiField :label="t('clients.clientDomainDetail.firstPaymentAmount')">
                  <UiNumberInput data-testid="client-domain-first-payment" v-model="firstPaymentAmount" :step="0.01" :min="0" placeholder="0.00" />
                </UiField>
                <UiField :label="t('clients.clientDomainDetail.recurringAmount')">
                  <UiNumberInput data-testid="client-domain-recurring" v-model="recurringAmount" :step="0.01" :min="0" placeholder="0.00" />
                </UiField>
                <UiField :label="t('clients.clientDomainDetail.promotionCode')">
                  <template #default="{ id }">
                    <UiInput
                      :id="id"
                      v-model="promotionCode"
                      type="text"
                      :placeholder="t('shared.examples.promoCode')"
                      data-testid="client-domain-promotion"
                    />
                  </template>
                </UiField>
                <UiField :label="t('clients.clientDomainDetail.subscriptionId')">
                  <template #default="{ id }">
                    <UiInput
                      :id="id"
                      v-model="subscriptionId"
                      type="text"
                      :placeholder="t('shared.examples.subscriptionId')"
                      data-testid="client-domain-subscription"
                    />
                  </template>
                </UiField>
              </UiGrid>
            </UiStack>
          </UiCard>

          <UiCard padding="md">
            <UiStack>
              <UiHeading :level="2" variant="card">{{ t('clients.clientDomainDetail.datesBilling') }}</UiHeading>
              <UiGrid :columns="2" gap="sm">
                <UiField :label="t('clients.clientDomainDetail.registrationPeriod')">
                  <UiStack direction="row" :align="UiAlignItems.Center" gap="sm">
                    <UiNumberInput data-testid="client-domain-registration-period" v-model="registrationPeriod" :min="1" :max="MAX_YEARS" :step="1" placeholder="1" />
                    <UiText :tone="UiTextTone.Muted">{{ t('clients.clientDomainDetail.years') }}</UiText>
                  </UiStack>
                </UiField>
                <UiField :label="t('shared.registrationDate')">
                  <UiText :tone="UiTextTone.Muted">{{ domain ? formatDate(domain.term.registeredAt) : '' }}</UiText>
                </UiField>
                <UiField :label="t('shared.expiryDate')">
                  <UiDatePicker v-model="expiresAt" :direction="UiDatePickerDirection.Future" />
                </UiField>
                <UiField :label="t('shared.nextDueDate')">
                  <UiDatePicker v-model="nextDueDate" :direction="UiDatePickerDirection.Future" />
                </UiField>
                <UiField :label="t('shared.paymentMethod')">
                  <UiSelect data-testid="client-domain-payment-method" v-model="paymentMethod" :options="PAYMENT_METHOD_OPTIONS" :placeholder="t('shared.paymentMethod')" />
                </UiField>
                <UiField :label="t('shared.status')">
                  <UiSelect data-testid="client-domain-status" v-model="status" :options="DOMAIN_STATUS_OPTIONS" :placeholder="t('shared.status')" />
                </UiField>
              </UiGrid>
            </UiStack>
          </UiCard>
        </UiGrid>

        <UiGrid v-else-if="domain && tab === DomainTab.Registrar" :columns="2">
          <UiCard padding="md">
            <UiStack>
              <UiHeading :level="2" variant="card">{{ t('clients.clientDomainDetail.nameservers') }}</UiHeading>
              <UiField v-for="(label, idx) in nameserverLabels" :key="label" :label="label">
                <template #default="{ id }">
                  <UiInput
                    :id="id"
                    v-model="nameservers[idx]"
                    type="text"
                    :placeholder="t('servers.serverForm.nsHostPlaceholder', { n: idx + 1 })"
                    :data-testid="`client-domain-ns-${idx + 1}`"
                  />
                </template>
              </UiField>
            </UiStack>
          </UiCard>

          <UiCard padding="md">
            <UiStack>
              <UiHeading :level="2" variant="card">{{ t('clients.clientDomainDetail.registrarCommands') }}</UiHeading>
              <UiStack direction="row" wrap gap="sm">
                <UiButton
                  type="button"
                  :variant="UiButtonVariant.Secondary"
                  :loading="commandLoading"
                  :disabled="domain.status === DomainStatus.Active"
                  data-testid="client-domain-register"
                  @click="handleRegister"
                >
                  {{ t('clients.clientDomainDetail.register') }}
                </UiButton>
                <UiButton
                  type="button"
                  :variant="UiButtonVariant.Secondary"
                  :loading="commandLoading"
                  data-testid="client-domain-renew"
                  @click="handleRenew"
                >
                  {{ t('clients.clientDomainDetail.renew') }}
                </UiButton>
                <UiButton
                  v-if="domain.status === DomainStatus.PendingTransfer"
                  type="button"
                  :variant="UiButtonVariant.Secondary"
                  :loading="commandLoading"
                  data-testid="client-domain-cancel-transfer"
                  @click="showCancelTransferModal = true"
                >
                  {{ t('clients.clientDomainDetail.cancelTransfer') }}
                </UiButton>
                <UiButton
                  type="button"
                  :variant="UiButtonVariant.Secondary"
                  :disabled="commandLoading"
                  data-testid="client-domain-contact"
                  @click="showContactModal = true"
                >
                  {{ t('clients.clientDomainDetail.modifyContactDetails') }}
                </UiButton>
                <UiButton
                  type="button"
                  :variant="UiButtonVariant.Secondary"
                  :loading="commandLoading"
                  data-testid="client-domain-epp"
                  @click="handleGetEppCode"
                >
                  {{ t('clients.clientDomainDetail.getEppCode') }}
                </UiButton>
                <UiButton
                  type="button"
                  :variant="domain.features.whoisPrivacy ? UiButtonVariant.Danger : UiButtonVariant.Secondary"
                  :loading="commandLoading"
                  data-testid="client-domain-id-protection"
                  @click="handleToggleIdProtection"
                >
                  {{ domain.features.whoisPrivacy ? t('clients.clientDomainDetail.disableIdProtection') : t('clients.clientDomainDetail.enableIdProtection') }}
                </UiButton>
              </UiStack>
            </UiStack>
          </UiCard>
        </UiGrid>

        <UiGrid v-else-if="domain" :columns="2">
          <UiCard padding="md">
            <UiStack>
              <UiHeading :level="2" variant="card">{{ t('clients.clientDomainDetail.managementTools') }}</UiHeading>
              <UiLabel :variant="UiLabelVariant.Inline">
                <UiToggle
                  :model-value="domain.features.dnsManagement"
                  data-testid="client-domain-toggle-dns"
                  @update:model-value="handleToggleDnsManagement"
                />
                {{ t('clients.clientDomainDetail.dnsManagement') }}
              </UiLabel>
              <UiLabel :variant="UiLabelVariant.Inline">
                <UiToggle
                  :model-value="domain.features.emailForwarding"
                  data-testid="client-domain-toggle-forwarding"
                  @update:model-value="handleToggleEmailForwarding"
                />
                {{ t('clients.clientDomainDetail.emailForwarding') }}
              </UiLabel>
              <UiLabel :variant="UiLabelVariant.Inline">
                <UiToggle
                  :model-value="domain.features.whoisPrivacy"
                  data-testid="client-domain-toggle-id-protection"
                  @update:model-value="handleToggleIdProtection"
                />
                {{ t('clients.clientDomainDetail.idProtection') }}
              </UiLabel>
              <UiLabel :variant="UiLabelVariant.Inline">
                <UiToggle
                  :model-value="domain.features.autoRenew"
                  data-testid="client-domain-toggle-auto-renew"
                  @update:model-value="handleToggleAutoRenew"
                />
                {{ t('clients.clientDomainDetail.autoRenew') }}
              </UiLabel>
              <UiLabel :variant="UiLabelVariant.Inline">
                <UiToggle
                  :model-value="domain.features.isLocked"
                  data-testid="client-domain-toggle-lock"
                  @update:model-value="handleToggleLock"
                />
                {{ t('clients.clientDomainDetail.registrarLock') }}
              </UiLabel>
            </UiStack>
          </UiCard>

          <UiCard padding="md">
            <UiStack>
              <UiHeading :level="2" variant="card">{{ t('shared.adminNotes') }}</UiHeading>
              <UiTextarea
                v-model="adminNotes"
                rows="6"
                :placeholder="t('shared.internalNotesVisibleOnlyTo')"
                data-testid="client-domain-admin-notes"
              />
            </UiStack>
          </UiCard>
        </UiGrid>
      </UiForm>

      <template v-if="domain && tab === DomainTab.Management">
        <EmailForwardingTable
          v-if="domain.features.emailForwarding"
          :domain-id="Number(domainId)"
          :rules="domain.emailForwardingRules"
          @refresh="fetchDomain"
        />

        <DnsRecordsTable
          v-if="domain.features.dnsManagement"
          :domain-id="Number(domainId)"
          :records="domain.dnsRecords"
          @refresh="fetchDomain"
        />

        <DomainRemindersTable :reminders="domain.reminders" />
      </template>

      <DomainContactModal
        v-if="showContactModal"
        :saving="contactSaving"
        @save="handleModifyContact"
        @close="showContactModal = false"
      />

      <UiDialog
        v-if="showRenewModal"
        :title="t('clients.clientDomainDetail.renewDomain')"
        @close="showRenewModal = false"
      >
        <UiField :label="t('clients.clientDomainDetail.numberOfYears')">
          <UiNumberInput data-testid="client-domain-renew-years" v-model="renewYears" :min="1" :max="MAX_YEARS" placeholder="1" />
        </UiField>
        <template #footer>
          <UiButton
            :variant="UiButtonVariant.Secondary"
            data-testid="client-domain-renew-cancel"
            @click="showRenewModal = false"
          >
            {{ t('shared.cancel') }}
          </UiButton>
          <UiButton
            :variant="UiButtonVariant.Primary"
            data-testid="client-domain-renew-confirm"
            @click="confirmRenew"
          >
            {{ t('clients.clientDomainDetail.renew') }}
          </UiButton>
        </template>
      </UiDialog>

      <UiDialog
        v-if="showRegisterModal"
        variant="confirm"
        :title="t('clients.clientDomainDetail.registerDomain')"
        :message="t('clients.clientDomainDetail.registerThisDomainWithThe')"
        :confirm-label="t('clients.clientDomainDetail.register')"
        :loading="commandLoading"
        tone="primary"
        @confirm="confirmRegister"
        @close="showRegisterModal = false"
      />

      <UiDialog
        v-if="showCancelTransferModal"
        variant="confirm"
        :title="t('clients.clientDomainDetail.cancelTransfer')"
        :message="t('clients.clientDomainDetail.cancelThisTransferAtThe')"
        :confirm-label="t('clients.clientDomainDetail.cancelTransfer')"
        :loading="commandLoading"
        tone="danger"
        @confirm="confirmCancelTransfer"
        @close="showCancelTransferModal = false"
      />

      <UiDialog
        v-if="showEppModal"
        :title="t('clients.clientDomainDetail.eppAuthorizationCode')"
        size="md"
        @close="showEppModal = false"
      >
        <UiText :tone="UiTextTone.Secondary">{{ t('clients.clientDomainDetail.useThisCodeToTransfer') }}</UiText>
        <UiStack direction="row" :align="UiAlignItems.Center" gap="sm">
          <UiText :tone="UiTextTone.Primary" class="flex-1 select-all font-mono tracking-widest" data-testid="client-domain-epp-code">
            {{ eppCode }}
          </UiText>
          <UiButton
            :variant="UiButtonVariant.Secondary"
            :size="UiSize.Sm"
            data-testid="client-domain-epp-copy"
            @click="copyEppCode"
          >
            {{ t('clients.clientDomainDetail.copy') }}
          </UiButton>
        </UiStack>
        <template #footer>
          <UiButton
            :variant="UiButtonVariant.Secondary"
            data-testid="client-domain-epp-close"
            @click="showEppModal = false"
          >
            {{ t('shared.close') }}
          </UiButton>
        </template>
      </UiDialog>
    </template>
  </UiStack>
</template>
