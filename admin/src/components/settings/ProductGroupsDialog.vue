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
import UiText from '@/components/ui/UiText.vue'
import { Icon } from '@/lib/icons'
import type { PaymentMethodOption, ProductGroup } from '@/types/product'
import { UiSize } from '@/utils/ui/size'
import { UiActionKind } from '@/utils/ui/actions'

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
  <UiDialog :title="t('settings.productGroups.title')" size="md" @close="emit('close')">
    <UiStack data-testid="product-groups-dialog">
      <UiText v-if="groups.length === 0" tone="muted" size="sm" data-testid="product-groups-empty">
        {{ t('settings.productGroups.empty') }}
      </UiText>
      <UiStack
        v-for="group in groups"
        :key="group.id"
        direction="row"
        align="center"
        justify="between"
        :data-testid="`product-group-${group.id}`"
      >
        <UiStack gap="xs">
          <UiText tone="primary" size="md">{{ group.name }}</UiText>
          <UiText tone="muted" size="sm">
            {{ t('settings.productGroups.products', { count: group.productCount }) }} · {{ allowedLabel(group) }}
          </UiText>
        </UiStack>
        <UiIconButton
          :label="t('settings.productGroups.edit')"
          :size="UiSize.Sm"
          :data-testid="`product-group-edit-${group.id}`"
          @click="emit('edit', group)"
        >
          <UiIcon :markup="Icon.Edit" :size="UiSize.Sm" />
        </UiIconButton>
      </UiStack>
      <UiButton :action="UiActionKind.Add" variant="secondary" data-testid="product-groups-add" @click="emit('add')">
        {{ t('settings.productGroups.add') }}
      </UiButton>
    </UiStack>
  </UiDialog>
</template>
