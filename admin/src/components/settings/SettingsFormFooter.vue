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
 * shows an idle bar instead of an empty space.
 *
 * Below `sm` the bar is compact so it stays one row: smaller padding, 32px buttons, and the short labels
 * (`saveLabelCompact`, `cancelLabelCompact`) when the screen gives them, since the full ones wrap on a phone.
 */
import { useI18n } from 'vue-i18n'
import UiButton from '@/components/ui/UiButton.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'

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
  /** Shorter save label for a phone (already translated); the full label is used when omitted. */
  saveLabelCompact?: string
  /** Shorter secondary label for a phone (already translated); the full label is used when omitted. */
  cancelLabelCompact?: string
  /** False when there is nothing to save or discard; both buttons are then disabled. */
  dirty?: boolean
}>(), {
  status: undefined,
  cancelLabel: undefined,
  saveLabelCompact: undefined,
  cancelLabelCompact: undefined,
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
  <div class="glass-strong sticky bottom-4 z-30 rounded-card px-3 py-2 sm:bottom-6 sm:px-6 sm:py-4 lg:bottom-8">
    <UiStack direction="row" align="center" justify="between" gap="sm">
      <UiText v-if="status" tone="secondary" size="md" role="status" class="min-w-0 truncate" data-testid="settings-form-status">
        {{ status }}
      </UiText>
      <UiStack direction="row" align="center" justify="end" gap="sm" class="ml-auto shrink-0">
        <UiButton
          type="button"
          variant="secondary"
          class="whitespace-nowrap max-sm:h-8 max-sm:px-3 max-sm:text-[0.8125rem]"
          :disabled="!dirty || saving"
          data-testid="settings-form-cancel"
          @click="emit('cancel')"
        >
          <template v-if="cancelLabelCompact">
            <span class="sm:hidden">{{ cancelLabelCompact }}</span>
            <span class="max-sm:hidden">{{ cancelLabel ?? t('shared.cancel') }}</span>
          </template>
          <template v-else>
            {{ cancelLabel ?? t('shared.cancel') }}
          </template>
        </UiButton>
        <UiButton
          type="button"
          variant="primary"
          class="whitespace-nowrap max-sm:h-8 max-sm:px-3 max-sm:text-[0.8125rem]"
          :loading="saving"
          :disabled="!dirty"
          data-testid="settings-form-save"
          @click="emit('save')"
        >
          <template v-if="saveLabelCompact">
            <span class="sm:hidden">{{ saveLabelCompact }}</span>
            <span class="max-sm:hidden">{{ saveLabel }}</span>
          </template>
          <template v-else>
            {{ saveLabel }}
          </template>
        </UiButton>
      </UiStack>
    </UiStack>
  </div>
</template>
