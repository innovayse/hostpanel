<script setup lang="ts">
/**
 * Dialog that brings a domain a registrar account already holds into a client, with its real expiry, without an order, an
 * invoice or a charge.
 *
 * The staff member chooses a registrar (only an installed registrar plugin that lists its domains is offered) and the client
 * who will own the domain, and the registrar's domains appear one page at a time, each marked with whether the panel already
 * has it. A domain not in the panel and with an expiry gets an import button; one the registrar gives no expiry for cannot be
 * imported and says so. A registrar pages by cursor, so there is a Previous and a Next button and no page number to jump to.
 * The dialog stays open after an import so several domains can be brought in; the parent is told through `imported` so it
 * can reload its list. Reads and writes `stores/registrarDomains`.
 */
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiClientSelect from '@/components/ui/UiClientSelect.vue'
import UiDataTable from '@/components/ui/UiDataTable.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiField from '@/components/ui/UiField.vue'
import UiRowActions from '@/components/ui/UiRowActions.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import { useClientOptions } from '@/composables/useClientOptions'
import { useRegistrarModuleOptions } from '@/composables/useRegistrarModuleOptions'
import { formatDate } from '@/lib/format'
import { Icon } from '@/lib/icons'
import { useRegistrarDomainsStore } from '@/stores/registrarDomains'
import { PluginCapability } from '@/types/plugin'
import type { RegistrarDomain } from '@/types/registrardomain'
import { UiActionKind } from '@/utils/ui/actions'
import { UiButtonType, UiButtonVariant } from '@/utils/ui/button'
import { UiDialogSize } from '@/utils/ui/dialog'
import type { UiMenuAction } from '@/utils/ui/menu'
import { UiAlignItems, UiDirection, UiJustify } from '@/utils/ui/layout'
import { UiColumnSize } from '@/utils/ui/table'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'

const { t } = useI18n()

const emit = defineEmits<{
  /** Emitted after a domain was imported, so the page behind the dialog can reload its list. */
  imported: []
  /** Emitted when the dialog is closed. */
  close: []
}>()

/** Registrar domains store, the caller of the endpoints. */
const store = useRegistrarDomainsStore()

/** The installed registrar plugins that list their domains, and the call that fills them. */
const { options: registrarOptions, load: loadRegistrars } = useRegistrarModuleOptions(PluginCapability.RegistrarDomainList)

/** The clients the picker offers, and the call that fills them. */
const { clients, load: loadClients } = useClientOptions()

/** The chosen registrar's plugin id, empty while none is chosen. */
const registrarId = ref('')

/** The chosen client's id as the picker holds it, empty while none is chosen. */
const clientId = ref('')

/** The name of the domain whose import is in flight, null when none is. */
const importing = ref<string | null>(null)

/** Whether a client is chosen, which every import needs. */
const hasClient = computed<boolean>(() => clientId.value !== '')

/**
 * Whether a domain can be imported now: the panel does not have it, the registrar reports its expiry, a client is chosen and
 * no other import is running.
 *
 * @param domain - The listed domain.
 * @returns True when its import button is enabled.
 */
const canImport = (domain: RegistrarDomain): boolean =>
  !domain.inPanel && domain.expiresAt !== null && hasClient.value && importing.value === null

/**
 * Imports one domain into the chosen client; the row turns to "in the panel" on success and the parent is told.
 *
 * @param domain - The listed domain.
 * @returns Promise resolving once the request has finished.
 */
const handleImport = async (domain: RegistrarDomain): Promise<void> => {
  if (!canImport(domain)) {
    return
  }
  importing.value = domain.name
  const imported = await store.importDomain(registrarId.value, { clientId: Number(clientId.value), domainName: domain.name })
  importing.value = null
  if (imported) {
    emit('imported')
  }
}

/**
 * The row action of a domain: its one import button.
 *
 * @param domain - The listed domain.
 * @returns The action list passed to UiRowActions.
 */
const domainActions = (domain: RegistrarDomain): UiMenuAction[] => {
  /**
   * Imports this domain.
   * @returns Nothing.
   */
  const onImport = (): void => {
    void handleImport(domain)
  }
  return [
    {
      label: t('domains.registrarImport.import'),
      hint: hasClient.value ? undefined : t('domains.registrarImport.chooseClientFirst'),
      icon: Icon.Download,
      testid: `registrar-import-row-${domain.name}`,
      disabled: !canImport(domain),
      onSelect: onImport,
    },
  ]
}

/** Loads the first page of the registrar's list when one is chosen, and forgets the list when the choice is cleared. */
watch(registrarId, (chosen: string) => {
  if (chosen === '') {
    store.reset()
    return
  }
  void store.fetchFirst(chosen)
})

