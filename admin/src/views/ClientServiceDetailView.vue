<script setup lang="ts">
/**
 * Editable service detail page. Each section (hosting account, billing, automation, notes, status and
 * the termination date) saves through its own endpoint with its own, distinctly named button, and
 * shows its outcome (success, or the API's sentence) beside that button in a live region that is
 * always rendered. Supports lifecycle actions: Suspend, Unsuspend, Terminate; terminating, and
 * moving the status to Terminated, is confirmed first.
 */
import { ref, computed, onMounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useServicesStore } from '@/stores/services'
import { useServersStore } from '@/stores/servers'
import { useSettingsStore } from '@/stores/settings'
import { SERVICE_STATUS_OPTIONS, BILLING_CYCLE_OPTIONS } from '@/lib/constants'
import { BillingCycle, ServiceChangeReason, ServiceSection, ServiceStatus } from '@/types/service'
import { formatDate, toDateInputValue } from '@/lib/format'
import type { ServiceListItem } from '@/types/service'
import PluginSlot from '@/components/plugins/PluginSlot.vue'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiDatePicker from '@/components/ui/UiDatePicker.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
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
import UiSkeleton from '@/components/ui/UiSkeleton.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import UiTextarea from '@/components/ui/UiTextarea.vue'
import UiToggle from '@/components/ui/UiToggle.vue'
import { PluginSlotId } from '@/types/pluginslot'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiAlignItems, UiJustify } from '@/utils/ui/layout'
import { UiSize } from '@/utils/ui/size'
import { UiLabelVariant, UiTextTone } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'
import { UiDialogVariant } from '@/utils/ui/dialog'
import { ClientSection, clientItemPath, clientSectionPath } from '@/lib/routes'

const { t } = useI18n()

/** Icon markup (shape tags) of the back link. */
const BACK_ICON = '<polyline points="15 18 9 12 15 6"/>'

/** Icon markup (shape tags) of the control panel button. */
const PANEL_ICON = '<path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6" /><polyline points="15 3 21 3 21 9" /><line x1="10" y1="14" x2="21" y2="3" />'

/** Default billing cycle. */
const DEFAULT_CYCLE = BillingCycle.Monthly

/**
 * Statuses that end the service; moving to one is confirmed like the Terminate command. A service
 * has no Cancelled status (the API's set is Pending, Active, Suspended, Terminated), so Terminated
 * is the only one.
 */
const TERMINAL_STATUSES: ServiceStatus[] = [ServiceStatus.Terminated]

/** The irreversible changes the danger confirmation guards. */
enum DangerAction {
  /** The Terminate module command. */
  Terminate = 'terminate',
  /** Saving the status section with a terminal status. */
  StatusChange = 'status-change',
}

/** The outcome of one save or command, shown beside its button. */
interface ActionOutcome {
  /** True when the request was refused. */
  failed: boolean
  /** The finished sentence: the success line or the API's own sentence. */
  text: string
}

/** Visible (and accessible) name of each section's save button, so the buttons are told apart. */
const SAVE_LABEL_KEYS: Record<ServiceSection, string> = {
  [ServiceSection.HostingAccount]: 'clients.clientServiceDetail.saveServiceInfo',
  [ServiceSection.Billing]: 'clients.clientServiceDetail.saveBilling',
  [ServiceSection.Automation]: 'clients.clientServiceDetail.saveAutomation',
  [ServiceSection.Notes]: 'clients.clientServiceDetail.saveNotes',
  [ServiceSection.Status]: 'clients.clientServiceDetail.changeStatus',
  [ServiceSection.TerminationDate]: 'clients.clientServiceDetail.saveTerminationDate',
}

/** Current route, source of the client and service ids. */
const route = useRoute()

/** Router, used by the service switcher. */
const router = useRouter()

/** Services store, the only caller of the service endpoints. */
const store = useServicesStore()

/** Servers store, source of the server dropdown. */
const serversStore = useServersStore()

/** Settings store, source of the product dropdown. */
const settingsStore = useSettingsStore()

/** Client ID from route params. */
const clientId = computed(() => route.params.id as string)

/** Service ID from route params. */
const serviceId = computed(() => route.params.serviceId as string)

/** Fetched service detail, null until loaded (owned by the store). */
const service = computed(() => store.current)

/** True while the initial load is in flight (owned by the store). */
const loading = computed(() => store.loading)

/** Error message from the initial load (owned by the store). */
const error = computed(() => store.error)

/** The section whose save is in flight, null when none. */
const saving = ref<ServiceSection | null>(null)

/** The outcome of the last save of each section, keyed by section. */
const sectionOutcomes = ref<Partial<Record<ServiceSection, ActionOutcome>>>({})

/** The outcome of the last module command, shown in the module commands card. */
const commandOutcome = ref<ActionOutcome | null>(null)

/** The sentence of a refused control panel sign-in, shown beside its button. */
const panelError = ref<string | null>(null)

/** True while a module command (suspend/unsuspend/terminate) is in flight. */
const commandLoading = ref(false)

/** Service domain. */
const domain = ref('')

/** Dedicated IP address. */
const dedicatedIp = ref('')

/** Control panel username. */
const username = ref('')

/** Control panel password. */
const password = ref('')

/** Recurring charge amount. */
const recurringAmount = ref(0)

/** Next due date (ISO date string). */
const nextDueDate = ref('')

/** Billing cycle. */
const billingCycle = ref<BillingCycle>(DEFAULT_CYCLE)

/** Payment method. */
const paymentMethod = ref('')

/** Automatic billing toggle; off means staff bill the service by hand and no billing run touches it. */
const autoRenew = ref(true)

/** Override auto-suspend toggle. */
const overrideAutoSuspend = ref(false)

/** Suspend-until date (ISO date string). */
const suspendUntil = ref('')

