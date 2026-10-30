<template>
  <UiStack gap="sm" data-testid="service-sidebar">
    <UiNavGroup :title="$t('client.services.sectionManage')" :icon="Settings">
      <UiNavItem
        v-for="tab in tabs"
        :key="tab.key"
        :icon="tab.icon"
        :active="activeTab === tab.key"
        :testid="`service-tab-${tab.key}`"
        @click="emit('update:activeTab', tab.key)"
      >
        {{ tab.label }}
      </UiNavItem>
    </UiNavGroup>

    <UiNavGroup v-if="canTicket || canSetUp" :title="$t('client.services.sectionActions')" :icon="Zap">
      <UiNavItem v-if="canTicket" :to="CLIENT_TICKET_NEW_PATH" :icon="MessageSquare" testid="service-open-ticket">
        {{ $t('client.services.actionOpenTicket') }}
      </UiNavItem>
      <UiNavItem
        v-if="canSetUp"
        :to="clientServiceSetupPath(serviceId)"
        :icon="Zap"
        tone="warning"
        testid="service-nav-setup"
      >
        {{ $t('client.services.completeSetup') }}
      </UiNavItem>
    </UiNavGroup>

    <UiNavGroup v-if="service.server" :title="$t('client.services.sectionQuickLinks')" :icon="Link2">
      <UiNavItem v-if="canSignIn" :icon="Monitor" :loading="ssoLoading" testid="service-link-cpanel" @click="emit('cpanel')">
        {{ $t('client.services.linkCpanel') }}
      </UiNavItem>
      <UiNavItem :href="webmailUrl" :icon="Mail" testid="service-link-webmail">
        {{ $t('client.services.linkWebmail') }}
      </UiNavItem>
      <!-- Automatic sign-in failed; the panel's own login form was opened instead. -->
      <UiText v-if="ssoError" size="xs" tone="danger" class="px-3 pb-3" data-testid="service-sso-error">{{ ssoError }}</UiText>
    </UiNavGroup>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * ServiceSidebar - the side menu of the service detail page: the panels to switch between, the
 * actions (open a ticket, finish setup) and, when the server is known, quick links to the
 * control panel and webmail.
 */
import { computed } from 'vue'
import { KeyRound, LayoutGrid, Link2, Mail, MessageSquare, Monitor, Settings, Terminal, XCircle, Zap } from 'lucide-vue-next'
import { CLIENT_TICKET_NEW_PATH, clientServiceSetupPath } from '~/constants/clientRoutes'
import { Permission } from '~/composables/usePermissions'
import { useClientStore } from '~/stores/client'
import type { ClientService } from '~/types/clientservice'
import { ServiceStatus } from '~/types/servicestatus'
import { ServiceTab } from '~/types/servicetab'
import { webmailUrl as buildWebmailUrl } from '~/utils/controlPanel'

/** Translates a key into the active locale. */
const { t } = useI18n()

/** Props of ServiceSidebar. */
interface Props {
  /** The service being shown. */
  service: ClientService
  /** The id of the service, for the setup link. */
  serviceId: string
  /** The panel currently shown, a `ServiceTab` value. */
  activeTab: string
  /** Whether the control-panel sign-in is in flight. */
  ssoLoading: boolean
  /** The sentence for a failed automatic sign-in; empty when there is none. */
  ssoError: string
}

/** The props, read by the webmail address. */
const props = defineProps<Props>()

/** Events the component raises. */
const emit = defineEmits<{
  /** The reader picked another panel. */
  'update:activeTab': [value: ServiceTab]
  /** The reader asked to sign in to the control panel. */
  cpanel: []
}>()

/** The client store, which knows what the signed-in person may do. */
const store = useClientStore()

/** Whether the person may open a support ticket. */
const canTicket = computed(() => store.mayDo(Permission.ViewOpenSupportTickets))

/** Whether the person may complete an order's setup, and cancel a service: both place orders. */
const canOrder = computed(() => store.mayDo(Permission.PlaceNewOrders))

/** Whether the setup action is offered: a pending hosting service (a digital one has none) the person may order for. */
const canSetUp = computed(() => !props.service.isDigital && props.service.status === ServiceStatus.Pending && canOrder.value)

/** Whether the person may sign in to the control panel from here. */
const canSignIn = computed(() => store.mayDo(Permission.AllowSingleSignOn))

/** Whether the person may change the service's control-panel password. */
const canChangePassword = computed(() => store.mayDo(Permission.ViewModifyPasswords))

/** Address of the service's webmail, on its own server; undefined while no server is assigned (the links are hidden then). */
const webmailUrl = computed(() => (props.service.server ? buildWebmailUrl(props.service.server.hostname) : undefined))

/** The panels the menu switches between. */
const tabs = computed(() => [
  { key: ServiceTab.Overview, label: t('client.services.tabOverview'), icon: LayoutGrid },
  ...(canChangePassword.value
    ? [{ key: ServiceTab.Password, label: t('client.services.tabChangePassword'), icon: KeyRound }]
    : []),
  // SSH keys are login credentials, so they sit behind the same permission as the password.
  ...(canChangePassword.value
    ? [{ key: ServiceTab.Ssh, label: t('client.services.tabSsh'), icon: Terminal }]
    : []),
  ...(canOrder.value ? [{ key: ServiceTab.Cancel, label: t('client.services.tabCancel'), icon: XCircle }] : []),
])
</script>
