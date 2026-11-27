<script setup lang="ts">
/**
 * Modal for adding a service to a client for a hosting account that already exists on a server:
 * restored after a reinstall, moved from another server, or set up by hand. The service starts
 * Active; nothing is created on the panel and no welcome mail is sent. Opened from a client's own tab it
 * knows the client; opened from the list of all services (no `clientId`) it asks which client the service belongs to.
 */
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { BILLING_CYCLE_OPTIONS } from '@/lib/constants'
import { useServersStore } from '@/stores/servers'
import { useClientOptions } from '@/composables/useClientOptions'
import { useServicesStore } from '@/stores/services'
import { useSettingsStore } from '@/stores/settings'
import { AddExistingRefusal, BillingCycle } from '@/types/service'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiClientSelect from '@/components/ui/UiClientSelect.vue'
import UiCheckbox from '@/components/ui/UiCheckbox.vue'
import UiDatePicker from '@/components/ui/UiDatePicker.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiField from '@/components/ui/UiField.vue'
import UiForm from '@/components/ui/UiForm.vue'
import UiFormActions from '@/components/ui/UiFormActions.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiLabel from '@/components/ui/UiLabel.vue'
import UiNumberInput from '@/components/ui/UiNumberInput.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiText from '@/components/ui/UiText.vue'
import { UiButtonType, UiButtonVariant } from '@/utils/ui/button'
import { UiDatePickerDirection } from '@/utils/ui/datePicker'
import { UiDialogSize } from '@/utils/ui/dialog'
import { UiInputType } from '@/utils/ui/input'
import { UiLabelVariant, UiTextTone } from '@/utils/ui/text'
import { UiActionKind } from '@/utils/ui/actions'
import { UiTone } from '@/utils/ui/tone'

const { t } = useI18n()

/** Props for AddExistingServiceModal. */
const props = defineProps<{
  /** The client the service is added to; absent when the modal is opened from the list of all services, which then asks. */
  clientId?: number
}>()

const emit = defineEmits<{
  /** Emitted once the service was added. */
  added: []
  /** Emitted when the modal is closed without adding. */
  close: []
}>()

/** Services store, the caller of the endpoint. */
const store = useServicesStore()

/** Servers store, for the server choice. */
const serversStore = useServersStore()

/** Settings store, for the product choice. */
const settingsStore = useSettingsStore()

/** The clients the picker offers, and the call that loads them; only used while the modal is not bound to a client. */
const { clients, load: loadClients } = useClientOptions()

/** The client chosen in the picker, empty until one is chosen. */
const chosenClient = ref<string | number | null>(null)

/** The client the service goes to: the one the modal was opened for, else the one chosen; null while none is. */
const ownerId = computed<number | null>(() => {
  if (props.clientId !== undefined) {
    return props.clientId
  }
  return chosenClient.value === null || chosenClient.value === '' ? null : Number(chosenClient.value)
})

/** Chosen product, or null. */
const productId = ref<number | null>(null)

/** Chosen billing cycle. */
const billingCycle = ref<BillingCycle>(BillingCycle.Annual)

/** Amount each renewal invoice charges. */
const recurringAmount = ref<number | null>(0)

/** Chosen server, or null. */
const serverId = ref<number | null>(null)

/** The account's primary domain. */
const domain = ref('')

/** The account's username on the panel. */
const username = ref('')

/** When the current billing period began (ISO day), or null for today. */
const startedAt = ref<string | null>(null)

/** True while the request is in flight. */
const saving = ref(false)

/** True once the API said the server's panel cannot confirm the account, so the confirmation is offered. */
const askUnverified = ref(false)

/** True once the API said another live service holds the domain, so the confirmation is offered. */
const askSharedDomain = ref(false)

/** The admin's confirmation of an account the server's panel cannot be asked about. */
const confirmUnverified = ref(false)

/** The admin's confirmation of a domain another live service already holds. */
const confirmSharedDomain = ref(false)

/** Product options for the select. */
const productOptions = computed(() => settingsStore.products.map(p => ({ value: p.id, label: p.name })))

/** Server options for the select. */
const serverOptions = computed(() =>
  serversStore.serverOptions.map(s => ({ value: s.id, label: `${s.name} (${s.hostname})` })),
)

/** True when every required field has a value. */
const complete = computed(() =>
  ownerId.value !== null && productId.value !== null && serverId.value !== null && domain.value.trim() !== '' && username.value.trim() !== '',
)

/**
 * Sends the service to the API and closes on success; a refusal stays on screen.
 *
 * @returns Promise resolving once the request has finished.
 */
const handleSave = async (): Promise<void> => {
  if (!complete.value || saving.value) {
    return
  }
  saving.value = true
  const ok = await store.addExisting({
    clientId: ownerId.value as number, // `complete` guarantees a client
    productId: productId.value as number,
    billingCycle: billingCycle.value,
    recurringAmount: recurringAmount.value ?? 0,
    serverId: serverId.value as number,
    domain: domain.value.trim(),
    username: username.value.trim(),
    startedAt: startedAt.value,
    confirmUnverified: confirmUnverified.value,
    confirmSharedDomain: confirmSharedDomain.value,
  })
  saving.value = false
  if (ok) {
    emit('added')
    return
  }
  // A refusal the admin can confirm past keeps its sentence on screen and offers the checkbox for it.
  askUnverified.value ||= store.addExistingCode === AddExistingRefusal.AccountUnverified
  askSharedDomain.value ||= store.addExistingCode === AddExistingRefusal.DomainShared
}

