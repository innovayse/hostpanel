<script setup lang="ts">
/**
 * Storefront card: the home-page figures, the hero badge and the contact details the portal shows.
 *
 * The figures (clients, uptime, regions) and the badge are the numbers and the line in the hero;
 * the contact details feed the footer and the contact page. Every field is a draft the page's save bar
 * saves; a value that breaks its rule is flagged under the field. The site name and tagline sit in the identity
 * card above.
 */
import { toRef } from 'vue'
import { useI18n } from 'vue-i18n'
import { useSettingDraft } from '@/composables/useSettingDraft'
import type { SettingDraft } from '@/composables/useSettingDraft'
import SettingNotSeeded from '@/components/settings/SettingNotSeeded.vue'
import SettingOutcome from '@/components/settings/SettingOutcome.vue'
import SettingsSection from '@/components/settings/SettingsSection.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiText from '@/components/ui/UiText.vue'
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

/** One editable field of the card. */
interface StorefrontField {
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
    key: SettingKey.StatsClients,
    env: SettingEnv[SettingKey.StatsClients],
    testid: 'stats-clients',
    labelKey: 'settings.storefrontContent.statsClients.label',
    hintKey: 'settings.storefrontContent.statsClients.hint',
    draft: useSettingDraft(settings, SettingKey.StatsClients),
  },
  {
    key: SettingKey.StatsUptime,
    env: SettingEnv[SettingKey.StatsUptime],
    testid: 'stats-uptime',
    labelKey: 'settings.storefrontContent.statsUptime.label',
    hintKey: 'settings.storefrontContent.statsUptime.hint',
    draft: useSettingDraft(settings, SettingKey.StatsUptime),
  },
  {
    key: SettingKey.StatsRegions,
    env: SettingEnv[SettingKey.StatsRegions],
    testid: 'stats-regions',
    labelKey: 'settings.storefrontContent.statsRegions.label',
    hintKey: 'settings.storefrontContent.statsRegions.hint',
    draft: useSettingDraft(settings, SettingKey.StatsRegions),
  },
  {
    key: SettingKey.HeroBadge,
    env: SettingEnv[SettingKey.HeroBadge],
    testid: 'hero-badge',
    labelKey: 'settings.storefrontContent.heroBadge.label',
    hintKey: 'settings.storefrontContent.heroBadge.hint',
    draft: useSettingDraft(settings, SettingKey.HeroBadge),
  },
  {
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
    key: SettingKey.ContactPhone,
    env: SettingEnv[SettingKey.ContactPhone],
    testid: 'contact-phone',
    labelKey: 'settings.storefrontContent.contactPhone.label',
    hintKey: 'settings.storefrontContent.contactPhone.hint',
    draft: useSettingDraft(settings, SettingKey.ContactPhone),
  },
  {
    key: SettingKey.ContactWhatsapp,
    env: SettingEnv[SettingKey.ContactWhatsapp],
    testid: 'contact-whatsapp',
    labelKey: 'settings.storefrontContent.contactWhatsapp.label',
    hintKey: 'settings.storefrontContent.contactWhatsapp.hint',
    draft: useSettingDraft(settings, SettingKey.ContactWhatsapp),
  },
  {
    key: SettingKey.ContactTelegram,
    env: SettingEnv[SettingKey.ContactTelegram],
    testid: 'contact-telegram',
    labelKey: 'settings.storefrontContent.contactTelegram.label',
    hintKey: 'settings.storefrontContent.contactTelegram.hint',
    draft: useSettingDraft(settings, SettingKey.ContactTelegram),
  },
  {
    key: SettingKey.ContactAddress,
    env: SettingEnv[SettingKey.ContactAddress],
    testid: 'contact-address',
    labelKey: 'settings.storefrontContent.contactAddress.label',
    hintKey: 'settings.storefrontContent.contactAddress.hint',
    draft: useSettingDraft(settings, SettingKey.ContactAddress),
  },
  {
    key: SettingKey.ContactAddressRu,
    env: SettingEnv[SettingKey.ContactAddressRu],
    testid: 'contact-address-ru',
    labelKey: 'settings.storefrontContent.contactAddressRu.label',
    hintKey: 'settings.storefrontContent.contactAddressRu.hint',
    draft: useSettingDraft(settings, SettingKey.ContactAddressRu),
  },
  {
    key: SettingKey.ContactAddressHy,
    env: SettingEnv[SettingKey.ContactAddressHy],
    testid: 'contact-address-hy',
    labelKey: 'settings.storefrontContent.contactAddressHy.label',
    hintKey: 'settings.storefrontContent.contactAddressHy.hint',
    draft: useSettingDraft(settings, SettingKey.ContactAddressHy),
  },
  {
    key: SettingKey.ContactMapUrl,
    env: SettingEnv[SettingKey.ContactMapUrl],
    testid: 'contact-map-url',
    labelKey: 'settings.storefrontContent.contactMapUrl.label',
    hintKey: 'settings.storefrontContent.contactMapUrl.hint',
    draft: useSettingDraft(settings, SettingKey.ContactMapUrl),
    rule: SettingFieldRule.HttpsUrl,
    invalidKey: 'settings.storefrontContent.contactMapUrl.invalid',
  },
  {
    key: SettingKey.ContactHours,
    env: SettingEnv[SettingKey.ContactHours],
    testid: 'contact-hours',
    labelKey: 'settings.storefrontContent.contactHours.label',
    hintKey: 'settings.storefrontContent.contactHours.hint',
    draft: useSettingDraft(settings, SettingKey.ContactHours),
  },
  {
    key: SettingKey.ContactHoursRu,
    env: SettingEnv[SettingKey.ContactHoursRu],
    testid: 'contact-hours-ru',
    labelKey: 'settings.storefrontContent.contactHoursRu.label',
    hintKey: 'settings.storefrontContent.contactHoursRu.hint',
    draft: useSettingDraft(settings, SettingKey.ContactHoursRu),
  },
  {
    key: SettingKey.ContactHoursHy,
    env: SettingEnv[SettingKey.ContactHoursHy],
    testid: 'contact-hours-hy',
    labelKey: 'settings.storefrontContent.contactHoursHy.label',
    hintKey: 'settings.storefrontContent.contactHoursHy.hint',
    draft: useSettingDraft(settings, SettingKey.ContactHoursHy),
  },
  {
    key: SettingKey.ContactTimezone,
    env: SettingEnv[SettingKey.ContactTimezone],
    testid: 'contact-timezone',
    labelKey: 'settings.storefrontContent.contactTimezone.label',
    hintKey: 'settings.storefrontContent.contactTimezone.hint',
    draft: useSettingDraft(settings, SettingKey.ContactTimezone),
  },
]

