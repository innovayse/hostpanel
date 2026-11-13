<template>
  <UiCard data-testid="domain-autorenew">
    <UiCardHeader :title="$t('client.domains.tabAutoRenew')" :icon="RefreshCw" />
    <UiAlert v-if="toggleError" variant="error" class="mb-4" data-testid="domain-toggle-error">{{ toggleError }}</UiAlert>
    <ClientDomainsDomainSwitchRow
      :enabled="enabled"
      :on-label="$t('client.domains.autoRenewOn')"
      :off-label="$t('client.domains.autoRenewOff')"
      :description="$t('client.domains.autoRenewDesc')"
      :toggle-label="$t('client.domains.tabAutoRenew')"
      :saving="autoRenewSaving"
      testid="domain-autorenew-row"
      @toggle="toggle"
    />
  </UiCard>
</template>

<script setup lang="ts">
/**
 * DomainAutoRenewPanel: the auto-renew switch of a domain. The change goes through the domains
 * store; the switch only moves when the API accepted it.
 */
import { RefreshCw } from 'lucide-vue-next'
import { storeToRefs } from 'pinia'
import { useDomainsStore } from '~/stores/domains'
import { DomainActionOutcome } from '~/types/domainactionoutcome'

/** Whether auto-renew is on (v-model); the parent keeps it across panel switches. */
const enabled = defineModel<boolean>('enabled', { required: true })

/** Props of DomainAutoRenewPanel. */
interface Props {
  /** The id of the domain. */
  domainId: string
}

/** The props of the component. */
const props = defineProps<Props>()

/** The domains store: the in-flight flag and the refusal sentence of toggles. */
const store = useDomainsStore()
/** Reactive state of the store this component renders. */
const { autoRenewSaving, toggleError } = storeToRefs(store)

/**
 * Asks the API to switch auto-renew; the switch keeps its state when the API refuses.
 *
 * @param value - The state the reader asked for.
 * @returns Resolves when the attempt has ended.
 */
const toggle = async (value: boolean): Promise<void> => {
  if (await store.setAutoRenew(props.domainId, value) === DomainActionOutcome.Done) {
    enabled.value = value
  }
}
</script>
