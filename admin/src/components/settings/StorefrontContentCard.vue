<script setup lang="ts">
/**
 * Public site card: the home-page figures, the hero badge and the contact details the portal shows.
 *
 * The fields are grouped under four headings: Figures (the numbers and the line in the hero), Contacts, Messengers, and
 * Address and hours (the footer and the contact page). Every field fills its column and is a draft the page's save bar
 * saves; a value that breaks its rule is flagged under the field. The site name and tagline sit in the identity
 * card above.
 */
import { toRef } from 'vue'
import { useI18n } from 'vue-i18n'
import { useLocalizedSettingDrafts, SETTING_LOCALES, SettingLanguage } from '@/composables/useLocalizedSettingDrafts'
import type { LocalizedSettingDrafts } from '@/composables/useLocalizedSettingDrafts'
import { useSettingDraft } from '@/composables/useSettingDraft'
import type { SettingDraft } from '@/composables/useSettingDraft'
import SettingNotSeeded from '@/components/settings/SettingNotSeeded.vue'
import SettingOutcome from '@/components/settings/SettingOutcome.vue'
import SettingsSection from '@/components/settings/SettingsSection.vue'
import SettingsSubsection from '@/components/settings/SettingsSubsection.vue'
import SettingsTextField from '@/components/settings/SettingsTextField.vue'
import SettingsTimezoneField from '@/components/settings/SettingsTimezoneField.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiLocalizedField from '@/components/ui/UiLocalizedField.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { SettingEnv, SettingKey } from '@/types/setting'
import type { Setting } from '@/types/setting'
import { SHORT_TEXT_MAX_LENGTH } from '@/lib/limits'
import { isValidSettingValue, SettingFieldRule } from '@/lib/settingValidation'
import { UiInputType } from '@/utils/ui/input'

const { t } = useI18n()

/** Props for StorefrontContentCard. */
const props = defineProps<{
  /** All system settings, as loaded from the backend. */
  settings: Setting[]
}>()

/** The settings list as a ref, which is what the draft composable watches. */
const settings = toRef(props, 'settings')

/** The groups of the card, in the order they are drawn; the value is the key of the group's heading. */
enum StorefrontGroup {
  /** The numbers and the line in the hero. */
  Figures = 'figures',
  /** Where visitors write or call. */
  Contacts = 'contacts',
  /** Messenger handles. */
  Messengers = 'messengers',
  /** The office address, its map, the working hours and their time zone. */
  Address = 'address',
}

/** One editable field of the card. */
interface StorefrontField {
  /** The group the field is drawn in. */
  group: StorefrontGroup
  /** Settings key the field edits. */
  key: SettingKey
  /** Environment variable the portal falls back to until the row exists. */
  env: string
  /** Suffix of the field's test ids. */
  testid: string
  /** Locale key of the label. */
  labelKey: string
  /** Locale key of the placeholder. */
  hintKey: string
  /** The field's draft state. */
  draft: SettingDraft
  /** The shape the value must have, when it has one; the API enforces the same shape. */
  rule?: SettingFieldRule
  /** Locale key of the sentence shown when the value breaks the rule. */
  invalidKey?: string
}