/**
 * Whether a field's current text breaks its rule.
 *
 * @param field - The field to check.
 * @returns True when the field has a rule and the text does not satisfy it.
 */
const isInvalid = (field: StorefrontField): boolean =>
  field.rule !== undefined && !isValidSettingValue(field.rule, field.draft.value.value)

</script>

<template>
  <SettingsSection
    :columns="2"
    :title="t('settings.storefrontContent.title')"
    :description="t('settings.storefrontContent.description')"
  >
    <template v-for="field in FIELDS" :key="field.key">
      <SettingNotSeeded v-if="!field.draft.setting.value" :setting-key="field.key" :env-name="field.env" />
      <UiStack v-else gap="xs">
        <UiStack direction="row" align="end" wrap>
          <UiInput
            v-model="field.draft.value.value"
            :type="field.rule === SettingFieldRule.Email ? UiInputType.Email : UiInputType.Text"
            :label="t(field.labelKey)"
            :placeholder="t(field.hintKey)"
            :maxlength="SHORT_TEXT_MAX_LENGTH"
            :invalid="isInvalid(field)"
            :data-testid="`storefront-${field.testid}`"
            class="min-w-72 flex-1"
          />
          <SettingOutcome :outcome="field.draft.outcome.value" />
        </UiStack>
        <UiText
          v-if="isInvalid(field) && field.invalidKey"
          tone="danger"
          size="sm"
          :data-testid="`storefront-${field.testid}-invalid`"
        >
          {{ t(field.invalidKey) }}
        </UiText>
      </UiStack>
    </template>
  </SettingsSection>
</template>
