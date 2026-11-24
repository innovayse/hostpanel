<script setup lang="ts">
/**
 * The Settings tab of the plugin details page: the plugin's own configuration (the integration config form, when the
 * integrations list has an entry with the plugin's id), the servers a panel plugin serves (only when the plugin declares a server
 * module) and the product bindings of the slot widgets it declares.
 * While the servers or the widgets load, a skeleton card holds their place. When the plugin has none of the three, a short note says so.
 * The component loads nothing: the page loads the integrations list, the servers and the widgets and passes them in, and performs the binding save.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import IntegrationConfigPanel from '@/components/integrations/IntegrationConfigPanel.vue'
import PluginServersCard from '@/components/plugins/details/PluginServersCard.vue'
import PluginSlotWidgetsCard from '@/components/plugins/PluginSlotWidgetsCard.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiStack from '@/components/ui/UiStack.vue'
import type { PluginListItemDto, PluginSlotWidgetBindingDto } from '@/types/plugin'
import type { Product } from '@/types/product'
import type { ServerOptionDto } from '@/types/server'
import { UiGap } from '@/utils/ui/layout'
import { UiCardPadding } from '@/utils/ui/card'

/** Translation function. */
const { t } = useI18n()

/** Props for PluginSettingsTab. */
const props = defineProps<{
  /** The installed plugin the settings belong to. */
  plugin: PluginListItemDto
  /** Whether the integrations list has an entry for this plugin, so it has a config form. */
  hasConfig: boolean
  /** The server modules the plugin declares; empty for a plugin that serves none, which shows no servers section. */
  serverModules: string[]
  /** The servers stored on one of those modules. */
  servers: ServerOptionDto[]
  /** Sentence of the failed load of the servers, null when none. */
  serversError: string | null
  /** Whether the servers are loading, so a skeleton holds their place and the empty note waits. */
  serversLoading: boolean
  /** The slot widgets the plugin declares with their current binding. */
  widgets: PluginSlotWidgetBindingDto[]
  /** Every product a widget can be bound to. */
  products: Product[]
  /** Sentence of the failed load of the widgets, null when none. */
  widgetsError: string | null
  /** Whether the widgets are loading, so a skeleton holds their place and the empty note waits. */
  widgetsLoading: boolean
  /** Persists one widget's binding and answers whether it was stored. */
  save: (widget: PluginSlotWidgetBindingDto, productIds: number[]) => Promise<boolean>
  /** Sentence of the last refused binding save, null when none. */
  bindingError: string | null
}>()

/** Events emitted by PluginSettingsTab. */
const emit = defineEmits<{
  /** Emitted when the operator asks to load the widgets again. */
  retry: []
  /** Emitted when the operator asks to load the servers again. */
  retryServers: []
}>()

/** True when the plugin serves a server module, so the servers section belongs on its settings. */
const servesServers = computed<boolean>(() => props.serverModules.length > 0)

/** True when no request of the tab is still running or was refused, so the note for an empty tab may be shown. */
const settled = computed<boolean>(() => props.widgetsError === null && !props.widgetsLoading && props.serversError === null && !props.serversLoading)
</script>

<template>
  <UiStack :gap="UiGap.Md" data-testid="plugin-details-settings">
    <IntegrationConfigPanel v-if="hasConfig" :slug="plugin.id" :servers="servers" />
    <UiErrorState
      v-if="serversError !== null"
      :message="serversError"
      testid="plugin-servers-error"
      @retry="emit('retryServers')"
    />
    <UiCard
      v-else-if="serversLoading && !servesServers"
      :padding="UiCardPadding.Md"
      loading
      :skeleton-lines="3"
      data-testid="plugin-servers-loading"
    />
    <PluginServersCard v-else-if="servesServers" :modules="serverModules" :servers="servers" />
    <UiErrorState
      v-if="widgetsError !== null"
      :message="widgetsError"
      testid="plugin-slot-widgets-error"
      @retry="emit('retry')"
    />
    <UiCard
      v-else-if="widgetsLoading && widgets.length === 0"
      :padding="UiCardPadding.Md"
      loading
      :skeleton-lines="3"
      data-testid="plugin-slot-widgets-loading"
    />
    <PluginSlotWidgetsCard
      v-else-if="widgets.length > 0"
      :plugin-id="plugin.id"
      :plugin-name="plugin.name"
      :plugin-icon-url="plugin.iconUrl"
      :plugin-color="plugin.color"
      :widgets="widgets"
      :products="products"
      :save="save"
      :error="bindingError"
    />
    <UiEmptyState
      v-if="!hasConfig && !servesServers && widgets.length === 0 && settled"
      :title="t('pluginDetails.settings.emptyTitle')"
      :description="t('pluginDetails.settings.emptyDescription')"
      data-testid="plugin-details-settings-empty"
    />
  </UiStack>
</template>
