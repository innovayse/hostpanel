<script setup lang="ts">
/**
 * One slot widget of a plugin with its product binding.
 *
 * The chosen products show as removable chips; a kit select adds one more from the products not yet
 * chosen, so the control is a multi select built from the single-value `UiSelect`. Nothing is sent
 * until the operator presses Save; an empty list means the plugin's own product list applies.
 */
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiAlignItems } from '@/utils/ui/layout'
import { UiSize } from '@/utils/ui/size'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'
import type { Product } from '@/types/product'
import type { PluginSlotWidgetBindingDto } from '@/types/plugin'

const { t } = useI18n()

/** Icon markup (shape tags) of the chip's remove button. */
const CLOSE_ICON = '<line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/>'

/** Props for PluginSlotWidgetRow. */
const props = defineProps<{
  /** Id of the plugin that declares the widget. */
  pluginId: string
  /** The widget and the products it is bound to now. */
  widget: PluginSlotWidgetBindingDto
  /** Every product the operator can choose from. */
  products: Product[]
  /** True while this widget's binding is being saved. */
  saving?: boolean
  /** Sentence of the last refused save of this widget, null when none. */
  error?: string | null
}>()

const emit = defineEmits<{
  /** Emitted when the operator presses Save, with the product ids to bind. */
  save: [productIds: number[]]
}>()

/** The product ids chosen in the editor, not yet saved. */
const draft = ref<number[]>([...props.widget.productIds])

/** The add-select's value; it snaps back to null after each pick so it always shows its placeholder. */
const pending = ref<number | null>(null)

/** Re-reads the draft when the server's binding changes (after a save or a reload). */
watch(() => props.widget.productIds, (ids) => {
  draft.value = [...ids]
})

/** Products still available to add: those not in the draft. */
const options = computed(() =>
  props.products
    .filter(product => !draft.value.includes(product.id))
    .map(product => ({ value: product.id as number | null, label: product.name })),
)

/** The chosen products as id and name; an id with no known product keeps its number as the name. */
const chosen = computed(() =>
  draft.value.map(id => ({ id, name: props.products.find(product => product.id === id)?.name ?? String(id) })),
)

/** Whether the widget is bound, yet every product it was bound to is gone, so it shows nowhere. */
const orphaned = computed<boolean>(() => props.widget.isBound && props.widget.productIds.length === 0)

/** Whether the draft differs from the saved binding, ignoring order. */
const dirty = computed<boolean>(() => {
  const saved = [...props.widget.productIds].sort((a, b) => a - b)
  const current = [...draft.value].sort((a, b) => a - b)
  return saved.length !== current.length || saved.some((id, index) => id !== current[index])
})

/**
 * Adds the picked product to the draft.
 *
 * @param id - The picked product id, null when the select was cleared.
 */
const add = (id: number | null): void => {
  if (id !== null && !draft.value.includes(id)) {
    draft.value = [...draft.value, id]
  }
  pending.value = null
}

/**
 * Removes a product from the draft.
 *
 * @param id - The product id to drop.
 */
const remove = (id: number): void => {
  draft.value = draft.value.filter(existing => existing !== id)
}

/** Sends the draft to the parent to be saved. */
const submit = (): void => {
  emit('save', [...draft.value])
}
</script>

<template>
  <UiStack gap="sm" :data-testid="`plugin-widget-${pluginId}-${widget.widgetId}`">
    <UiStack gap="xs">
      <UiText :tone="UiTextTone.Primary" :size="UiTextSize.Md">
        {{ widget.title ?? t('plugins.pluginSlotWidgets.pluginWidget', { plugin: pluginId, widget: widget.widgetId }) }}
      </UiText>
      <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Xs">
        {{ t('plugins.pluginSlotWidgets.slot', { slot: widget.slotId }) }}
      </UiText>
    </UiStack>

    <UiAlert v-if="orphaned" data-testid="plugin-widget-orphaned">
      {{ t('plugins.pluginSlotWidgets.orphaned') }}
    </UiAlert>

    <UiStack direction="row" gap="xs" wrap data-testid="plugin-widget-chosen">
      <UiBadge v-for="item in chosen" :key="item.id" :tone="UiTone.Primary">
        {{ item.name }}
        <UiIconButton
          :label="t('plugins.pluginSlotWidgets.removeProduct', { name: item.name })"
          :size="UiSize.Sm"
          :data-testid="`plugin-widget-remove-${item.id}`"
          @click="remove(item.id)"
        >
          <UiIcon :markup="CLOSE_ICON" :size="UiSize.Sm" />
        </UiIconButton>
      </UiBadge>
      <UiText v-if="chosen.length === 0" :tone="UiTextTone.Muted" :size="UiTextSize.Sm">
        {{ t('plugins.pluginSlotWidgets.noProducts') }}
      </UiText>
    </UiStack>

    <UiStack direction="row" :align="UiAlignItems.Center" wrap>
      <UiSelect
        v-model="pending"
        :options="options"
        :placeholder="t('plugins.pluginSlotWidgets.addProduct')"
        searchable
        class="min-w-56 flex-1"
        data-testid="plugin-widget-add"
        @update:model-value="add"
      />
      <UiButton
        type="button"
        :variant="UiButtonVariant.Primary"
        :disabled="!dirty || saving"
        :loading="saving"
        data-testid="plugin-widget-save"
        @click="submit"
      >
        {{ saving ? t('common.saving') : t('common.save') }}
      </UiButton>
    </UiStack>
    <UiText v-if="error" :tone="UiTextTone.Danger" :size="UiTextSize.Sm" data-testid="plugin-widget-error">
      {{ error }}
    </UiText>
  </UiStack>
</template>
