<template>
  <UiCard data-testid="domain-epp">
    <UiCardHeader :title="$t('client.domains.eppCode')" :icon="Key" />
    <UiText size="xs" tone="muted" class="mb-3">{{ $t('client.domains.eppDesc') }}</UiText>
    <UiAlert v-if="lockEnabled" :icon-size="14" class="mb-3" data-testid="domain-epp-lock-warning">
      {{ $t('client.domains.lockWarning') }}
    </UiAlert>
    <UiAlert v-if="eppMessage" :variant="eppError ? 'error' : 'success'" class="mb-3" data-testid="domain-epp-message">
      {{ eppMessage }}
    </UiAlert>
    <UiButton size="sm" variant="subtle" :loading="eppSending" full-width data-testid="domain-epp-request" @click="requestEpp">
      <Mail v-if="!eppSending" :size="14" :stroke-width="2" class="mr-1.5" />
      {{ $t('client.domains.emailEpp') }}
    </UiButton>
  </UiCard>
</template>

<script setup lang="ts">
/**
 * DomainEppCard: asks for the domain's EPP (transfer authorisation) code, which the registrar
 * e-mails to the account. Shows a warning while the registrar lock is on, because a locked
 * domain cannot be transferred.
 */
import { computed } from 'vue'
import { Key, Mail } from 'lucide-vue-next'
import { storeToRefs } from 'pinia'
import { useDomainsStore } from '~/stores/domains'

/** Props of DomainEppCard. */
interface Props {
  /** The id of the domain. */
  domainId: string
  /** Whether the registrar lock is on. */
  lockEnabled: boolean
}

/** The props of the component. */
const props = defineProps<Props>()

/** The domains store: the in-flight flag, the code and the refusal sentence. */
const store = useDomainsStore()
/** Reactive state of the store this component renders. */
const { eppSending } = storeToRefs(store)

/** The code on success, else the refusal sentence. */
const eppMessage = computed(() => store.eppCode || store.eppError)

/** Whether {@link eppMessage} is a refusal rather than the code. */
const eppError = computed(() => store.eppError !== '')

/**
 * Requests the EPP code through the store.
 *
 * @returns Resolves when the attempt has ended.
 */
const requestEpp = async (): Promise<void> => {
  await store.requestEpp(props.domainId)
}
</script>
