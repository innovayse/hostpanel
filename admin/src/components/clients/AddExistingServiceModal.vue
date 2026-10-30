<script setup lang="ts">
/**
 * Modal for adding a service to a client for a hosting account that already exists on a server:
 * restored after a reinstall, moved from another server, or set up by hand. The service starts
 * Active; nothing is created on the panel and no welcome mail is sent.
 */
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { BILLING_CYCLE_OPTIONS } from '@/lib/constants'
import { useServersStore } from '@/stores/servers'
import { useServicesStore } from '@/stores/services'
import { useSettingsStore } from '@/stores/settings'
import { BillingCycle } from '@/types/service'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiDatePicker from '@/components/ui/UiDatePicker.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiField from '@/components/ui/UiField.vue'
import UiForm from '@/components/ui/UiForm.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiNumberInput from '@/components/ui/UiNumberInput.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiDatePickerDirection } from '@/utils/ui/datePicker'
import { UiJustify } from '@/utils/ui/layout'
import { UiTextTone } from '@/utils/ui/text'

const { t } = useI18n()

/** Props for AddExistingServiceModal. */
const props = defineProps<{
  /** The client the service is added to. */
  clientId: number
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

/** Chosen product, or null. */
const productId = ref<number | null>(null)

/** Chosen billing cycle. */
const billingCycle = ref<string>(BillingCycle.Annual)

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

/** Product options for the select. */
const productOptions = computed(() => settingsStore.products.map(p => ({ value: p.id, label: p.name })))

/** Server options for the select. */
const serverOptions = computed(() =>
  serversStore.servers.map(s => ({ value: s.id, label: `${s.name} — ${s.endpoint.hostname}` })),
)

/** True when every required field has a value. */
const complete = computed(() =>
  productId.value !== null && serverId.value !== null && domain.value.trim() !== '' && username.value.trim() !== '',
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
    clientId: props.clientId,
    productId: productId.value as number,
    billingCycle: billingCycle.value,
    recurringAmount: recurringAmount.value ?? 0,
    serverId: serverId.value as number,
    domain: domain.value.trim(),
    username: username.value.trim(),
    startedAt: startedAt.value,
  })
  saving.value = false
  if (ok) {
    emit('added')
  }
}

onMounted(() => {
  store.actionError = null
  serversStore.fetchServers()
  settingsStore.fetchProducts()
})
</script>

<template>
  <UiDialog :title="t('clients.addExistingService.title')" size="lg" @close="emit('close')">
    <UiForm stacked @submit="handleSave">
      <UiText :tone="UiTextTone.Secondary">{{ t('clients.addExistingService.intro') }}</UiText>
      <UiAlert v-if="store.actionError" variant="error" data-testid="add-existing-service-error">
        {{ store.actionError }}
      </UiAlert>
      <UiGrid :columns="2">
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
            <UiInput :id="id" v-model="domain" type="text" placeholder="example.com" data-testid="add-existing-service-domain" />
          </template>
        </UiField>
        <UiField :label="t('clients.addExistingService.username')" required :error="store.fieldErrors.username">
          <template #default="{ id }">
            <UiInput :id="id" v-model="username" type="text" :placeholder="t('clients.addExistingService.username')" data-testid="add-existing-service-username" />
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
      <UiStack direction="row" :justify="UiJustify.End">
        <UiButton type="button" :variant="UiButtonVariant.Secondary" data-testid="add-existing-service-cancel" @click="emit('close')">{{ t('shared.cancel') }}</UiButton>
        <UiButton
          type="submit"
          :variant="UiButtonVariant.Primary"
          :disabled="!complete || saving"
          data-testid="add-existing-service-save"
        >
          {{ saving ? t('shared.saving') : t('clients.addExistingService.add') }}
        </UiButton>
      </UiStack>
    </UiForm>
  </UiDialog>
</template>
