<script setup lang="ts">
/**
 * Dialog listing the product groups, each with the payment methods it is restricted to ("All methods" when it is not),
 * an edit button per group and an add button. It only lists and reports the choice: the form dialogs open from the
 * parent through the `add` and `edit` events.
 */
import { useI18n } from 'vue-i18n'
import UiButton from '@/components/ui/UiButton.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiText from '@/components/ui/UiText.vue'
import { Icon } from '@/lib/icons'
import type { PaymentMethodOption, ProductGroup } from '@/types/product'
import { UiDialogSize } from '@/utils/ui/dialog'
import { UiAlignItems, UiDirection, UiGap, UiJustify } from '@/utils/ui/layout'
import { UiSize } from '@/utils/ui/size'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import { UiActionKind } from '@/utils/ui/actions'
import { UiButtonVariant } from '@/utils/ui/button'

const { t } = useI18n()

/** Props for ProductGroupsDialog. */
const props = defineProps<{
  /** Every product group. */
  groups: ProductGroup[]
  /** The payment methods the checkout offers, to name the ones a group allows. */
  methods: PaymentMethodOption[]
}>()

const emit = defineEmits<{
  /** Emitted when the user asks to add a group. */
  add: []
  /** Emitted when the user asks to edit a group. */
  edit: [group: ProductGroup]
  /** Emitted when the user closes the dialog. */
  close: []
}>()

/**
 * Names the payment methods a group may be paid with.
 *
 * @param group - The group to describe.
 * @returns The method names joined by commas, or the "all methods" label when the group restricts nothing.
 */
const allowedLabel = (group: ProductGroup): string => {
  if (group.allowedPaymentMethods.length === 0) {
    return t('settings.productGroups.allMethods')
  }
  return group.allowedPaymentMethods
    .map((module) => props.methods.find((method) => method.module === module)?.displayName ?? module)
    .join(', ')
}
</script>

<template>
  <UiDialog :title="t('settings.productGroups.title')" :size="UiDialogSize.Md" @close="emit('close')">
    <UiStack data-testid="product-groups-dialog">
      <UiEmptyState v-if="groups.length === 0" compact :title="t('settings.productGroups.empty')" data-testid="product-groups-empty" />
      <UiStack
        v-for="group in groups"
        :key="group.id"
        :direction="UiDirection.Row"
        :align="UiAlignItems.Center"
        :justify="UiJustify.Between"
        :data-testid="`product-group-${group.id}`"
      >
        <UiStack :gap="UiGap.Xs">
          <UiText :tone="UiTextTone.Primary" :size="UiTextSize.Md">{{ group.name }}</UiText>
          <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">
            {{ t('settings.productGroups.products', { count: group.productCount }) }} · {{ allowedLabel(group) }}
          </UiText>
        </UiStack>
        <UiIconButton
          :label="t('settings.productGroups.edit')"
          :variant="UiButtonVariant.Ghost"
          :size="UiSize.Sm"
          :data-testid="`product-group-edit-${group.id}`"
          @click="emit('edit', group)"
        >
          <UiIcon :markup="Icon.Edit" :size="UiSize.Sm" />
        </UiIconButton>
      </UiStack>
      <UiButton :action="UiActionKind.AddRow" :variant="UiButtonVariant.Secondary" data-testid="product-groups-add" @click="emit('add')">
        {{ t('settings.productGroups.create') }}
      </UiButton>
    </UiStack>
  </UiDialog>
</template>
