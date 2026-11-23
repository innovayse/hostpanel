<script setup lang="ts">
/**
 * The action bar pinned to the bottom of a full-page settings form (Cancel and Save).
 *
 * Both the slide form and the TLD form are long enough that the buttons would scroll away, so they
 * live in a bar that sticks to the bottom edge of the scrolling content area. It is sticky, not
 * fixed: a fixed bar is laid over the sidebar and the scrollbar as well, and hides the last fields
 * of the form. It is a rounded glass bar inside the content width, lifted off the bottom edge by the
 * same amount as the layout's content gutter, so it never runs edge to edge.
 *
 * The bar can also report the form's state: `status` is a line on the left (for example the count
 * of unsaved changes) and `dirty` false disables both buttons, so a page whose edits are optional
 * shows an idle bar instead of an empty space. `saveDisabled` turns off Save alone (the edits are there but break a rule), so
 * Cancel still discards them.
 *
 * Both buttons carry an icon (undo, check) and a short label, so the bar stays one row on a phone: below `sm` it has
 * smaller padding, and both buttons are the small size. It floats 4px above the bottom edge of the scrolling area on every width.
 */
import { useI18n } from 'vue-i18n'
import UiButton from '@/components/ui/UiButton.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { UiActionKind } from '@/utils/ui/actions'
import { UiButtonType, UiButtonVariant } from '@/utils/ui/button'
import { UiAlignItems, UiDirection, UiGap, UiJustify } from '@/utils/ui/layout'
import { UiSize } from '@/utils/ui/size'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'

const { t } = useI18n()

/** Props for SettingsFormFooter. */
withDefaults(defineProps<{
  /** Label of the save button (already translated), e.g. "Create slide" or "Save Changes". */
  saveLabel: string
  /** True while the save request is in flight; the button is disabled and shows the saving label. */
  saving: boolean
  /** Line on the left of the bar, e.g. "3 unsaved changes" (already translated); omitted when empty. */
  status?: string
  /** Label of the secondary button (already translated); defaults to "Cancel". */
  cancelLabel?: string
  /** False when there is nothing to save or discard; both buttons are then disabled. */
  dirty?: boolean
  /** True when the edits cannot be saved as they are; only Save is disabled. */
  saveDisabled?: boolean
}>(), {
  saveDisabled: false,
  status: undefined,
  cancelLabel: undefined,
  dirty: true,
})

const emit = defineEmits<{
  /** Emitted when the operator presses Cancel. */
  cancel: []
  /** Emitted when the operator presses Save. */
  save: []
}>()
</script>

<template>
  <UiStack :direction="UiDirection.Row" :align="UiAlignItems.Center" :justify="UiJustify.Between" :gap="UiGap.Sm" class="glass-strong sticky bottom-1 z-30 rounded-card px-3 py-2 sm:px-6 sm:py-4">
    <UiText v-if="status" :tone="UiTextTone.Secondary" :size="UiTextSize.Md" role="status" class="min-w-0 truncate" data-testid="settings-form-status">
      {{ status }}
    </UiText>
    <UiStack :direction="UiDirection.Row" :align="UiAlignItems.Center" :justify="UiJustify.End" :gap="UiGap.Sm" class="ml-auto shrink-0">
      <UiButton
        :action="UiActionKind.Cancel"
        :type="UiButtonType.Button"
        :variant="UiButtonVariant.Secondary"
        :size="UiSize.Sm"
        :disabled="!dirty || saving"
        data-testid="settings-form-cancel"
        @click="emit('cancel')"
      >{{ cancelLabel ?? t('shared.cancel') }}
      </UiButton>
      <UiButton
        :action="UiActionKind.Save"
        :type="UiButtonType.Button"
        :variant="UiButtonVariant.Primary"
        :size="UiSize.Sm"
        :loading="saving"
        :disabled="!dirty || saveDisabled"
        data-testid="settings-form-save"
        @click="emit('save')"
      >{{ saveLabel }}
      </UiButton>
    </UiStack>
  </UiStack>
</template>
