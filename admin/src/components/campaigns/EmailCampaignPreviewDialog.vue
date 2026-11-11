<script setup lang="ts">
/**
 * Dialog that shows a campaign mail as the recipients would read it: the rendered subject as the
 * title and the rendered HTML in a frame that runs nothing. The unsubscribe link in it is a sample.
 * Emits `close` on the close button, Escape or a backdrop click.
 */
import { useI18n } from 'vue-i18n'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiHtmlPreview from '@/components/ui/UiHtmlPreview.vue'
import type { EmailCampaignPreview } from '@/types/emailcampaign'
import { UiDialogSize } from '@/utils/ui/dialog'

const { t } = useI18n()

/** Props for EmailCampaignPreviewDialog. */
defineProps<{
  /** The rendered mail to show. */
  preview: EmailCampaignPreview
}>()

const emit = defineEmits<{
  /** Emitted when the operator closes the dialog. */
  close: []
}>()
</script>

<template>
  <UiDialog :title="preview.subject" :size="UiDialogSize.Xl" data-testid="email-campaign-preview" @close="emit('close')">
    <UiHtmlPreview
      :html="preview.html"
      :title="t('emailCampaigns.previewFrameTitle')"
      data-testid="email-campaign-preview-frame"
    />
  </UiDialog>
</template>
