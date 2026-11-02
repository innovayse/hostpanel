<template>
  <UiCard padding="md" data-testid="service-quick-actions">
    <UiHeading :level="3" size="subsection" class="!text-sm mb-3">{{ $t('client.services.whatToday') }}</UiHeading>
    <UiStack gap="xs" align="start">
      <UiButton
        v-if="canSignIn"
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
import type { ClientService } from '~/types/clientservice'
import { canSignInToControlPanel } from '~/utils/controlPanel'

/** The client store, which knows what the signed-in person may do. */
const store = useClientStore()

/** Props of ServiceQuickActions. */
interface Props {
  /** The service the shortcuts act on; the panel sign-in needs it active and on a known server. */
  service: ClientService
  /** Whether the control-panel sign-in is in flight. */
  ssoLoading: boolean
}

/** The props of the component. */
const props = defineProps<Props>()

/** Whether the control-panel shortcut is offered: an active service on a known server, to a person allowed single sign-on. */
const canSignIn = computed(() => canSignInToControlPanel(props.service, store.mayDo(Permission.AllowSingleSignOn)))

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