/** Auto-terminate at end of cycle toggle. */
const autoTerminateEndOfCycle = ref(false)

/** Auto-terminate reason. */
const autoTerminateReason = ref('')

/** Internal admin notes. */
const adminNotes = ref('')

/** Provisioning reference / server. */
const provisioningRef = ref('')

/** First payment amount. */
const firstPaymentAmount = ref(0)

/** Promotion code. */
const promotionCode = ref('')

/** Termination date (ISO date string). */
const terminatedAt = ref('')

/** Status (editable dropdown). */
const status = ref<ServiceStatus>(ServiceStatus.Active)

/** Subscription ID. */
const subscriptionId = ref('')

/** Quantity of units purchased. */
const quantity = ref(1)

/** FK to the assigned server. */
const serverId = ref<number | null>(null)

/** FK to the ordered product (for changing product). */
const productIdRef = ref<number | null>(null)

/** All services for the current client (for service switcher). */
const clientServices = ref<ServiceListItem[]>([])

/** Available servers for dropdown. */
const serverList = computed(() => serversStore.servers)

/** Available products for dropdown. */
const productList = computed(() => settingsStore.products)

/** Whether the change password modal is visible. */
const showChangePasswordModal = ref(false)

/** New password for change password modal. */
const newPassword = ref('')

/** Whether the change package modal is visible. */
const showChangePackageModal = ref(false)

/** New package name for change package modal. */
const newPackage = ref('')

/** Whether the suspend confirmation modal is visible. */
const showSuspendModal = ref(false)

/** The irreversible change waiting for confirmation, null when the prompt is closed. */
const dangerConfirm = ref<DangerAction | null>(null)

/** Whether SSO URL is loading. */
const ssoLoading = ref(false)

/** Status options for dropdown. */
const statusOptions = SERVICE_STATUS_OPTIONS

/** Billing cycle options for the UiSelect dropdown. */
const billingCycleOptions = BILLING_CYCLE_OPTIONS

/** Server options for UiSelect dropdown. */
const serverOptions = computed(() => [
  { value: 0, label: t('shared.none') },
  ...serverList.value.map(s => ({
    value: s.id,
    label: `${s.name} — ${s.endpoint.hostname} (${s.accountsCount ?? 0}/${s.limits.maxAccounts ?? '∞'})`,
  })),
])

/** Product options for UiSelect dropdown. */
const productOptions = computed(() =>
  productList.value.map(p => ({ value: p.id, label: p.name })),
)

/** Service switcher options for UiSelect dropdown. */
const serviceSwitcherOptions = computed(() =>
  clientServices.value.map(s => ({
    value: s.id,
    label: `#${s.id} — ${s.productName}`,
  })),
)

/** Title of the danger confirmation. */
const dangerTitle = computed<string>(() =>
  dangerConfirm.value === DangerAction.StatusChange
    ? t('clients.clientServiceDetail.changeStatusTo', { status: status.value })
    : t('clients.clientServiceDetail.terminateService'),
)

/** Confirm label of the danger confirmation. */
const dangerConfirmLabel = computed<string>(() =>
  dangerConfirm.value === DangerAction.StatusChange
    ? t('clients.clientServiceDetail.changeStatus')
    : t('clients.clientServiceDetail.terminate'),
)

/** True while the confirmed irreversible change runs. */
const dangerLoading = computed<boolean>(() => commandLoading.value || saving.value === ServiceSection.Status)

/**
 * The outcome of a section's last save.
 *
 * @param section - The section.
 * @returns The outcome, or undefined when the section has none to show.
 */
const outcomeOf = (section: ServiceSection): ActionOutcome | undefined => sectionOutcomes.value[section]

/**
 * The tone of an outcome.
 *
 * @param outcome - The outcome, if any.
 * @returns Danger for a refusal, success otherwise.
 */
const outcomeTone = (outcome: ActionOutcome | null | undefined): UiTone =>
  outcome?.failed === true ? UiTone.Danger : UiTone.Success

/**
 * The finished outcome of a request that just settled: the success line, or the API's sentence.
 *
 * @param ok - Whether the request was accepted.
 * @param successKey - Locale key of the success line.
 * @returns The outcome to show.
 */
const settle = (ok: boolean, successKey: string): ActionOutcome =>
  ok
    ? { failed: false, text: t(successKey) }
    : { failed: true, text: store.actionError ?? t('errors.noAnswer') }

/**
 * Populates the hosting account fields from the fetched service detail.
 *
 * @returns Nothing.
 */
const populateHostingAccount = (): void => {
  const s = service.value
  if (!s) {
    return
  }
  serverId.value = s.serverId ?? null
  domain.value = s.domain ?? ''
  provisioningRef.value = s.account.provisioningRef ?? ''
  dedicatedIp.value = s.account.dedicatedIp ?? ''
  username.value = s.account.username ?? ''
  password.value = s.account.password ?? ''
}

/**
 * Populates the billing fields from the fetched service detail.
 *
 * @returns Nothing.
 */
const populateBilling = (): void => {
  const s = service.value
  if (!s) {
    return
  }
  productIdRef.value = s.productId
  quantity.value = s.billing.quantity
  billingCycle.value = s.billing.billingCycle || DEFAULT_CYCLE
  firstPaymentAmount.value = s.billing.firstPaymentAmount
  recurringAmount.value = s.billing.recurringAmount
  nextDueDate.value = toDateInputValue(s.billing.nextDueDate)
  paymentMethod.value = s.billing.paymentMethod ?? ''
  promotionCode.value = s.billing.promotionCode ?? ''
  subscriptionId.value = s.billing.subscriptionId ?? ''
}

/**
 * Populates the automation fields from the fetched service detail.
 *
 * @returns Nothing.
 */