/** Shows the page before the one on screen. */
const handlePrevious = (): void => {
  void store.fetchPrevious(registrarId.value)
}

/** Shows the page after the one on screen. */
const handleNext = (): void => {
  void store.fetchNext(registrarId.value)
}

onMounted(() => {
  store.reset()
  void loadRegistrars()
  void loadClients()
})
</script>

<template>
  <UiDialog :title="t('domains.registrarImport.title')" :size="UiDialogSize.Xl" data-testid="registrar-import-dialog" @close="emit('close')">
    <UiStack>
      <UiText :tone="UiTextTone.Secondary">{{ t('domains.registrarImport.intro') }}</UiText>
      <UiAlert v-if="store.actionError" :variant="UiTone.Danger" data-testid="registrar-import-action-error">
        {{ store.actionError }}
      </UiAlert>
      <UiField :label="t('shared.registrar')">
        <UiSelect
          v-model="registrarId"
          :options="registrarOptions"
          :placeholder="registrarOptions.length === 0 ? t('domains.registrarImport.noRegistrars') : t('domains.registrarImport.chooseRegistrar')"
          :disabled="registrarOptions.length === 0"
          data-testid="registrar-import-registrar"
        />
      </UiField>
      <UiField :label="t('shared.client')" :error="store.fieldErrors.clientId">
        <UiClientSelect
          v-model="clientId"
          :clients="clients"
          :placeholder="t('shared.selectAClient')"
          data-testid="registrar-import-client"
        />
      </UiField>
      <UiAlert v-if="store.error" :variant="UiTone.Danger" data-testid="registrar-import-error">
        {{ store.error }}
      </UiAlert>
      <UiDataTable
        v-else-if="registrarId !== ''"
        :loading="store.loading && store.domains.length === 0"
        :empty="!store.loading && store.domains.length === 0"
        :empty-text="t('domains.registrarImport.empty')"
        data-testid="registrar-import-table"
      >
        <UiTableHead>
          <UiTableRow>
            <UiTableHeader :size="UiColumnSize.Grow">{{ t('shared.domainName') }}</UiTableHeader>
            <UiTableHeader>{{ t('domains.domainsList.expiresAt') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.status') }}</UiTableHeader>
            <UiTableHeader actions>{{ t('shared.actions') }}</UiTableHeader>
          </UiTableRow>
        </UiTableHead>
        <UiTableBody>
          <UiTableRow v-for="domain in store.domains" :key="domain.name" :data-testid="`registrar-import-domain-${domain.name}`">
            <UiTableCell>{{ domain.name }}</UiTableCell>
            <UiTableCell muted>{{ formatDate(domain.expiresAt) }}</UiTableCell>
            <UiTableCell>
              <UiBadge v-if="domain.inPanel" :tone="UiTone.Success" small>{{ t('domains.registrarImport.inPanel') }}</UiBadge>
              <UiBadge v-else-if="domain.expiresAt === null" :tone="UiTone.Warning" small>{{ t('domains.registrarImport.noExpiry') }}</UiBadge>
              <UiBadge v-else :tone="UiTone.Neutral" small>{{ t('domains.registrarImport.notInPanel') }}</UiBadge>
            </UiTableCell>
            <UiTableCell actions>
              <UiRowActions :actions="domainActions(domain)" :testid="`registrar-import-actions-${domain.name}`" />
            </UiTableCell>
          </UiTableRow>
        </UiTableBody>
      </UiDataTable>
      <UiStack v-if="registrarId !== '' && !store.error" :direction="UiDirection.Row" :justify="UiJustify.Between" :align="UiAlignItems.Center">
        <UiButton
          :icon="Icon.Back"
          :type="UiButtonType.Button"
          :variant="UiButtonVariant.Secondary"
          :disabled="!store.hasPrevious || store.loading"
          data-testid="registrar-import-previous"
          @click="handlePrevious"
        >
          {{ t('domains.registrarImport.previousPage') }}
        </UiButton>
        <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm" data-testid="registrar-import-page">
          {{ t('domains.registrarImport.page', { page: store.pageNumber }) }}
        </UiText>
        <UiButton
          :action="UiActionKind.Next"
          :type="UiButtonType.Button"
          :variant="UiButtonVariant.Secondary"
          :disabled="!store.hasNext || store.loading"
          data-testid="registrar-import-next"
          @click="handleNext"
        >
          {{ t('shared.next') }}
        </UiButton>
      </UiStack>
    </UiStack>
  </UiDialog>
</template>
