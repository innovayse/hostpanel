<template>
  <UiStack gap="sm" data-testid="domain-sidebar">
    <UiNavGroup :title="$t('client.domains.sectionManage')" :icon="Settings">
      <UiNavItem
        v-for="tab in tabs"
        :key="tab.key"
        :icon="tab.icon"
        :active="activeTab === tab.key"
        :disabled="isPending && tab.key !== DomainTab.Overview"
        :testid="`domain-tab-${tab.key}`"
        @click="choose(tab.key)"
      >
        {{ tab.label }}
      </UiNavItem>
    </UiNavGroup>

    <UiNavGroup v-if="canOrder" :title="$t('client.domains.sectionActions')" :icon="Zap">
      <UiNavItem :icon="RefreshCw" :disabled="isPending" testid="domain-action-renew" @click="choose(DomainTab.Renew)">
        {{ $t('client.domains.actionRenewDomain') }}
      </UiNavItem>
      <UiNavItem to="/domains" :icon="PlusCircle" testid="domain-action-register">
        {{ $t('client.domains.actionRegisterNew') }}
      </UiNavItem>
    </UiNavGroup>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * DomainSidebar - the side menu of the domain detail page: the panels to switch between (all but
 * the overview are locked while the registrar is still processing the domain) and the actions
 * renew and register another domain.
 */
import { computed } from 'vue'
import { LayoutGrid, Lock, Network, PlusCircle, RefreshCw, Settings, UserCog, Zap } from 'lucide-vue-next'
import { Permission } from '~/composables/usePermissions'
import { useClientStore } from '~/stores/client'
import { DomainTab } from '~/types/domaintab'

/** Translates a key into the active locale. */
const { t } = useI18n()

/** Props of DomainSidebar. */
interface Props {
  /** The panel currently shown, a `DomainTab` value. */
  activeTab: string
  /** Whether the registrar is still registering or transferring the domain. */
  isPending: boolean
}

/** The props of the component. */
const props = defineProps<Props>()

/** Events the component raises. */
const emit = defineEmits<{
  /** The reader picked another panel. */
  'update:activeTab': [value: DomainTab]
}>()

/** The client store, which knows what the signed-in person may do. */
const store = useClientStore()

/** Whether the person may change domain settings; without it only the overview is offered. */
const canManage = computed(() => store.mayDo(Permission.ManageDomainSettings))

/** Whether the person may place orders, which is what renewing or registering a domain is. */
const canOrder = computed(() => store.mayDo(Permission.PlaceNewOrders))

/** The panels the menu switches between: the overview for everyone, the settings panels for those who may change them. */
const tabs = computed(() => [
  { key: DomainTab.Overview, label: t('client.domains.tabOverview'), icon: LayoutGrid },
  ...(canManage.value
    ? [
        { key: DomainTab.AutoRenew, label: t('client.domains.tabAutoRenew'), icon: RefreshCw },
        { key: DomainTab.Nameservers, label: t('client.domains.tabNameservers'), icon: Network },
        { key: DomainTab.Lock, label: t('client.domains.tabRegistrarLock'), icon: Lock },
        { key: DomainTab.Contact, label: t('client.domains.tabContactInfo'), icon: UserCog },
      ]
    : []),
])

/**
 * Switches panel, unless the domain is pending and the panel is not the overview.
 *
 * @param tab - The panel the reader picked.
 */
const choose = (tab: DomainTab): void => {
  if (props.isPending && tab !== DomainTab.Overview) {
    return
  }
  emit('update:activeTab', tab)
}
</script>
