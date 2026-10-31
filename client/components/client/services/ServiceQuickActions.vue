<template>
  <UiCard padding="md" data-testid="service-quick-actions">
    <UiHeading :level="3" size="subsection" class="!text-sm mb-3">{{ $t('client.services.whatToday') }}</UiHeading>
    <UiStack gap="xs" align="start">
      <UiButton
        v-if="hasServer && isActive && store.mayDo(Permission.AllowSingleSignOn)"
        variant="ghost"
        size="sm"
        :loading="ssoLoading"
        data-testid="service-quick-cpanel"
        @click="emit('cpanel')"
      >
        <ChevronRight :size="14" :stroke-width="2" class="mr-1.5" />
        {{ $t('client.services.quickCpanel') }}
      </UiButton>
      <UiButton
        v-if="store.mayDo(Permission.ViewPayInvoices)"
        variant="ghost"
        size="sm"
        data-testid="service-quick-invoices"
        @click="emit('invoices')"
      >
        <ChevronRight :size="14" :stroke-width="2" class="mr-1.5" />
        {{ $t('client.services.quickViewInvoices') }}
      </UiButton>
      <UiButton
        v-if="store.mayDo(Permission.PlaceNewOrders)"
        variant="ghost"
        size="sm"
        data-testid="service-quick-cancel"
        @click="emit('cancel')"
      >
        <ChevronRight :size="14" :stroke-width="2" class="mr-1.5" />
        {{ $t('client.services.quickCancel') }}
      </UiButton>
    </UiStack>
  </UiCard>
</template>

<script setup lang="ts">
/**
 * ServiceQuickActions - the "what would you like to do today" shortcuts of the service overview:
 * sign in to the control panel, reload the related invoices, start a cancellation.
 */
import { ChevronRight } from 'lucide-vue-next'
import { Permission } from '~/composables/usePermissions'
import { useClientStore } from '~/stores/client'

/** The client store, which knows what the signed-in person may do. */
const store = useClientStore()

/** Props of ServiceQuickActions. */
interface Props {
  /** Whether the service sits on a known server (the control-panel shortcut needs one). */
  hasServer: boolean
  /** Whether the service is active; the portal offers a client the panel sign-in on an active service only. */
  isActive: boolean
  /** Whether the control-panel sign-in is in flight. */
  ssoLoading: boolean
}

defineProps<Props>()

/** Events the component raises. */
const emit = defineEmits<{
  /** The reader asked to sign in to the control panel. */
  cpanel: []
  /** The reader asked to reload the related invoices. */
  invoices: []
  /** The reader asked to start a cancellation. */
  cancel: []
}>()
</script>