// A confirmation vouches for one server, username and domain: changing any of them takes it back.
watch([serverId, username, domain], () => {
  askUnverified.value = false
  askSharedDomain.value = false
  confirmUnverified.value = false
  confirmSharedDomain.value = false
})

onMounted(() => {
  store.actionError = null
  serversStore.fetchServerOptions()
  settingsStore.fetchProducts()
  if (props.clientId === undefined) {
    void loadClients()
  }
})
</script>

<template>
  <UiDialog :title="t('clients.addExistingService.title')" :size="UiDialogSize.Lg" @close="emit('close')">
    <UiForm stacked @submit="handleSave">
      <UiText :tone="UiTextTone.Secondary">{{ t('clients.addExistingService.intro') }}</UiText>
      <UiAlert v-if="store.actionError" :variant="UiTone.Danger" data-testid="add-existing-service-error">
        {{ store.actionError }}
      </UiAlert>
      <UiGrid :columns="2" container>
        <UiField v-if="clientId === undefined" :label="t('shared.client')" required :error="store.fieldErrors.clientId">
          <UiClientSelect
            v-model="chosenClient"
            :clients="clients"
            :placeholder="t('shared.selectAClient')"
            data-testid="add-existing-service-client"
          />
        </UiField>
        <UiField :label="t('shared.productService')" required :error="store.fieldErrors.productId">
          <UiSelect
            v-model="productId"
            :options="productOptions"
            :placeholder="t('shared.productService')"
            searchable
            data-testid="add-existing-service-product"
          />
        </UiField>
        <UiField :label="t('clients.clientServiceDetail.server')" required :error="store.fieldErrors.serverId">
          <UiSelect
            v-model="serverId"
            :options="serverOptions"
            :placeholder="t('clients.clientServiceDetail.server')"
            data-testid="add-existing-service-server"
          />
        </UiField>
        <UiField :label="t('shared.domain')" required :error="store.fieldErrors.domain">
          <template #default="{ id }">
            <UiInput :id="id" v-model="domain" :type="UiInputType.Text" placeholder="example.com" data-testid="add-existing-service-domain" />
          </template>
        </UiField>
        <UiField :label="t('clients.addExistingService.username')" required :error="store.fieldErrors.username">
          <template #default="{ id }">
            <UiInput :id="id" v-model="username" :type="UiInputType.Text" :placeholder="t('clients.addExistingService.username')" data-testid="add-existing-service-username" />
          </template>
        </UiField>
        <UiField :label="t('shared.billingCycle')">
          <UiSelect v-model="billingCycle" :options="BILLING_CYCLE_OPTIONS" :placeholder="t('shared.billingCycle')" data-testid="add-existing-service-cycle" />
        </UiField>
        <UiField :label="t('clients.addExistingService.recurringAmount')" :error="store.fieldErrors.recurringAmount">
          <UiNumberInput v-model="recurringAmount" :step="0.01" :min="0" placeholder="0.00" data-testid="add-existing-service-amount" />
        </UiField>
        <UiField :label="t('clients.addExistingService.startedAt')">
          <UiDatePicker
            v-model="startedAt"
            :direction="UiDatePickerDirection.Past"
            :placeholder="t('clients.addExistingService.today')"
            data-testid="add-existing-service-started-at"
          />
        </UiField>
      </UiGrid>
      <UiLabel v-if="askUnverified" :variant="UiLabelVariant.Inline">
        <UiCheckbox
          v-model="confirmUnverified"
          :aria-label="t('clients.addExistingService.confirmUnverified')"
          data-testid="add-existing-service-confirm-unverified"
        />
        {{ t('clients.addExistingService.confirmUnverified') }}
      </UiLabel>
      <UiLabel v-if="askSharedDomain" :variant="UiLabelVariant.Inline">
        <UiCheckbox
          v-model="confirmSharedDomain"
          :aria-label="t('clients.addExistingService.confirmSharedDomain')"
          data-testid="add-existing-service-confirm-shared-domain"
        />
        {{ t('clients.addExistingService.confirmSharedDomain') }}
      </UiLabel>
      <UiFormActions>
        <UiButton :action="UiActionKind.Cancel" :type="UiButtonType.Button" :variant="UiButtonVariant.Secondary" data-testid="add-existing-service-cancel" @click="emit('close')">{{ t('shared.cancel') }}</UiButton>
        <UiButton :action="UiActionKind.Add"
          :type="UiButtonType.Submit"
          :variant="UiButtonVariant.Primary"
          :loading="saving"
          :disabled="!complete"
          data-testid="add-existing-service-save"
        >
          {{ t('shared.add') }}
        </UiButton>
      </UiFormActions>
    </UiForm>
  </UiDialog>
</template>
