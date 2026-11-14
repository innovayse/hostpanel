<script setup lang="ts">
/**
 * The Settings tab of the plugin details page: the plugin's own configuration (the integration config form, when the
 * integrations list has an entry with the plugin's id) and the product bindings of the slot widgets it declares.
 * While the widgets load, a skeleton card holds their place. When the plugin has neither, a short note says so. The component loads nothing: the page loads the
 * integrations list and the widgets and passes them in, and performs the binding save.
 */
import { useI18n } from 'vue-i18n'
import IntegrationConfigPanel from '@/components/integrations/IntegrationConfigPanel.vue'
import PluginSlotWidgetsCard from '@/components/plugins/PluginSlotWidgetsCard.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiStack from '@/components/ui/UiStack.vue'
import type { PluginListItemDto, PluginSlotWidgetBindingDto } from '@/types/plugin'
import type { Product } from '@/types/product'

/** Translation function. */
const { t } = useI18n()

/** Props for PluginSettingsTab. */
defineProps<{
  /** The installed plugin the settings belong to. */
  plugin: PluginListItemDto
  /** Whether the integrations list has an entry for this plugin, so it has a config form. */
  hasConfig: boolean
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
}>()
</script>

<template>
  <UiStack gap="md" data-testid="plugin-details-settings">
    <IntegrationConfigPanel v-if="hasConfig" :slug="plugin.id" />
    <UiErrorState
      v-if="widgetsError !== null"
      :message="widgetsError"
      testid="plugin-slot-widgets-error"
      @retry="emit('retry')"
    />
    <UiCard
      v-else-if="widgetsLoading && widgets.length === 0"
      padding="md"
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
      v-if="!hasConfig && widgets.length === 0 && widgetsError === null && !widgetsLoading"
      :title="t('pluginDetails.settings.emptyTitle')"
      :description="t('pluginDetails.settings.emptyDescription')"
      data-testid="plugin-details-settings-empty"
    />
  </UiStack>
</template>
