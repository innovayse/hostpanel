<template>
  <UiCard data-testid="domain-nameservers">
    <UiCardHeader :title="$t('client.domains.nameservers')" :icon="Network" />

    <UiForm data-testid="domain-nameservers-form" :error="nsError" :success="success" spacing="sm" @submit="save">
      <UiAlert variant="info" :icon-size="16">{{ $t('client.domains.nsInfoNote') }}</UiAlert>

      <UiStack gap="sm">
        <UiRadio
          name="ns-type"
          :value="false"
          :model-value="useCustom"
          :label="$t('client.domains.nsUseDefault')"
          testid="domain-ns-default"
          @update:model-value="useCustom = false"
        />
        <UiRadio
          name="ns-type"
          :value="true"
          :model-value="useCustom"
          :label="$t('client.domains.nsUseCustom')"
          testid="domain-ns-custom"
          @update:model-value="useCustom = true"
        />
      </UiStack>

      <UiStack v-for="(_, index) in servers" :key="index" direction="row" align="center">
        <UiText as="span" size="sm" tone="muted" class="w-24 flex-shrink-0">{{ $t('client.domains.nameserverN', { n: index + 1 }) }}</UiText>
        <UiInput
          v-model="servers[index]"
          :placeholder="$t('client.domains.nameserverPlaceholder', { n: index + 1 })"
          :disabled="!useCustom"
          size="sm"
          class="flex-1"
          :testid="`domain-ns-${index + 1}`"
        />
      </UiStack>

      <template #actions>
        <UiButton type="submit" size="sm" :loading="nsSaving" full-width data-testid="domain-ns-save">
          <Save v-if="!nsSaving" :size="14" :stroke-width="2" class="mr-1.5" />
          {{ $t('client.domains.saveNameservers') }}
        </UiButton>
      </template>
    </UiForm>
  </UiCard>
</template>

<script setup lang="ts">
/**
 * DomainNameserversPanel. The nameservers a domain points at: use the registrar's defaults or
 * enter up to five custom ones. The current servers come from the domain the page already
 * loaded into the domains store (its `management.nameservers`); saving goes through the store,
 * and its refusal sentence is shown above the form.
 */
import { ref } from 'vue'
import { Network, Save } from 'lucide-vue-next'
import { storeToRefs } from 'pinia'
import { useDomainsStore } from '~/stores/domains'
import { DomainActionOutcome } from '~/types/domainactionoutcome'

/** How many nameserver fields the form offers. */
const NAMESERVER_COUNT = 5

/** Translates a key into the active locale. */
const { t } = useI18n()

/** Props of DomainNameserversPanel. */
interface Props {
  /** The id of the domain. */
  domainId: string
}

/** The props of the component. */
const props = defineProps<Props>()

/** The domains store: the in-flight flags and the refusal sentence. */
const store = useDomainsStore()
/** Reactive state of the store this component renders. */
const { nsSaving, nsError } = storeToRefs(store)

/** The confirmation shown after a successful save. */
const success = ref('')

/** The nameserver fields, one string each, filled from the domain's current nameservers. */
const servers = ref<string[]>(
  Array.from({ length: NAMESERVER_COUNT }, (_, index) => store.domain?.management.nameservers[index]?.host ?? '')
)

/** Whether the custom nameservers are used (false means the registrar's defaults). */
const useCustom = ref(Boolean(servers.value[0] || servers.value[1]))

/**
 * Saves the nameserver configuration through the store: the non-empty fields, in order.
 *
 * @returns Resolves when the attempt has ended.
 */
const save = async (): Promise<void> => {
  success.value = ''
  const nameservers = servers.value.filter((nameserver) => nameserver.trim() !== '')
  if (await store.saveNameservers(props.domainId, nameservers) === DomainActionOutcome.Done) {
    success.value = t('client.domains.nsSuccess')
  }
}
</script>
