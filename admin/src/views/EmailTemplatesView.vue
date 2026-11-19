<script setup lang="ts">
/**
 * Email templates view: the notification templates, one row per template, with the languages
 * each one is written in.
 *
 * Reads `stores/settings`; the whole list is loaded at once, so the search (name, key and subject) and the group filter
 * narrow it here, and both live in the address like every other list. A row shows the template's readable name with its
 * key beneath it, the subject with each `{{ placeholder }}` drawn as a chip, and one badge per language written
 * (EN, HY, RU). Edit opens the language versions of a template, where staff rewrite a version or add a missing one. A
 * refused load or save shows the API's sentence.
 */
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import EmailTemplateDialog from '@/components/settings/EmailTemplateDialog.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiListToolbar from '@/components/ui/UiListToolbar.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiRowActions from '@/components/ui/UiRowActions.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import type { UiMenuAction } from '@/utils/ui/menu'
import { useDocsGuide } from '@/composables/useDocsGuide'
import { useListQuery } from '@/composables/useListQuery'
import PageHelpLink from '@/components/layout/PageHelpLink.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiSchemaTableHead from '@/components/ui/UiSchemaTableHead.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import schema from '@/views/EmailTemplatesView.schema'
import { MessageLanguage } from '@/locales/messages'
import { useSettingsStore } from '@/stores/settings'
import type { EmailTemplate, EmailTemplateContent, EmailTemplateGroup } from '@/types/emailtemplate'
import { UiTableDensity } from '@/utils/ui/injection'
import { emailTemplateGroupOf, humanizeSlug, languageBadge, splitSubject } from '@/lib/emailTemplates'
import { Icon } from '@/lib/icons'
import { schemaHeader } from '@/lib/schemaHeader'
import { translateOrRaw } from '@/lib/translate'
import { UiTextSize, UiTextTag } from '@/utils/ui/text'

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

/**
 * The readable name of a template: its translated name, or the slug written as words when it has none (a template a
 * plugin adds).
 *
 * @param slug - The template slug.
 * @returns The name in the active language.
 */
const nameOf = (slug: string): string => {
  return translateOrRaw(`settings.emailTemplates.names.${slug}`, humanizeSlug(slug))
}

/**
 * The readable name of a group of templates, by the same rule as {@link nameOf}.
 *
 * @param group - The group key, the first word of a slug.
 * @returns The name in the active language.
 */
const groupName = (group: string): string => {
  return translateOrRaw(`settings.emailTemplates.groups.${group}`, humanizeSlug(group))
}

/** The filter field the dialog edits: the group of the template, one query key. */
const FilterKey = { Group: 'group' } as const

/** The group filter and the search text, kept in the address. */
const { applied, draft, search, appliedSearch, activeCount, apply, reset, cancel, remove, clearAll, narrowed, chipsOf }
  = useListQuery(Object.values(FilterKey))

/** Every group the loaded templates fall in, in the order the templates come. */
const groupKeys = computed<string[]>(() => [...new Set(groups.value.map((g) => emailTemplateGroupOf(g.slug)))])

/** Group filter options; the empty value means any group. */
const groupOptions = computed(() => [
  { value: '', label: t('shared.any') },
  ...groupKeys.value.map((group) => ({ value: group, label: groupName(group) })),
])

/** The group in the address, undefined when none is set or it names no loaded group. */
const groupOfAddress = computed<string | undefined>(() => {
  const group = applied.value.group
  return groupKeys.value.includes(group) ? group : undefined
})

/** The chip of the applied group filter. */
const chips = chipsOf(() => [
  {
    key: FilterKey.Group,
    label: t('settings.emailTemplates.groupFilter'),
    value: groupOfAddress.value === undefined ? undefined : groupName(groupOfAddress.value),
  },
])

/** The templates the search and the group filter leave in. */
const shownGroups = computed<EmailTemplateGroup[]>(() => {
  const needle = appliedSearch.value.trim().toLowerCase()
  return groups.value.filter((group) => {
    if (groupOfAddress.value !== undefined && emailTemplateGroupOf(group.slug) !== groupOfAddress.value) {
      return false
    }
    return needle === ''
      || [nameOf(group.slug), group.slug, group.shown.subject].some((text) => text.toLowerCase().includes(needle))
  })
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
  return [{ label: t('shared.edit'), icon: Icon.Edit, testid: `email-templates-edit-${slug}`, onSelect: onEdit }]
}
</script>

<template>
  <UiStack>
    <UiPageHeader v-bind="schemaHeader(schema, t)" data-testid="email-templates-header">
      <template v-if="guideUrl" #actions>
        <PageHelpLink :url="guideUrl" />
      </template>
    </UiPageHeader>

    <UiListToolbar
      v-model:search="search"
      search-testid="email-templates-search"
      :search-placeholder="t('settings.emailTemplates.searchPlaceholder')"
      :active-count="activeCount"
      :chips="chips"
      :loading="store.loading && groups.length === 0"
      @apply="apply"
      @reset="reset"
      @cancel="cancel"
      @remove="remove"
      @clear="clearAll"
    >
      <UiField :label="t('settings.emailTemplates.groupFilter')">
        <template #default="{ id }">
          <UiSelect
            :id="id"
            v-model="draft.group"
            :options="groupOptions"
            :placeholder="t('shared.any')"
            data-testid="email-templates-filter-group"
          />
        </template>
      </UiField>
    </UiListToolbar>

    <UiErrorState
      v-if="store.error"
      :message="store.error"
      :loading="store.loading"
      testid="email-templates-error"
      @retry="store.fetchEmailTemplates()"
    />

    <UiCard v-else-if="narrowed && shownGroups.length === 0 && !store.loading">
      <UiEmptyState :title="t('settings.emailTemplates.noMatches')" data-testid="email-templates-no-matches" />
    </UiCard>

    <UiCard v-else>
      <UiTable
        :empty-text="t('settings.emailTemplates.noTemplatesFound')"
        :density="UiTableDensity.Comfortable"
        :loading="store.loading"
        :empty="!store.loading && groups.length === 0"
        data-testid="email-templates-table"
      >
        <UiSchemaTableHead :columns="schema.table.columns" />
        <UiTableBody divided>
          <UiTableRow
            v-for="group in shownGroups"
            :key="group.slug"
            hoverable
            :data-testid="`email-templates-row-${group.slug}`"
          >
            <UiTableCell>
              <UiStack gap="none">
                <UiText class="font-semibold text-text-primary" :data-testid="`email-templates-name-${group.slug}`">
                  {{ nameOf(group.slug) }}
                </UiText>
                <UiText tone="muted" :size="UiTextSize.Xs" class="font-mono">{{ group.slug }}</UiText>
              </UiStack>
            </UiTableCell>
            <UiTableCell muted>
              <UiStack direction="row" gap="xs" wrap align="center">
                <template v-for="(part, index) in splitSubject(group.shown.subject)" :key="index">
                  <UiBadge v-if="part.placeholder" small class="font-mono" :data-testid="`email-templates-placeholder-${group.slug}`">
                    {{ part.text }}
                  </UiBadge>
                  <UiText v-else :as="UiTextTag.Span" class="whitespace-pre-wrap">{{ part.text }}</UiText>
                </template>
              </UiStack>
            </UiTableCell>
            <UiTableCell>
              <UiStack direction="row" gap="xs">
                <UiBadge
                  v-for="version in group.versions"
                  :key="version.id"
                  small
                  :title="t(`storefront.locales.${version.language}`)"
                  :data-testid="`email-templates-row-${group.slug}-${version.language}`"
                >
                  {{ languageBadge(version.language) }}
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
