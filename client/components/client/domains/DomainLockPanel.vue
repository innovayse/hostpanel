<template>
  <UiStack data-testid="domain-lock">
    <UiAlert v-if="toggleError" variant="error" data-testid="domain-toggle-error">{{ toggleError }}</UiAlert>

    <UiCard>
      <UiCardHeader :title="$t('client.domains.registrarLock')" :icon="Lock" />
      <ClientDomainsDomainSwitchRow
        :enabled="lockEnabled"
        :on-label="$t('client.domains.lockOn')"
        :off-label="$t('client.domains.lockOff')"
        :description="$t('client.domains.lockDesc')"
        :toggle-label="$t('client.domains.registrarLock')"
        :saving="lockSaving"
        testid="domain-lock-row"
        @toggle="toggleLock"
      />
    </UiCard>

    <UiCard>
      <UiCardHeader :title="$t('client.domains.idProtection')" :icon="ShieldCheck" />
      <ClientDomainsDomainSwitchRow
        :enabled="idProtectEnabled"
        :on-label="$t('client.domains.idProtectOn')"
        :off-label="$t('client.domains.idProtectOff')"
        :description="$t('client.domains.idProtectionDesc')"
        :toggle-label="$t('client.domains.idProtection')"
        :saving="idProtectSaving"
        testid="domain-idprotect-row"
        @toggle="toggleIdProtect"
      />
    </UiCard>

    <ClientDomainsDomainEppCard :domain-id="domainId" :lock-enabled="lockEnabled" />
  </UiStack>
</template>

<script setup lang="ts">
/**
 * DomainLockPanel: the registrar lock, the ID protection (hides the owner's data from public
 * lookup) and the EPP code request of a domain. Each change goes through the domains store and
 * the switch only moves when the API accepted it.
 */
import { Lock, ShieldCheck } from 'lucide-vue-next'
import { storeToRefs } from 'pinia'
import { useDomainsStore } from '~/stores/domains'
import { DomainActionOutcome } from '~/types/domainactionoutcome'

/** Whether the registrar lock is on (v-model); the parent keeps it across panel switches. */
const lockEnabled = defineModel<boolean>('lockEnabled', { required: true })

/** Whether ID protection is on (v-model); the parent keeps it across panel switches. */
const idProtectEnabled = defineModel<boolean>('idProtectEnabled', { required: true })

/** Props of DomainLockPanel. */
interface Props {
  /** The id of the domain. */
  domainId: string
}

/** The props of the component. */
const props = defineProps<Props>()

/** The domains store: the in-flight flags and the refusal sentence of toggles. */
const store = useDomainsStore()
/** Reactive state of the store this component renders. */
const { lockSaving, idProtectSaving, toggleError } = storeToRefs(store)

/**
 * Asks the API to switch the registrar lock; the switch keeps its state when the API refuses.
 *
 * @param value - The state the reader asked for.
 * @returns Resolves when the attempt has ended.
 */
const toggleLock = async (value: boolean): Promise<void> => {
  if (await store.setLock(props.domainId, value) === DomainActionOutcome.Done) {
    lockEnabled.value = value
  }
}

/**
 * Asks the API to switch ID protection; the switch keeps its state when the API refuses.
 *
 * @param value - The state the reader asked for.
 * @returns Resolves when the attempt has ended.
 */
const toggleIdProtect = async (value: boolean): Promise<void> => {
  if (await store.setIdProtect(props.domainId, value) === DomainActionOutcome.Done) {
    idProtectEnabled.value = value
  }
}
</script>
