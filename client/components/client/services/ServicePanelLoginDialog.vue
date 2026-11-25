<template>
  <UiDialog
    :model-value="true"
    :title="$t('client.services.panelLoginTitle')"
    size="md"
    @update:model-value="emit('close')"
  >
    <UiStack gap="md">
      <UiText size="sm" tone="muted">{{ $t('client.services.panelLoginIntro') }}</UiText>
      <UiButton
        variant="primary"
        :href="details.loginUrl"
        target="_blank"
        rel="noopener noreferrer"
        data-testid="service-panel-login-link"
      >
        <ExternalLink :size="14" class="mr-1.5" />
        {{ $t('client.services.panelLoginOpen') }}
      </UiButton>
      <UiStack gap="xs">
        <UiKeyValue :label="$t('client.services.panelLoginUsername')" data-testid="service-panel-login-username">{{ details.username }}</UiKeyValue>
        <UiKeyValue v-if="details.domain" :label="$t('client.services.panelLoginDomain')" data-testid="service-panel-login-domain">{{ details.domain }}</UiKeyValue>
        <UiKeyValue :label="$t('client.services.panelLoginServer')" data-testid="service-panel-login-server">{{ details.serverAddress }}</UiKeyValue>
      </UiStack>
      <UiText size="xs" tone="muted">{{ $t('client.services.panelLoginNoPassword') }}</UiText>
    </UiStack>
  </UiDialog>
</template>

<script setup lang="ts">
/**
 * ServicePanelLoginDialog. What the "Open panel" control shows for a panel whose serving plugin cannot sign a person in by itself:
 * the panel's own sign-in page as a link that opens in a new tab (never an auto-submitted form), and the user name, domain and
 * server of the account. It never shows a password: the panel does not reveal one, and the API does not send one on this route.
 * It holds no request: the store reads the details and the screen hands them in.
 */
import { ExternalLink } from 'lucide-vue-next'
import type { PanelLoginDetails } from '~/types/panellogindetails'

/** Props of ServicePanelLoginDialog. */
interface Props {
  /** How to sign in by hand to the service's panel. */
  details: PanelLoginDetails
}

/** The props of the component. */
defineProps<Props>()

/** Events the component raises. */
const emit = defineEmits<{
  /** The dialog was closed (its button, Escape or the backdrop). */
  close: []
}>()
</script>
