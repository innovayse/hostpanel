<script setup lang="ts">
/**
 * Dialog of the Open panel button for a panel that cannot sign anybody in by itself: it shows how to sign in by hand,
 * which is the panel's own sign in page (a link that opens in a new tab, never an auto-submitted form), the user name, the
 * domain and the server of the account. It never shows a password: the panel does not reveal one, and the host never
 * passes a stored one on this way. It holds no request: the page reads the details and hands them in.
 */
import { useI18n } from 'vue-i18n'
import UiButton from '@/components/ui/UiButton.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import type { PanelLoginDetails } from '@/types/service'
import { UiActionKind } from '@/utils/ui/actions'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiTextTone } from '@/utils/ui/text'

const { t } = useI18n()

/** Props for ServicePanelLoginDialog. */
defineProps<{
  /** How to sign in by hand to the service's panel. */
  details: PanelLoginDetails
}>()

const emit = defineEmits<{
  /** Emitted when the dialog is closed. */
  close: []
}>()
</script>

<template>
  <UiDialog
    :title="t('clients.clientServiceDetail.panelLoginTitle')"
    data-testid="client-service-panel-login-dialog"
    @close="emit('close')"
  >
    <UiStack>
      <UiText :tone="UiTextTone.Secondary">{{ t('clients.clientServiceDetail.panelLoginIntro') }}</UiText>
      <UiLink
        :href="details.loginUrl"
        external
        :variant="UiButtonVariant.Primary"
        :action="UiActionKind.Open"
        data-testid="client-service-panel-login-link"
      >
        {{ t('clients.clientServiceDetail.panelLoginOpen') }}
      </UiLink>
      <UiStack direction="row" wrap gap="sm">
        <UiText :tone="UiTextTone.Muted">{{ t('shared.username') }}</UiText>
        <UiText data-testid="client-service-panel-login-username">{{ details.username }}</UiText>
      </UiStack>
      <UiStack v-if="details.domain" direction="row" wrap gap="sm">
        <UiText :tone="UiTextTone.Muted">{{ t('clients.clientServiceDetail.panelLoginDomain') }}</UiText>
        <UiText data-testid="client-service-panel-login-domain">{{ details.domain }}</UiText>
      </UiStack>
      <UiStack direction="row" wrap gap="sm">
        <UiText :tone="UiTextTone.Muted">{{ t('clients.clientServiceDetail.panelLoginServer') }}</UiText>
        <UiText data-testid="client-service-panel-login-server">{{ details.serverAddress }}</UiText>
      </UiStack>
      <UiText :tone="UiTextTone.Muted">{{ t('clients.clientServiceDetail.panelLoginNoPassword') }}</UiText>
    </UiStack>

    <template #footer>
      <UiButton
        :action="UiActionKind.Close"
        :variant="UiButtonVariant.Secondary"
        data-testid="client-service-panel-login-close"
        @click="emit('close')"
      >
        {{ t('shared.close') }}
      </UiButton>
    </template>
  </UiDialog>
</template>