const populateAutomation = (): void => {
  const s = service.value
  if (!s) {
    return
  }
  autoRenew.value = s.billing.autoRenew
  overrideAutoSuspend.value = s.automation.overrideAutoSuspend
  suspendUntil.value = toDateInputValue(s.automation.suspendUntil)
  autoTerminateEndOfCycle.value = s.automation.autoTerminateEndOfCycle
  autoTerminateReason.value = s.automation.autoTerminateReason ?? ''
}

/**
 * Populates the admin notes from the fetched service detail.
 *
 * @returns Nothing.
 */
const populateNotes = (): void => {
  adminNotes.value = service.value?.adminNotes ?? ''
}

/**
 * Populates the status dropdown from the fetched service detail.
 *
 * @returns Nothing.
 */
const populateStatus = (): void => {
  status.value = service.value?.status || ServiceStatus.Active
}

/**
 * Populates the termination date from the fetched service detail.
 *
 * @returns Nothing.
 */
const populateTerminationDate = (): void => {
  terminatedAt.value = toDateInputValue(service.value?.terminatedAt)
}

/** Fills one section's form fields from the fetched service detail, keyed by section. */
const sectionPopulators: Record<ServiceSection, () => void> = {
  [ServiceSection.HostingAccount]: populateHostingAccount,
  [ServiceSection.Billing]: populateBilling,
  [ServiceSection.Automation]: populateAutomation,
  [ServiceSection.Notes]: populateNotes,
  [ServiceSection.Status]: populateStatus,
  [ServiceSection.TerminationDate]: populateTerminationDate,
}

/**
 * Populates every section's form fields from the fetched service detail.
 *
 * @returns Nothing.
 */
const populateForm = (): void => {
  if (!service.value) {
    return
  }
  Object.values(sectionPopulators).forEach(populate => {
    populate()
  })
}

/**
 * Fetches the service detail through the store and populates the whole form.
 *
 * @returns Promise that resolves when the service is loaded.
 */
const fetchService = async (): Promise<void> => {
  if (await store.fetchById(serviceId.value)) {
    populateForm()
  }
}

/**
 * Reloads the service after one section was saved and refreshes only that section's fields, so
 * unsaved edits in the other sections stay.
 *
 * @param section - The section that was saved.
 * @returns Promise that resolves when the service is reloaded.
 */
const refreshSection = async (section: ServiceSection): Promise<void> => {
  if (await store.fetchById(serviceId.value)) {
    sectionPopulators[section]()
  }
}

/**
 * Fetches all services for the current client (service switcher).
 *
 * @returns Promise that resolves when services are loaded.
 */
const fetchClientServices = async (): Promise<void> => {
  clientServices.value = await store.fetchClientServices(clientId.value)
}

/**
 * Resets form to the last saved state and clears every outcome line.
 *
 * @returns Nothing.
 */
const handleCancel = (): void => {
  populateForm()
  sectionOutcomes.value = {}
  commandOutcome.value = null
  panelError.value = null
}

/**
 * Runs one section's save with its busy flag and shows the outcome beside that section's button;
 * on success it reloads the service and refreshes only that section's fields. The section's
 * previous outcome is cleared first (the store clears its `actionError` when the request starts),
 * so a stale refusal never sits next to a new attempt. A press while any save runs is ignored:
 * the buttons stay focusable (`aria-disabled`) so keyboard focus is not lost.
 *
 * @param section - The section being saved.
 * @param save - The store call that saves it.
 * @returns Promise that resolves when the save completes.
 */
const saveSection = async (section: ServiceSection, save: () => Promise<boolean>): Promise<void> => {
  if (saving.value !== null) {
    return
  }
  sectionOutcomes.value = { ...sectionOutcomes.value, [section]: undefined }
  saving.value = section
  const saved = await save()
  saving.value = null
  sectionOutcomes.value = { ...sectionOutcomes.value, [section]: settle(saved, 'shared.changesSavedSuccessfully') }
  if (saved) {
    await refreshSection(section)
  }
}

/**
 * Saves the hosting account section: server, domain, panel reference, dedicated IP and credentials.
 *
 * @returns Promise that resolves when the save completes.
 */
const saveHostingAccount = (): Promise<void> =>
  saveSection(ServiceSection.HostingAccount, () => store.updateHostingAccount(serviceId.value, {
    serverId: serverId.value,
    domain: domain.value.trim() || null,
    provisioningRef: provisioningRef.value.trim() || null,
    dedicatedIp: dedicatedIp.value.trim() || null,
    username: username.value.trim() || null,
    password: password.value.trim() || null,
  }))

/**
 * Saves the billing section: product, quantity, cycle, amounts, next due date and payment references.
 *
 * @returns Promise that resolves when the save completes.
 */
const saveBillingTerms = async (): Promise<void> => {
  if (productIdRef.value === null) {
    return
  }
  const productId = productIdRef.value
  await saveSection(ServiceSection.Billing, () => store.updateBillingTerms(serviceId.value, {
    productId,
    quantity: quantity.value,
    billingCycle: billingCycle.value,
    firstPaymentAmount: firstPaymentAmount.value,
    recurringAmount: recurringAmount.value,
    nextRenewalAt: nextDueDate.value || null,
    paymentMethod: paymentMethod.value.trim() || null,
    promotionCode: promotionCode.value.trim() || null,
    subscriptionId: subscriptionId.value.trim() || null,
  }))
}

/**
 * Saves the automation section. A hold date or a reason is only sent with its switch on, which is
 * what the API accepts.
 *
 * @returns Promise that resolves when the save completes.
 */
const saveAutomation = (): Promise<void> =>
  saveSection(ServiceSection.Automation, () => store.updateAutomation(serviceId.value, {
    autoRenew: autoRenew.value,
    overrideAutoSuspend: overrideAutoSuspend.value,
    suspendUntil: overrideAutoSuspend.value && suspendUntil.value ? suspendUntil.value : null,
    autoTerminateEndOfCycle: autoTerminateEndOfCycle.value,
    autoTerminateReason: autoTerminateEndOfCycle.value ? autoTerminateReason.value.trim() || null : null,
  }))

