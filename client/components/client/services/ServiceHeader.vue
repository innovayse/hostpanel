<template>
  <UiStack direction="row" align="center" justify="between" wrap data-testid="service-header">
    <UiStack direction="row" align="center">
      <UiTile :icon="Server" tone="primary" size="xl" />
      <UiStack gap="none">
        <UiHeading :level="1" size="subsection">{{ service.productName }}</UiHeading>
        <UiText size="sm" tone="muted">{{ service.domain || $t('client.dashboard.noDomain') }}</UiText>
      </UiStack>
    </UiStack>
    <UiStack direction="row" align="center" gap="sm">
      <UiButton
        v-if="canLogin"
        size="sm"
        :loading="ssoLoading"
        class="hidden sm:flex"
        data-testid="service-cpanel-login"
        @click="emit('cpanel')"
      >
        <Monitor :size="14" class="mr-1.5" />
        {{ $t('client.services.actionLoginCpanel') }}
      </UiButton>
      <ClientStatusBadge :status="service.status" />
    </UiStack>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * ServiceHeader - the title row of the service detail page: icon, plan name, domain, a "log in
 * to the control panel" button for an active service on a known server, and the status badge.
 */
import { computed } from 'vue'
import { Monitor, Server } from 'lucide-vue-next'
import type { ClientService } from '~/types/clientservice'
import { Permission } from '~/composables/usePermissions'
import { useClientStore } from '~/stores/client'
import { canSignInToControlPanel } from '~/utils/controlPanelSignIn'

/** Props of ServiceHeader. */
interface Props {
  /** The service being shown. */
  service: ClientService
  /** Whether the control-panel sign-in is in flight. */
  ssoLoading: boolean
}

/** The props of the component. */
const props = defineProps<Props>()

/** Events the component raises. */
const emit = defineEmits<{
  /** The reader asked to sign in to the control panel. */
  cpanel: []
}>()

/** The client store, which knows what the signed-in person may do. */
const store = useClientStore()

/** Whether the control-panel button is offered: an active service on a known server, to a person allowed single sign-on. */
const canLogin = computed(() => canSignInToControlPanel(props.service, store.mayDo(Permission.AllowSingleSignOn)))
</script>
