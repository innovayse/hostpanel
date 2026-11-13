<script setup lang="ts">
/**
 * Dialog for editing the language versions of one email template.
 *
 * One tab per language; each tab holds that language's subject, body and description. A language
 * with no stored version starts blank (mail in it falls back to the default language until one is
 * saved). Emits `save` for the language on screen, and `close` on cancel, Escape or a backdrop click.
 */
import { computed, reactive, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiField from '@/components/ui/UiField.vue'
import UiForm from '@/components/ui/UiForm.vue'
import UiFormActions from '@/components/ui/UiFormActions.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiTabs from '@/components/ui/UiTabs.vue'
import UiText from '@/components/ui/UiText.vue'
import UiTextarea from '@/components/ui/UiTextarea.vue'
import { useStorefrontLocaleTabs } from '@/composables/useStorefrontLocaleTabs'
import { MessageLanguage } from '@/locales/messages'
import type { EmailTemplate, EmailTemplateContent } from '@/types/emailtemplate'

const { t } = useI18n()

/** Props for EmailTemplateDialog. */
const props = defineProps<{
  /** The template slug whose versions are edited. */
  slug: string
  /** The stored versions of the template, at most one per language. */
  versions: EmailTemplate[]
  /** True while the save request is in flight. */
  saving: boolean
  /** The API's sentence for a refused save, null when none. */
  error: string | null
}>()

const emit = defineEmits<{
  /** Emitted on submit with the language on screen, its stored version id (null when new) and its words. */
  save: [language: MessageLanguage, existingId: number | null, content: EmailTemplateContent]
  /** Emitted when the user closes or cancels the dialog. */
  close: []
}>()

/** The language tab state. */
const { activeLocale, tabs, select } = useStorefrontLocaleTabs('email-template-tab')

/**
 * Creates the words of a language with no stored version.
 *
 * @returns Blank words.
 */
const blank = (): EmailTemplateContent => ({ subject: '', body: '', description: null })

/** The words of each language being edited. */
const forms = reactive<Record<MessageLanguage, EmailTemplateContent>>({
  [MessageLanguage.En]: blank(),
  [MessageLanguage.Ru]: blank(),
  [MessageLanguage.Hy]: blank(),
})

/** Fills every tab from the stored versions, blank where a language has none. */
watch(() => props.versions, (versions) => {
  for (const language of Object.values(MessageLanguage)) {
    const stored = versions.find((v) => v.language === language)
    forms[language] = stored
      ? { subject: stored.subject, body: stored.body, description: stored.description }
      : blank()
  }
}, { immediate: true })

/** The stored version of the language on screen, undefined when there is none yet. */
const activeVersion = computed<EmailTemplate | undefined>(() =>
  props.versions.find((v) => v.language === activeLocale.value))

/** Submits the language on screen by emitting the save event. */
const handleSubmit = (): void => {
  const form = forms[activeLocale.value]
  const description = form.description?.trim() ?? ''
  emit('save', activeLocale.value, activeVersion.value?.id ?? null, {
    subject: form.subject.trim(),
    body: form.body,
    description: description === '' ? null : description,
  })
}
</script>

<template>
  <UiDialog :title="slug" size="xl" @close="emit('close')">
    <UiForm stacked data-testid="email-template-form" @submit="handleSubmit">
      <UiTabs :items="tabs" :model-value="activeLocale" @update:model-value="select" />

      <UiText v-if="!activeVersion" tone="muted" size="sm" data-testid="email-template-missing">
        {{ t('settings.emailTemplates.missingVersion') }}
      </UiText>

      <UiField :label="t('shared.subject')">
        <template #default="{ id, invalid, describedby }">
          <UiInput
            :id="id"
            v-model="forms[activeLocale].subject"
            required
            :invalid="invalid"
            :aria-describedby="describedby"
            :placeholder="t('settings.emailTemplates.subjectPlaceholder')"
            data-testid="email-template-subject"
          />
        </template>
      </UiField>

      <UiField :label="t('settings.emailTemplates.body')">
        <template #default="{ id }">
          <UiTextarea
            :id="id"
            v-model="forms[activeLocale].body"
            required
            rows="14"
            :placeholder="t('settings.emailTemplates.bodyPlaceholder')"
            data-testid="email-template-body"
            class="font-mono"
          />
        </template>
      </UiField>

      <UiField :label="t('shared.description')">
        <template #default="{ id }">
          <UiInput
            :id="id"
            v-model="forms[activeLocale].description"
            :placeholder="t('settings.emailTemplates.descriptionPlaceholder')"
            data-testid="email-template-description"
          />
        </template>
      </UiField>

      <UiAlert v-if="error" variant="error" data-testid="email-template-error">
        {{ error }}
      </UiAlert>

      <UiFormActions>
        <UiButton type="button" variant="secondary" data-testid="email-template-cancel" @click="emit('close')">
          {{ t('shared.cancel') }}
        </UiButton>
        <UiButton type="submit" variant="primary" :loading="saving" data-testid="email-template-save">
          {{ t('shared.save') }}
        </UiButton>
      </UiFormActions>
    </UiForm>
  </UiDialog>
</template>