/**
 * Saves the admin notes.
 *
 * @returns Promise that resolves when the save completes.
 */
const saveNotes = (): Promise<void> =>
  saveSection(ServiceSection.Notes, () => store.updateNotes(serviceId.value, {
    adminNotes: adminNotes.value.trim() || null,
  }))

/**
 * Moves the service to the status chosen in the dropdown.
 *
 * @returns Promise that resolves when the change completes.
 */
const applyStatus = (): Promise<void> =>
  saveSection(ServiceSection.Status, () => store.changeStatus(serviceId.value, status.value))

/**
 * Saves the status section; a terminal status (see {@link TERMINAL_STATUSES}) opens the danger
 * confirmation first.
 *
 * @returns Promise that resolves when the change completes or the prompt is open.
 */
const saveStatus = async (): Promise<void> => {
  if (saving.value !== null) {
    return
  }
  if (TERMINAL_STATUSES.includes(status.value)) {
    dangerConfirm.value = DangerAction.StatusChange
    return
  }
  await applyStatus()
}

/**
 * Corrects the termination date of a terminated service.
 *
 * @returns Promise that resolves when the save completes.
 */
const saveTerminationDate = async (): Promise<void> => {
  if (!terminatedAt.value) {
    return
  }
  const date = terminatedAt.value
  await saveSection(ServiceSection.TerminationDate, () => store.correctTerminationDate(serviceId.value, date))
}

/**
 * Opens a control panel single-sign-on URL in a new browser tab.
 *
 * @returns Promise that resolves when the SSO URL is retrieved.
 */
const handleLoginToPanel = async (): Promise<void> => {
  panelError.value = null
  ssoLoading.value = true
  const url = await store.fetchPanelLoginUrl(serviceId.value)
  ssoLoading.value = false
  if (url === null) {
    panelError.value = store.actionError ?? t('errors.noAnswer')
    return
  }
  window.open(url, '_blank')
}

/**
 * Runs a provisioning command with the shared busy flag, shows its outcome in the module commands
 * card and reloads the service when it succeeds.
 *
 * @param command - The store call to run.
 * @param successKey - Locale key of the line shown when the command is accepted.
 * @returns Promise that resolves with whether the command succeeded.
 */
const runCommand = async (command: () => Promise<boolean>, successKey: string): Promise<boolean> => {
  commandOutcome.value = null
  commandLoading.value = true
  const ok = await command()
  commandLoading.value = false
  commandOutcome.value = settle(ok, successKey)
  if (ok) {
    await fetchService()
  }
  return ok
}

/**
 * Provisions a pending service on the hosting server.
 *
 * @returns Promise that resolves when provisioning completes.
 */
const handleProvision = async (): Promise<void> => {
  await runCommand(() => store.provision(serviceId.value), 'clients.clientServiceDetail.provisioned')
}

/**
 * Changes the hosting account password via the provisioning server.
 *
 * @returns Promise that resolves when the password is changed.
 */
const handleChangePassword = async (): Promise<void> => {
  if (!newPassword.value.trim()) {
    return
  }
  if (await runCommand(() => store.changePassword(serviceId.value, newPassword.value), 'clients.clientServiceDetail.passwordChanged')) {
    showChangePasswordModal.value = false
    newPassword.value = ''
  }
}

/**
 * Changes the hosting package via the provisioning server.
 *
 * @returns Promise that resolves when the package is changed.
 */
const handleChangePackage = async (): Promise<void> => {
  if (!newPackage.value.trim()) {
    return
  }
  if (await runCommand(() => store.changePackage(serviceId.value, newPackage.value), 'clients.clientServiceDetail.packageChanged')) {
    showChangePackageModal.value = false
    newPackage.value = ''
  }
}

/**
 * Suspends the service via the API and re-fetches the detail.
 *
 * @returns Promise that resolves when the action completes.
 */
const handleSuspend = async (): Promise<void> => {
  showSuspendModal.value = false
  await runCommand(() => store.suspend(serviceId.value, ServiceChangeReason.Suspended), 'clients.clientServiceDetail.suspended')
}

/**
 * Unsuspends the service via the API and re-fetches the detail.
 *
 * @returns Promise that resolves when the action completes.
 */
const handleUnsuspend = async (): Promise<void> => {
  await runCommand(() => store.unsuspend(serviceId.value), 'clients.clientServiceDetail.unsuspended')
}

/**
 * Terminates the service via the API and re-fetches the detail.
 *
 * @returns Promise that resolves when the action completes.
 */
const handleTerminate = async (): Promise<void> => {
  await runCommand(() => store.terminate(serviceId.value, ServiceChangeReason.Terminated), 'clients.clientServiceDetail.terminated')
}

/**
 * Runs the irreversible change the operator confirmed, then closes the prompt.
 *
 * @returns Promise that resolves when the change completes.
 */
const confirmDanger = async (): Promise<void> => {
  if (dangerConfirm.value === DangerAction.StatusChange) {
    await applyStatus()
  } else {
    await handleTerminate()
  }
  dangerConfirm.value = null
}

/** Closes the danger confirmation; ignored while the confirmed change runs. */
const dismissDanger = (): void => {
  if (!dangerLoading.value) {
    dangerConfirm.value = null
  }
}

/**
 * Opens another service of the same client from the switcher.
 *
 * @param val - The service id.
 * @returns Nothing.
 */
const switchService = (val: number): void => {
  router.push(clientItemPath(clientId.value, ClientSection.Services, val))
}

/**
 * Stores the server chosen in the dropdown; zero means "none".
 *
 * @param val - The chosen server id.
 * @returns Nothing.
 */
const setServer = (val: number): void => {
  serverId.value = val === 0 ? null : val
}