/** The editable fields, each with its own draft. */
const FIELDS: StorefrontField[] = [
  {
    group: StorefrontGroup.Figures,
    key: SettingKey.StatsClients,
    env: SettingEnv[SettingKey.StatsClients],
    testid: 'stats-clients',
    labelKey: 'settings.storefrontContent.statsClients.label',
    hintKey: 'settings.storefrontContent.statsClients.hint',
    draft: useSettingDraft(settings, SettingKey.StatsClients),
  },
  {
    group: StorefrontGroup.Figures,
    key: SettingKey.StatsUptime,
    env: SettingEnv[SettingKey.StatsUptime],
    testid: 'stats-uptime',
    labelKey: 'settings.storefrontContent.statsUptime.label',
    hintKey: 'settings.storefrontContent.statsUptime.hint',
    draft: useSettingDraft(settings, SettingKey.StatsUptime),
  },
  {
    group: StorefrontGroup.Figures,
    key: SettingKey.StatsRegions,
    env: SettingEnv[SettingKey.StatsRegions],
    testid: 'stats-regions',
    labelKey: 'settings.storefrontContent.statsRegions.label',
    hintKey: 'settings.storefrontContent.statsRegions.hint',
    draft: useSettingDraft(settings, SettingKey.StatsRegions),
  },
  {
    group: StorefrontGroup.Figures,
    key: SettingKey.HeroBadge,
    env: SettingEnv[SettingKey.HeroBadge],
    testid: 'hero-badge',
    labelKey: 'settings.storefrontContent.heroBadge.label',
    hintKey: 'settings.storefrontContent.heroBadge.hint',
    draft: useSettingDraft(settings, SettingKey.HeroBadge),
  },
  {
    group: StorefrontGroup.Contacts,
    key: SettingKey.ContactEmail,
    env: SettingEnv[SettingKey.ContactEmail],
    testid: 'contact-email',
    labelKey: 'settings.storefrontContent.contactEmail.label',
    hintKey: 'settings.storefrontContent.contactEmail.hint',
    draft: useSettingDraft(settings, SettingKey.ContactEmail),
    rule: SettingFieldRule.Email,
    invalidKey: 'settings.storefrontContent.contactEmail.invalid',
  },
  {
    group: StorefrontGroup.Contacts,
    key: SettingKey.ContactPhone,
    env: SettingEnv[SettingKey.ContactPhone],
    testid: 'contact-phone',
    labelKey: 'settings.storefrontContent.contactPhone.label',
    hintKey: 'settings.storefrontContent.contactPhone.hint',
    draft: useSettingDraft(settings, SettingKey.ContactPhone),
  },
  {
    group: StorefrontGroup.Messengers,
    key: SettingKey.ContactWhatsapp,
    env: SettingEnv[SettingKey.ContactWhatsapp],
    testid: 'contact-whatsapp',
    labelKey: 'settings.storefrontContent.contactWhatsapp.label',
    hintKey: 'settings.storefrontContent.contactWhatsapp.hint',
    draft: useSettingDraft(settings, SettingKey.ContactWhatsapp),
  },
  {
    group: StorefrontGroup.Messengers,
    key: SettingKey.ContactTelegram,
    env: SettingEnv[SettingKey.ContactTelegram],
    testid: 'contact-telegram',
    labelKey: 'settings.storefrontContent.contactTelegram.label',
    hintKey: 'settings.storefrontContent.contactTelegram.hint',
    draft: useSettingDraft(settings, SettingKey.ContactTelegram),
  },
  {
    group: StorefrontGroup.Address,
    key: SettingKey.ContactMapUrl,
    env: SettingEnv[SettingKey.ContactMapUrl],
    testid: 'contact-map-url',
    labelKey: 'settings.storefrontContent.contactMapUrl.label',
    hintKey: 'settings.storefrontContent.contactMapUrl.hint',
    draft: useSettingDraft(settings, SettingKey.ContactMapUrl),
    rule: SettingFieldRule.HttpsUrl,
    invalidKey: 'settings.storefrontContent.contactMapUrl.invalid',
  },
]

/** The time zone the working hours are stated in: a list pick, not text, so it has its own field. */
const TIMEZONE = useSettingDraft(settings, SettingKey.ContactTimezone)

/** One contact detail written once per language, shown as a single field with a tab per language. */
interface LocalizedStorefrontField {
  /** The group the field is drawn in. */
  group: StorefrontGroup
  /** Suffix of the test ids: `<testid>` is the English input, `<testid>-ru` and `<testid>-hy` the others. */
  testid: string
  /** The settings row of each language. */
  keys: Record<SettingLanguage, SettingKey>
  /** Locale key of the label. */
  labelKey: string
  /** Locale key of the placeholder. */
  hintKey: string
  /** The drafts of the rows and what the tabs mark. */
  localized: LocalizedSettingDrafts
}

/**
 * Builds the config of one per-language field.
 *
 * @param group - The group the field is drawn in.
 * @param testid - Suffix of the test ids.
 * @param labelKey - Locale key of the label.
 * @param hintKey - Locale key of the placeholder.
 * @param keys - The settings row of each language.
 * @returns The field with its drafts.
 */
const localizedField = (
  group: StorefrontGroup,
  testid: string,
  labelKey: string,
  hintKey: string,
  keys: Record<SettingLanguage, SettingKey>,
): LocalizedStorefrontField => ({ group, testid, keys, labelKey, hintKey, localized: useLocalizedSettingDrafts(settings, keys) })

/** The contact details written per language: an empty language shows the English text on the storefront. */
const LOCALIZED_FIELDS: LocalizedStorefrontField[] = [
  localizedField(StorefrontGroup.Address, 'contact-address', 'settings.storefrontContent.contactAddress.label', 'settings.storefrontContent.contactAddress.hint', {
    [SettingLanguage.En]: SettingKey.ContactAddress,
    [SettingLanguage.Ru]: SettingKey.ContactAddressRu,
    [SettingLanguage.Hy]: SettingKey.ContactAddressHy,
  }),
  localizedField(StorefrontGroup.Address, 'contact-hours', 'settings.storefrontContent.contactHours.label', 'settings.storefrontContent.contactHours.hint', {
    [SettingLanguage.En]: SettingKey.ContactHours,
    [SettingLanguage.Ru]: SettingKey.ContactHoursRu,
    [SettingLanguage.Hy]: SettingKey.ContactHoursHy,
  }),
]

