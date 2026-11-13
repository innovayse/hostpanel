<script setup lang="ts">
/**
 * Modal that lets staff pick which data points of a client to export and downloads them as JSON.
 */
import { ref, computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useClientsStore } from '@/stores/clients'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCheckbox from '@/components/ui/UiCheckbox.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiLabel from '@/components/ui/UiLabel.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiLabelVariant, UiTextSize, UiTextTone } from '@/utils/ui/text'
import { MimeType } from '@/types/http'

const { t } = useI18n()

/** Props for ClientExportModal. */
const props = defineProps<{
  /** Client being exported. */
  clientId: number
  /** Display name shown under the title. */
  clientName: string
}>()

const emit = defineEmits<{
  /** Emitted when the modal is closed or the download finished. */
  close: []
}>()

/** Clients store, owner of the export call and its error. */
const store = useClientsStore()

/** True while the export is being generated. */
const downloading = ref(false)

/** One exportable data point. */
interface ExportField {
  /** Key sent to the API. */
  key: string
  /** Translated label. */
  label: string
}

/** Every data point that can be exported. */
const FIELDS: ExportField[] = [
  { key: 'profileData', label: t('clients.clientExport.profileData') },
  { key: 'payMethods', label: t('clients.clientExport.payMethods') },
  { key: 'contacts', label: t('clients.clientSummary.contacts') },
  { key: 'users', label: t('clients.clientExport.users') },
  { key: 'invitations', label: t('clients.clientExport.invitations') },
  { key: 'productsServices', label: t('clients.clientSummary.productsServices2') },
  { key: 'domains', label: t('shared.domains') },
  { key: 'billableItems', label: t('nav.billingChildren.billableItems') },
  { key: 'invoices', label: t('shared.invoices') },
  { key: 'quotes', label: t('shared.quotes') },
  { key: 'transactions', label: t('shared.transactions') },
  { key: 'tickets', label: t('clients.clientTickets.tickets') },
  { key: 'emails', label: t('clients.clientEmails.emails') },
  { key: 'notes', label: t('shared.notes') },
  { key: 'consentHistory', label: t('clients.clientExport.consentHistory') },
  { key: 'activityLog', label: t('clients.clientLog.activityLog') },
]

/** Checked state per field key; everything starts selected. */
const selected = ref<Record<string, boolean>>(
  Object.fromEntries(FIELDS.map(f => [f.key, true])),
)

/** True when every field is checked. */
const allSelected = computed<boolean>(() => FIELDS.every(f => selected.value[f.key]))

/** True when at least one field is checked. */
const anySelected = computed<boolean>(() => FIELDS.some(f => selected.value[f.key]))

/**
 * Checks or unchecks one field.
 *
 * @param key - The field key.
 * @param value - The new checked state.
 */
const setField = (key: string, value: boolean): void => {
  selected.value[key] = value
}

/**
 * Checks or unchecks every field.
 *
 * @returns Nothing.
 */
const toggleAll = (): void => {
  const val = !allSelected.value
  FIELDS.forEach(f => {
    selected.value[f.key] = val
  })
}

/**
 * Requests the export and saves the result as a JSON file.
 *
 * @returns Resolves once the file was handed to the browser or the store reported an error.
 */
const download = async (): Promise<void> => {
  downloading.value = true
  const fields = FIELDS.filter(f => selected.value[f.key]).map(f => f.key)
  const data = await store.exportClient(props.clientId, fields)
  downloading.value = false
  if (data === null) {
    return
  }
  const blob = new Blob([JSON.stringify(data, null, 2)], { type: MimeType.Json })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `client-${props.clientId}-export.json`
  a.click()
  URL.revokeObjectURL(url)
  emit('close')
}
</script>

<template>
  <UiDialog :title="t('clients.clientExport.exportData')" size="md" @close="emit('close')">
    <UiStack>
      <UiAlert v-if="store.exportError" variant="error" data-testid="clients-export-error">
        {{ store.exportError }}
      </UiAlert>
      <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Md">{{ clientName }}</UiText>
      <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">
        {{ t('clients.clientExport.chooseWhichDataPointsTo') }}
      </UiText>
      <UiLabel :variant="UiLabelVariant.Inline">
        <UiCheckbox
          :model-value="allSelected"
          :aria-label="t('shared.selectAll')"
          data-testid="clients-export-select-all"
          @update:model-value="toggleAll"
        />
        {{ t('shared.selectAll') }}
      </UiLabel>
      <UiGrid :columns="2" gap="sm">
        <UiLabel v-for="field in FIELDS" :key="field.key" :variant="UiLabelVariant.Inline">
          <UiCheckbox
            :model-value="selected[field.key] === true"
            :aria-label="field.label"
            :data-testid="`clients-export-field-${field.key}`"
            @update:model-value="setField(field.key, $event)"
          />
          {{ field.label }}
        </UiLabel>
      </UiGrid>
      <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Xs">
        {{ t('clients.clientExport.generatingAnExportForA') }}
      </UiText>
      <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Xs" data-testid="clients-export-storage-note">
        {{ t('clients.clientExport.storageNote') }}
      </UiText>
    </UiStack>
    <template #footer>
      <UiButton
        :variant="UiButtonVariant.Primary"
        :loading="downloading"
        :disabled="!anySelected"
        data-testid="clients-export-download"
        @click="download"
      >
        {{ t('shared.download') }}
      </UiButton>
    </template>
  </UiDialog>
</template>