// Re-fetch service when navigating via the service switcher
watch(serviceId, () => {
  fetchService()
})

onMounted(() => {
  fetchService()
  fetchClientServices()
  serversStore.fetchServers()
  settingsStore.fetchProducts()
})
</script>

<template>
  <UiStack>
    <UiStack v-if="loading && !service" data-testid="client-service-loading">
      <UiSkeleton :lines="3" />
      <UiText :tone="UiTextTone.Secondary">{{ t('clients.clientServiceDetail.loadingService') }}</UiText>
    </UiStack>

    <UiAlert v-else-if="error && !service" variant="error" data-testid="client-service-error">
      {{ error }}
    </UiAlert>

    <template v-else-if="service">
      <UiStack>
        <UiStack direction="row" wrap :justify="UiJustify.Between" :align="UiAlignItems.Center">
          <UiStack direction="row" :align="UiAlignItems.Center">
            <UiLink
              :to="clientSectionPath(clientId, ClientSection.Services)"
              :variant="UiButtonVariant.Secondary"
              data-testid="client-service-back"
            >
              <UiIcon :markup="BACK_ICON" :size="UiSize.Sm" />
              {{ t('shared.back') }}
            </UiLink>
            <UiSelect
              data-testid="client-service-switcher"
              v-if="clientServices.length > 1"
              :model-value="Number(serviceId)"
              :options="serviceSwitcherOptions"
              :placeholder="t('shared.productService')"
              searchable
              @update:model-value="switchService"
            />
            <UiHeading :level="1" variant="card">
              {{ t('clients.clientServiceDetail.serviceIdProductname', { id: service.id, productName: service.productName }) }}
            </UiHeading>
          </UiStack>

          <UiStack direction="row" :align="UiAlignItems.Center" gap="sm">
            <UiStatusMessage :message="panelError" :tone="UiTone.Danger" data-testid="client-service-panel-error" />
            <UiButton
              type="button"
              :variant="UiButtonVariant.Secondary"
              :disabled="ssoLoading || !service.account.provisioningRef"
              data-testid="client-service-panel"
              @click="handleLoginToPanel"
            >
              <UiIcon :markup="PANEL_ICON" :size="UiSize.Sm" />
              {{ ssoLoading ? t('shared.loading') : t('clients.clientServiceDetail.loginToControlPanel') }}
            </UiButton>
            <UiButton
              type="button"
              :variant="UiButtonVariant.Secondary"
              data-testid="client-service-cancel"
              @click="handleCancel"
            >
              {{ t('shared.cancel') }}
            </UiButton>
          </UiStack>
        </UiStack>

        <UiGrid :columns="2">
          <UiStack>
            <UiCard padding="md">
              <UiForm
                stacked
                :aria-busy="saving === ServiceSection.HostingAccount"
                data-testid="client-service-hosting-form"
                @submit="saveHostingAccount"
              >
                <UiHeading :level="2" variant="card">{{ t('clients.clientServiceDetail.serviceInfo') }}</UiHeading>
                <UiGrid :columns="2" gap="sm">
                  <UiStack gap="xs">
                    <UiLabel :variant="UiLabelVariant.Field">{{ t('clients.clientServiceDetail.order') }}</UiLabel>
                    <UiText :tone="UiTextTone.Secondary">{{ service.id }}</UiText>
                  </UiStack>
                  <UiField :label="t('clients.clientServiceDetail.server')">
                    <UiSelect
                      data-testid="client-service-server"
                      :model-value="serverId ?? 0"
                      :options="serverOptions"
                      :placeholder="t('clients.clientServiceDetail.server')"
                      searchable
                      @update:model-value="setServer"
                    />
                  </UiField>
                  <UiField :label="t('shared.domain')">
                    <template #default="{ id }">
                      <UiInput
                        :id="id"
                        v-model="domain"
                        type="text"
                        :placeholder="t('shared.examples.domain')"
                        data-testid="client-service-domain"
                      />
                    </template>
                  </UiField>
                  <UiField :label="t('clients.clientServiceDetail.provisioningRef')">
                    <template #default="{ id }">
                      <UiInput
                        :id="id"
                        v-model="provisioningRef"
                        type="text"
                        :placeholder="t('shared.examples.hostingAccount')"
                        data-testid="client-service-provisioning-ref"
                      />
                    </template>
                  </UiField>
                  <UiField :label="t('clients.clientServiceDetail.dedicatedIp')">
                    <template #default="{ id }">
                      <UiInput
                        :id="id"
                        v-model="dedicatedIp"
                        type="text"
                        :placeholder="t('shared.examples.ipAddress')"
                        data-testid="client-service-dedicated-ip"
                      />
                    </template>
                  </UiField>
                  <UiField :label="t('shared.username')">
                    <template #default="{ id }">
                      <UiInput
                        :id="id"
                        v-model="username"
                        type="text"
                        :placeholder="t('shared.examples.hostingUser')"
                        data-testid="client-service-username"
                      />
                    </template>
                  </UiField>
                  <UiField :label="t('shared.password')">
                    <template #default="{ id }">
                      <UiInput
                        :id="id"
                        v-model="password"
                        type="password"
                        placeholder="********"
                        data-testid="client-service-password"
                      />
                    </template>
                  </UiField>
                </UiGrid>
                <UiButton
                  type="submit"
                  :variant="UiButtonVariant.Primary"
                  :aria-disabled="saving !== null"
                  data-testid="client-service-save-hosting"
                >
                  {{ saving === ServiceSection.HostingAccount ? t('shared.saving') : t(SAVE_LABEL_KEYS[ServiceSection.HostingAccount]) }}
                </UiButton>
                <UiStatusMessage
                  :message="outcomeOf(ServiceSection.HostingAccount)?.text ?? null"
                  :tone="outcomeTone(outcomeOf(ServiceSection.HostingAccount))"
                  data-testid="client-service-outcome-hosting-account"
                />
              </UiForm>
            </UiCard>

            <UiCard padding="md">
              <UiStack>
                <UiHeading :level="2" variant="card">{{ t('clients.clientServiceDetail.moduleCommands') }}</UiHeading>

                <UiForm
                  :aria-busy="saving === ServiceSection.Status"
                  data-testid="client-service-status-form"
                  @submit="saveStatus"
                >
                  <UiStack direction="row" wrap :align="UiAlignItems.End" gap="sm">
                    <UiField :label="t('shared.status')">
                      <UiSelect data-testid="client-service-status" v-model="status" :options="statusOptions" :placeholder="t('shared.status')" />
                    </UiField>
                    <UiButton
                      type="submit"
                      :variant="UiButtonVariant.Secondary"
                      :disabled="status === service.status"
                      :aria-disabled="saving !== null"
                      data-testid="client-service-status-save"
                    >
                      {{ saving === ServiceSection.Status ? t('shared.saving') : t(SAVE_LABEL_KEYS[ServiceSection.Status]) }}
                    </UiButton>
                  </UiStack>
                  <UiStatusMessage
                    :message="outcomeOf(ServiceSection.Status)?.text ?? null"
                    :tone="outcomeTone(outcomeOf(ServiceSection.Status))"
                    data-testid="client-service-outcome-status"
                  />
                </UiForm>

                <UiForm
                  v-if="service.status === ServiceStatus.Terminated"
                  :aria-busy="saving === ServiceSection.TerminationDate"
                  data-testid="client-service-termination-form"
                  @submit="saveTerminationDate"
                >
                  <UiStack>
                    <UiText :tone="UiTextTone.Muted">
                      {{ t('clients.clientServiceDetail.serviceTerminated') }}
                    </UiText>
                    <UiStack direction="row" wrap :align="UiAlignItems.End" gap="sm">
                      <UiField :label="t('clients.clientServiceDetail.terminationDate')">
                        <UiDatePicker v-model="terminatedAt" data-testid="client-service-termination-date" />
                      </UiField>
                      <UiButton
                        type="submit"
                        :variant="UiButtonVariant.Secondary"
                        :disabled="!terminatedAt"
                        :aria-disabled="saving !== null"
                        data-testid="client-service-save-termination-date"
                      >
                        {{ saving === ServiceSection.TerminationDate ? t('shared.saving') : t(SAVE_LABEL_KEYS[ServiceSection.TerminationDate]) }}
                      </UiButton>
                    </UiStack>
                    <UiStatusMessage
                      :message="outcomeOf(ServiceSection.TerminationDate)?.text ?? null"
                      :tone="outcomeTone(outcomeOf(ServiceSection.TerminationDate))"
                      data-testid="client-service-outcome-termination-date"
                    />
                  </UiStack>
                </UiForm>

                <UiStack v-else direction="row" wrap gap="sm">
                  <template v-if="service.status === ServiceStatus.Pending">
                    <UiButton
                      type="button"
                      :variant="UiButtonVariant.Secondary"
                      :disabled="commandLoading"
                      data-testid="client-service-create"
                      @click="handleProvision"
                    >
                      {{ commandLoading ? t('shared.processing') : t('clients.clientServiceDetail.create') }}
                    </UiButton>
                    <UiButton
                      type="button"
                      :variant="UiButtonVariant.Danger"
                      :disabled="commandLoading"
                      data-testid="client-service-terminate"
                      @click="dangerConfirm = DangerAction.Terminate"
                    >
                      {{ commandLoading ? t('shared.processing') : t('clients.clientServiceDetail.terminate') }}
                    </UiButton>
                  </template>

                  <template v-else-if="service.status === ServiceStatus.Active">
                    <UiButton
                      type="button"
                      :variant="UiButtonVariant.Secondary"
                      :disabled="commandLoading"
                      data-testid="client-service-suspend"
                      @click="showSuspendModal = true"
                    >
                      {{ commandLoading ? t('shared.processing') : t('clients.clientServiceDetail.suspend') }}
                    </UiButton>
                    <UiButton
                      type="button"
                      :variant="UiButtonVariant.Danger"
                      :disabled="commandLoading"
                      data-testid="client-service-terminate"
                      @click="dangerConfirm = DangerAction.Terminate"
                    >
                      {{ commandLoading ? t('shared.processing') : t('clients.clientServiceDetail.terminate') }}
                    </UiButton>
                    <UiButton
                      type="button"
                      :variant="UiButtonVariant.Secondary"
                      :disabled="commandLoading || !service.account.provisioningRef"
                      data-testid="client-service-change-package"
                      @click="showChangePackageModal = true"
                    >
                      {{ commandLoading ? t('shared.processing') : t('clients.clientServiceDetail.changePackage') }}
                    </UiButton>
                    <UiButton
                      type="button"
                      :variant="UiButtonVariant.Secondary"
                      :disabled="commandLoading || !service.account.provisioningRef"
                      data-testid="client-service-change-password"
                      @click="showChangePasswordModal = true"
                    >
                      {{ commandLoading ? t('shared.processing') : t('shared.changePassword') }}
                    </UiButton>
                  </template>

                  <template v-else-if="service.status === ServiceStatus.Suspended">
                    <UiButton
                      type="button"
                      :variant="UiButtonVariant.Secondary"
                      :disabled="commandLoading"
                      data-testid="client-service-unsuspend"
                      @click="handleUnsuspend"
                    >
                      {{ commandLoading ? t('shared.processing') : t('clients.clientServiceDetail.unsuspend') }}
                    </UiButton>
                    <UiButton
                      type="button"
                      :variant="UiButtonVariant.Danger"
                      :disabled="commandLoading"
                      data-testid="client-service-terminate"
                      @click="dangerConfirm = DangerAction.Terminate"
                    >
                      {{ commandLoading ? t('shared.processing') : t('clients.clientServiceDetail.terminate') }}
                    </UiButton>
                  </template>
                </UiStack>
                <UiStatusMessage
                  :message="commandOutcome?.text ?? null"
                  :tone="outcomeTone(commandOutcome)"
                  data-testid="client-service-command-outcome"
                />
              </UiStack>
            </UiCard>

            <UiCard padding="md">
              <UiStack>
                <UiHeading :level="2" variant="card">{{ t('clients.clientServiceDetail.addons') }}</UiHeading>
                <UiText :tone="UiTextTone.Muted">{{ t('clients.clientServiceDetail.noAddonsFound') }}</UiText>
              </UiStack>
            </UiCard>
          </UiStack>

          <UiStack>
            <UiCard padding="md">
              <UiForm
                stacked
                :aria-busy="saving === ServiceSection.Billing"
                data-testid="client-service-billing-form"
                @submit="saveBillingTerms"
              >
                <UiHeading :level="2" variant="card">{{ t('clients.clientServiceDetail.billing') }}</UiHeading>
                <UiField :label="t('shared.productService')">
                  <UiSelect
                    data-testid="client-service-product"
                    v-model="productIdRef"
                    :options="productOptions"
                    :placeholder="t('shared.productService')"
                    searchable
                  />
                </UiField>
                <UiField :label="t('clients.clientServiceDetail.quantity')">
                  <UiNumberInput data-testid="client-service-quantity" v-model="quantity" :min="1" placeholder="1" />
                </UiField>
                <UiGrid :columns="2" gap="sm">
                  <UiStack gap="xs">
                    <UiLabel :variant="UiLabelVariant.Field">{{ t('shared.registrationDate') }}</UiLabel>
                    <UiText :tone="UiTextTone.Secondary" data-testid="client-service-registered">{{ formatDate(service.createdAt) }}</UiText>
                  </UiStack>
                  <UiField :label="t('clients.clientServiceDetail.firstPaymentAmount')">
                    <UiNumberInput data-testid="client-service-first-payment" v-model="firstPaymentAmount" :step="0.01" :min="0" placeholder="0.00" />
                  </UiField>
                  <UiField :label="t('clients.clientServiceDetail.recurringAmount')">
                    <UiNumberInput data-testid="client-service-recurring" v-model="recurringAmount" :step="0.01" :min="0" placeholder="0.00" />
                  </UiField>
                  <UiField :label="t('shared.nextDueDate')">
                    <UiDatePicker v-model="nextDueDate" />
                  </UiField>
                  <UiField :label="t('shared.billingCycle')">
                    <UiSelect data-testid="client-service-billing-cycle" v-model="billingCycle" :options="billingCycleOptions" :placeholder="t('shared.billingCycle')" />
                  </UiField>
                  <UiField :label="t('shared.paymentMethod')">
                    <template #default="{ id }">
                      <UiInput
                        :id="id"
                        v-model="paymentMethod"
                        type="text"
                        :placeholder="t('clients.clientServiceDetail.stripePaypal')"
                        data-testid="client-service-payment-method"
                      />
                    </template>
                  </UiField>
                  <UiField :label="t('clients.clientServiceDetail.promotionCode')">
                    <template #default="{ id }">
                      <UiInput
                        :id="id"
                        v-model="promotionCode"
                        type="text"
                        :placeholder="t('shared.examples.promoCode')"
                        data-testid="client-service-promotion"
                      />
                    </template>
                  </UiField>
                </UiGrid>
                <UiField :label="t('clients.clientServiceDetail.subscriptionId')">
                  <template #default="{ id }">
                    <UiInput
                      :id="id"
                      v-model="subscriptionId"
                      type="text"
                      :placeholder="t('shared.examples.subscriptionId')"
                      data-testid="client-service-subscription"
                    />
                  </template>
                </UiField>
                <UiButton
                  type="submit"
                  :variant="UiButtonVariant.Primary"
                  :disabled="productIdRef === null"
                  :aria-disabled="saving !== null"
                  data-testid="client-service-save-billing"
                >
                  {{ saving === ServiceSection.Billing ? t('shared.saving') : t(SAVE_LABEL_KEYS[ServiceSection.Billing]) }}
                </UiButton>
                <UiStatusMessage
                  :message="outcomeOf(ServiceSection.Billing)?.text ?? null"
                  :tone="outcomeTone(outcomeOf(ServiceSection.Billing))"
                  data-testid="client-service-outcome-billing"
                />
              </UiForm>
            </UiCard>

            <UiCard padding="md">
              <UiForm
                stacked
                :aria-busy="saving === ServiceSection.Automation"
                data-testid="client-service-automation-form"
                @submit="saveAutomation"
              >
                <UiHeading :level="2" variant="card">{{ t('clients.clientServiceDetail.autoManagement') }}</UiHeading>
                <UiLabel :variant="UiLabelVariant.Inline">
                  <UiToggle v-model="autoRenew" data-testid="client-service-auto-renew" />
                  {{ t('clients.clientServiceDetail.autoRenew') }}
                </UiLabel>
                <UiLabel :variant="UiLabelVariant.Inline">
                  <UiToggle v-model="overrideAutoSuspend" data-testid="client-service-override-suspend" />
                  {{ t('clients.clientServiceDetail.overrideAutoSuspend') }}
                </UiLabel>
                <UiField v-if="overrideAutoSuspend" :label="t('clients.clientServiceDetail.suspendUntil')">
                  <UiDatePicker v-model="suspendUntil" />
                </UiField>
                <UiLabel :variant="UiLabelVariant.Inline">
                  <UiToggle v-model="autoTerminateEndOfCycle" data-testid="client-service-auto-terminate" />
                  {{ t('clients.clientServiceDetail.autoTerminateEndOfCycle') }}
                </UiLabel>
                <UiField v-if="autoTerminateEndOfCycle" :label="t('clients.clientServiceDetail.terminationReason')">
                  <template #default="{ id }">
                    <UiInput
                      :id="id"
                      v-model="autoTerminateReason"
                      type="text"
                      :placeholder="t('clients.clientServiceDetail.reasonForAutoTermination')"
                      data-testid="client-service-terminate-reason"
                    />
                  </template>
                </UiField>
                <UiButton
                  type="submit"
                  :variant="UiButtonVariant.Primary"
                  :aria-disabled="saving !== null"
                  data-testid="client-service-save-automation"
                >
                  {{ saving === ServiceSection.Automation ? t('shared.saving') : t(SAVE_LABEL_KEYS[ServiceSection.Automation]) }}
                </UiButton>
                <UiStatusMessage
                  :message="outcomeOf(ServiceSection.Automation)?.text ?? null"
                  :tone="outcomeTone(outcomeOf(ServiceSection.Automation))"
                  data-testid="client-service-outcome-automation"
                />
              </UiForm>
            </UiCard>
          </UiStack>
        </UiGrid>

        <UiCard padding="md">
          <UiForm
            stacked
            :aria-busy="saving === ServiceSection.Notes"
            data-testid="client-service-notes-form"
            @submit="saveNotes"
          >
            <UiHeading :level="2" variant="card">{{ t('shared.adminNotes') }}</UiHeading>
            <UiTextarea
              v-model="adminNotes"
              rows="6"
              :placeholder="t('shared.internalNotesVisibleOnlyTo')"
              data-testid="client-service-admin-notes"
            />
            <UiButton
              type="submit"
              :variant="UiButtonVariant.Primary"
              :aria-disabled="saving !== null"
              data-testid="client-service-save-notes"
            >
              {{ saving === ServiceSection.Notes ? t('shared.saving') : t(SAVE_LABEL_KEYS[ServiceSection.Notes]) }}
            </UiButton>
            <UiStatusMessage
              :message="outcomeOf(ServiceSection.Notes)?.text ?? null"
              :tone="outcomeTone(outcomeOf(ServiceSection.Notes))"
              data-testid="client-service-outcome-notes"
            />
          </UiForm>
        </UiCard>
      </UiStack>

      <PluginSlot :key="service.id" :slot-id="PluginSlotId.AdminServicePanels" :context="{ serviceId: service.id }" />

      <UiDialog
        v-if="showChangePasswordModal"
        :title="t('shared.changePassword')"
        @close="showChangePasswordModal = false"
      >
        <UiText :tone="UiTextTone.Secondary">{{ t('clients.clientServiceDetail.enterANewPasswordFor') }}</UiText>
        <UiField :label="t('clients.clientServiceDetail.newPassword')">
          <template #default="{ id }">
            <UiInput
              :id="id"
              v-model="newPassword"
              type="password"
              :placeholder="t('clients.clientServiceDetail.enterNewPassword')"
              data-testid="client-service-new-password"
            />
          </template>
        </UiField>
        <template #footer>
          <UiButton
            :variant="UiButtonVariant.Secondary"
            data-testid="client-service-password-cancel"
            @click="showChangePasswordModal = false"
          >
            {{ t('shared.cancel') }}
          </UiButton>
          <UiButton
            :variant="UiButtonVariant.Primary"
            :disabled="!newPassword.trim()"
            :loading="commandLoading"
            data-testid="client-service-password-confirm"
            @click="handleChangePassword"
          >
            {{ t('clients.clientServiceDetail.confirm') }}
          </UiButton>
        </template>
      </UiDialog>

      <UiDialog
        v-if="showChangePackageModal"
        :title="t('clients.clientServiceDetail.changePackage')"
        @close="showChangePackageModal = false"
      >
        <UiText :tone="UiTextTone.Secondary">{{ t('clients.clientServiceDetail.enterTheNewHostingPackage') }}</UiText>
        <UiField :label="t('clients.clientServiceDetail.packageName')">
          <template #default="{ id }">
            <UiInput
              :id="id"
              v-model="newPackage"
              type="text"
              :placeholder="t('clients.clientServiceDetail.eGStarterPlan')"
              data-testid="client-service-new-package"
            />
          </template>
        </UiField>
        <template #footer>
          <UiButton
            :variant="UiButtonVariant.Secondary"
            data-testid="client-service-package-cancel"
            @click="showChangePackageModal = false"
          >
            {{ t('shared.cancel') }}
          </UiButton>
          <UiButton
            :variant="UiButtonVariant.Primary"
            :disabled="!newPackage.trim()"
            :loading="commandLoading"
            data-testid="client-service-package-confirm"
            @click="handleChangePackage"
          >
            {{ t('clients.clientServiceDetail.confirm') }}
          </UiButton>
        </template>
      </UiDialog>

      <UiDialog
        v-if="showSuspendModal"
        variant="confirm"
        :title="t('clients.clientServiceDetail.suspendService')"
        :message="t('clients.clientServiceDetail.areYouSureYouWant')"
        :confirm-label="t('clients.clientServiceDetail.suspend')"
        :loading-label="t('shared.processing')"
        :loading="commandLoading"
        tone="warning"
        @confirm="handleSuspend"
        @close="showSuspendModal = false"
      />

      <UiDialog
        v-if="dangerConfirm !== null"
        :variant="UiDialogVariant.Confirm"
        :title="dangerTitle"
        :message="t('clients.clientServiceDetail.thisActionCannotBeUndone')"
        :confirm-label="dangerConfirmLabel"
        :loading-label="t('shared.processing')"
        :loading="dangerLoading"
        :tone="UiTone.Danger"
        @confirm="confirmDanger"
        @close="dismissDanger"
      />
    </template>
  </UiStack>
</template>