/**
 * Test id of the input of one language of a per-language field; English keeps the bare suffix.
 *
 * @param field - The per-language field.
 * @param locale - The language of the input.
 * @returns The `data-testid` of the input.
 */
const localizedInputTestid = (field: LocalizedStorefrontField, locale: SettingLanguage): string =>
  locale === SettingLanguage.En ? `storefront-${field.testid}` : `storefront-${field.testid}-${locale}`

/**
 * The sentence under a field whose current text breaks its rule.
 *
 * @param field - The field to check.
 * @returns The refusal sentence; undefined when the field has no rule, the text satisfies it or it is empty.
 */
const invalidMessageOf = (field: StorefrontField): string | undefined =>
  field.rule !== undefined && field.invalidKey !== undefined && !isValidSettingValue(field.rule, field.draft.value.value)
    ? t(field.invalidKey)
    : undefined

/** The groups in the order they are drawn, each with its heading key. */
const GROUPS: readonly StorefrontGroup[] = [StorefrontGroup.Figures, StorefrontGroup.Contacts, StorefrontGroup.Messengers, StorefrontGroup.Address]

/**
 * The plain text fields of a group.
 *
 * @param group - The group.
 * @returns Its fields in card order.
 */
const fieldsOf = (group: StorefrontGroup): StorefrontField[] => FIELDS.filter((field) => field.group === group)

/**
 * The per-language fields of a group.
 *
 * @param group - The group.
 * @returns Its fields in card order.
 */
const localizedOf = (group: StorefrontGroup): LocalizedStorefrontField[] => LOCALIZED_FIELDS.filter((field) => field.group === group)
</script>

<template>
  <SettingsSection
    :columns="1"
    :title="t('settings.storefrontContent.title')"
    :description="t('settings.storefrontContent.description')"
  >
    <SettingsSubsection
      v-for="group in GROUPS"
      :key="group"
      :title="t(`settings.storefrontContent.groups.${group}`)"
      :data-testid="`storefront-group-${group}`"
    >
      <UiLocalizedField
        v-for="field in localizedOf(group)"
        :key="field.testid"
        :label="t(field.labelKey)"
        :hint="t('settings.storefrontContent.localeFallbackHint')"
        :locales="SETTING_LOCALES"
        :filled="field.localized.filled.value"
        :errors="field.localized.errors.value"
        :testid="`storefront-${field.testid}-tab`"
      >
        <template #default="{ locale }">
          <SettingNotSeeded
            v-if="!field.localized.drafts[locale].setting.value"
            :setting-key="field.keys[locale]"
            :env-name="SettingEnv[field.keys[locale]]"
          />
          <UiStack v-else gap="xs">
            <UiInput
              v-model="field.localized.drafts[locale].value.value"
              :placeholder="t(field.hintKey)"
              :maxlength="SHORT_TEXT_MAX_LENGTH"
              :data-testid="localizedInputTestid(field, locale)"
            />
            <SettingOutcome :outcome="field.localized.drafts[locale].outcome.value?.saved ? field.localized.drafts[locale].outcome.value : null" />
          </UiStack>
        </template>
      </UiLocalizedField>

      <SettingsTextField
        v-for="field in fieldsOf(group)"
        :key="field.key"
        :draft="field.draft"
        :setting-key="field.key"
        :env-name="field.env"
        :type="field.rule === SettingFieldRule.Email ? UiInputType.Email : UiInputType.Text"
        :label="t(field.labelKey)"
        :placeholder="t(field.hintKey)"
        :maxlength="SHORT_TEXT_MAX_LENGTH"
        :invalid-message="invalidMessageOf(field)"
        :testid="`storefront-${field.testid}`"
      />

      <SettingsTimezoneField
        v-if="group === StorefrontGroup.Address"
        :draft="TIMEZONE"
        :setting-key="SettingKey.ContactTimezone"
        :env-name="SettingEnv[SettingKey.ContactTimezone]"
        :label="t('settings.storefrontContent.contactTimezone.label')"
        :hint="t('settings.storefrontContent.contactTimezone.hint')"
        testid="storefront-contact-timezone"
      />
    </SettingsSubsection>
  </SettingsSection>
</template>
