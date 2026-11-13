<script setup lang="ts">
/**
 * Email templates view: the notification templates, one row per template, with the languages
 * each one is written in.
 *
 * Reads `stores/settings`; Edit opens the language versions of a template, where staff rewrite a
 * version or add a missing one. A refused load or save shows the API's sentence.
 */
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import EmailTemplateDialog from '@/components/settings/EmailTemplateDialog.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiRowActions from '@/components/ui/UiRowActions.vue'
import type { UiMenuAction } from '@/utils/ui/menu'
import { useDocsGuide } from '@/composables/useDocsGuide'
import PageHelpLink from '@/components/layout/PageHelpLink.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import { MessageLanguage } from '@/locales/messages'
import { useSettingsStore } from '@/stores/settings'
import type { EmailTemplate, EmailTemplateContent, EmailTemplateGroup } from '@/types/emailtemplate'
import { UiTableDensity } from '@/utils/ui/injection'

const { t } = useI18n()
/** Link to the docs guide for this screen, or null when docsUrl is not configured. */
const { guideUrl } = useDocsGuide()

/** Settings store: holds the templates and the load state. */
const store = useSettingsStore()

/** The templates, one entry per slug, in the order the API listed them. */
const groups = computed<EmailTemplateGroup[]>(() => {
  const bySlug = new Map<string, EmailTemplate[]>()
  for (const template of store.emailTemplates) {
    const versions = bySlug.get(template.slug) ?? []
    versions.push(template)
    bySlug.set(template.slug, versions)
  }
  return [...bySlug.entries()].map(([slug, versions]) => ({
    slug,
    versions,
    // The map only holds slugs with at least one version, so versions[0] always exists.
    shown: versions.find((v) => v.language === MessageLanguage.En) ?? versions[0] as EmailTemplate,
  }))
})

/** The slug whose versions are open in the dialog, null when it is closed. */
const editingSlug = ref<string | null>(null)

/** The versions of the open template. */
const editingVersions = computed<EmailTemplate[]>(() =>
  groups.value.find((g) => g.slug === editingSlug.value)?.versions ?? [])

/** True while a version is being saved. */
const saving = ref(false)

/**
 * Opens the language versions of a template.
 *
 * @param slug - The template slug.
 */
const openEditor = (slug: string): void => {
  editingSlug.value = slug
}

/** Closes the dialog. */
const closeEditor = (): void => {
  editingSlug.value = null
}

/**
 * Saves the language on screen; the dialog stays open so staff can move on to the next language.
 *
 * @param language - The version's language.
 * @param existingId - The stored version's id, null when the language has none yet.
 * @param content - Subject, body and description.
 */
const handleSave = async (
  language: MessageLanguage,
  existingId: number | null,
  content: EmailTemplateContent,
): Promise<void> => {
  if (editingSlug.value === null) {
    return
  }
  saving.value = true
  await store.saveEmailTemplateVersion(editingSlug.value, language, existingId, content)
  saving.value = false
}

onMounted(() => store.fetchEmailTemplates())

/**
 * The row actions of a template: its one action, Edit, which opens the language versions dialog.
 *
 * @param slug - The slug of the template in the row.
 * @returns The action list passed to UiRowActions.
 */
const templateActions = (slug: string): UiMenuAction[] => {
  /**
   * Opens the versions of this template.
   *
   * @returns Nothing.
   */
  const onEdit = (): void => {
    openEditor(slug)
  }
  return [{ label: t('shared.edit'), testid: `email-templates-edit-${slug}`, onSelect: onEdit }]
}
</script>

<template>
  <UiStack>
    <UiPageHeader :title="t('settings.emailTemplates.emailTemplates')" data-testid="email-templates-header">
      <template v-if="guideUrl" #actions>
        <PageHelpLink :url="guideUrl" />
      </template>
    </UiPageHeader>

    <UiErrorState
      v-if="store.error"
      :message="store.error"
      :loading="store.loading"
      testid="email-templates-error"
      @retry="store.fetchEmailTemplates()"
    />

    <UiCard v-else>
      <UiTable
        :density="UiTableDensity.Comfortable"
        :loading="store.loading"
        :empty="!store.loading && groups.length === 0"
        data-testid="email-templates-table"
      >
        <template #empty>
          {{ t('settings.emailTemplates.noTemplatesFound') }}
        </template>
        <UiTableHead>
          <UiTableRow>
            <UiTableHeader>{{ t('shared.name') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.subject') }}</UiTableHeader>
            <UiTableHeader>{{ t('settings.emailTemplates.languages') }}</UiTableHeader>
            <UiTableHeader actions>{{ t('shared.actions') }}</UiTableHeader>
          </UiTableRow>
        </UiTableHead>
        <UiTableBody divided>
          <UiTableRow
            v-for="group in groups"
            :key="group.slug"
            hoverable
            :data-testid="`email-templates-row-${group.slug}`"
          >
            <UiTableCell>{{ group.slug }}</UiTableCell>
            <UiTableCell muted>{{ group.shown.subject }}</UiTableCell>
            <UiTableCell>
              <UiStack direction="row" gap="xs">
                <UiBadge
                  v-for="version in group.versions"
                  :key="version.id"
                  small
                  :data-testid="`email-templates-row-${group.slug}-${version.language}`"
                >
                  {{ t(`storefront.locales.${version.language}`) }}
                </UiBadge>
              </UiStack>
            </UiTableCell>
            <UiTableCell actions>
              <UiRowActions :actions="templateActions(group.slug)" :testid="`email-templates-actions-${group.slug}`" />
            </UiTableCell>
          </UiTableRow>
        </UiTableBody>
      </UiTable>
    </UiCard>

    <EmailTemplateDialog
      v-if="editingSlug !== null"
      :slug="editingSlug"
      :versions="editingVersions"
      :saving="saving"
      :error="store.actionError"
      @save="handleSave"
      @close="closeEditor"
    />
  </UiStack>
</template>
