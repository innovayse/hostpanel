<script setup lang="ts">
/**
 * The "Slot widgets" section of one plugin, in the Settings tab of its details page.
 *
 * Lists every widget the plugin puts into a page slot, each with a product multi select
 * and its own Save. Product ids differ per installation, so a plugin cannot know them when it is
 * built; the operator binds them here after install. The card holds no state of its own beyond which
 * widget is saving: the parent loads the widgets and products and performs the save.
 */
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import PluginSlotWidgetRow from '@/components/plugins/PluginSlotWidgetRow.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiPluginAvatar from '@/components/ui/UiPluginAvatar.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { UiSize } from '@/utils/ui/size'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import type { PluginSlotWidgetBindingDto } from '@/types/plugin'
import type { Product } from '@/types/product'

const { t } = useI18n()

/** Props for PluginSlotWidgetsCard. */
const props = defineProps<{
  /** Id of the plugin that declares the widgets. */
  pluginId: string
  /** Display name of the plugin, shown in the heading. */
  pluginName: string
  /** Address of the plugin's icon; null when it has none. */
  pluginIconUrl?: string | null
  /** Hex colour of the plugin's fallback block. */
  pluginColor?: string
  /** The slot widgets with their current binding. */
  widgets: PluginSlotWidgetBindingDto[]
  /** Every product the operator can choose from. */
  products: Product[]
  /** Persists one widget's binding and answers whether it was stored. */
  save: (widget: PluginSlotWidgetBindingDto, productIds: number[]) => Promise<boolean>
  /** Sentence of the last refused save, null when none. */
  error?: string | null
}>()

/** Key (`slot:widget`) of the widget being saved, null when none. */
const savingKey = ref<string | null>(null)

/** Key (`slot:widget`) of the widget whose last save was refused, null when none. */
const failedKey = ref<string | null>(null)

/**
 * Builds the key that tells one widget from another.
 *
 * @param widget - The widget.
 * @returns Its `slot:widget` key.
 */
const keyOf = (widget: PluginSlotWidgetBindingDto): string => `${widget.slotId}:${widget.widgetId}`

/**
 * Saves one widget's binding and remembers which row a refusal belongs to.
 *
 * @param widget - The widget being bound.
 * @param productIds - The product ids to bind it to.
 */
const saveWidget = async (widget: PluginSlotWidgetBindingDto, productIds: number[]): Promise<void> => {
  const key = keyOf(widget)
  savingKey.value = key
  const ok = await props.save(widget, productIds)
  savingKey.value = null
  failedKey.value = ok ? null : key
}
</script>

<template>
  <UiCard padding="md" :data-testid="`plugin-slot-widgets-${pluginId}`">
    <UiStack gap="md">
      <UiStack gap="sm">
        <UiStack direction="row" align="center" gap="sm">
          <UiPluginAvatar :name="pluginName" :icon-url="pluginIconUrl" :color="pluginColor" :size="UiSize.Sm" />
          <UiHeading :level="2" variant="section">{{ t('plugins.pluginSlotWidgets.title', { plugin: pluginName }) }}</UiHeading>
        </UiStack>
        <UiText :tone="UiTextTone.Secondary" :size="UiTextSize.Md">{{ t('plugins.pluginSlotWidgets.description') }}</UiText>
      </UiStack>
      <PluginSlotWidgetRow
        v-for="widget in widgets"
        :key="keyOf(widget)"
        :plugin-id="pluginId"
        :widget="widget"
        :products="products"
        :saving="savingKey === keyOf(widget)"
        :error="failedKey === keyOf(widget) ? error : null"
        @save="saveWidget(widget, $event)"
      />
    </UiStack>
  </UiCard>
</template>
