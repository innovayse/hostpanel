<script setup lang="ts">
/**
 * Hosting details card: the figures and facts the performance, security, installer, guarantee and
 * support sections of the storefront show, in four groups: Performance figures, Guarantees, Support response and Features.
 *
 * Text fields (response time, sites hosted, support response, installer app count, money back
 * days and the response time of each support channel) are drafts of the page's save bar; an empty value hides
 * that figure. The SSL, DDoS and no hidden fees switches are settings rows too. Whether a whole section shows is decided in the home sections card.
 */
import { toRef } from 'vue'
import { useI18n } from 'vue-i18n'
import { useSettingDraft } from '@/composables/useSettingDraft'
import type { SettingDraft } from '@/composables/useSettingDraft'
import SettingToggleField from '@/components/settings/SettingToggleField.vue'
import SettingsSection from '@/components/settings/SettingsSection.vue'
import SettingsSubsection from '@/components/settings/SettingsSubsection.vue'
import SettingsTextField from '@/components/settings/SettingsTextField.vue'
import { SettingEnv, SettingKey } from '@/types/setting'
import type { Setting } from '@/types/setting'
import { SHORT_TEXT_MAX_LENGTH } from '@/lib/limits'

const { t } = useI18n()

/** The most digits the money back days value takes (the API accepts 1 to 3 digits). */
const MONEY_BACK_DAYS_MAX_LENGTH = 3

/** Props for HostingDetailsCard. */
const props = defineProps<{
  /** All system settings, as loaded from the backend. */
  settings: Setting[]
}>()

/** The settings list as a ref, which is what the draft composable watches. */
const settings = toRef(props, 'settings')

/** The groups of the card, in the order they are drawn; the value is the key of the group's heading. */
enum HostingGroup {
  /** The measured figures of the performance and installer sections. */
  Performance = 'performance',
  /** The money back guarantee. */
  Guarantees = 'guarantees',
  /** How fast each support channel answers. */
  Support = 'support',
  /** The security claims. */
  Features = 'features',
}

/** One editable text field of the card. */
interface HostingField {
  /** The group the field is drawn in. */
  group: HostingGroup
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
  /** The most characters the field takes. */
  maxLength: number
  /** The field's draft state. */
  draft: SettingDraft
}

/** The editable text fields, each with its own draft. */
const FIELDS: HostingField[] = [
  {
    group: HostingGroup.Performance,
    key: SettingKey.StatsResponseTime,
    env: SettingEnv[SettingKey.StatsResponseTime],
    testid: 'stats-response-time',
    labelKey: 'settings.hostingDetails.statsResponseTime.label',
    hintKey: 'settings.hostingDetails.statsResponseTime.hint',
    maxLength: SHORT_TEXT_MAX_LENGTH,
    draft: useSettingDraft(settings, SettingKey.StatsResponseTime),
  },
  {
    group: HostingGroup.Performance,
    key: SettingKey.StatsSitesHosted,
    env: SettingEnv[SettingKey.StatsSitesHosted],
    testid: 'stats-sites-hosted',
    labelKey: 'settings.hostingDetails.statsSitesHosted.label',
    hintKey: 'settings.hostingDetails.statsSitesHosted.hint',
    maxLength: SHORT_TEXT_MAX_LENGTH,
    draft: useSettingDraft(settings, SettingKey.StatsSitesHosted),
  },
  {
    group: HostingGroup.Performance,
    key: SettingKey.StatsSupportResponse,
    env: SettingEnv[SettingKey.StatsSupportResponse],
    testid: 'stats-support-response',
    labelKey: 'settings.hostingDetails.statsSupportResponse.label',
    hintKey: 'settings.hostingDetails.statsSupportResponse.hint',
    maxLength: SHORT_TEXT_MAX_LENGTH,
    draft: useSettingDraft(settings, SettingKey.StatsSupportResponse),
  },
  {
    group: HostingGroup.Performance,
    key: SettingKey.InstallerAppCount,
    env: SettingEnv[SettingKey.InstallerAppCount],
    testid: 'installer-app-count',
    labelKey: 'settings.hostingDetails.installerAppCount.label',
    hintKey: 'settings.hostingDetails.installerAppCount.hint',
    maxLength: SHORT_TEXT_MAX_LENGTH,
    draft: useSettingDraft(settings, SettingKey.InstallerAppCount),
  },
  {
    group: HostingGroup.Guarantees,
    key: SettingKey.GuaranteeMoneyBackDays,
    env: SettingEnv[SettingKey.GuaranteeMoneyBackDays],
    testid: 'guarantee-money-back-days',
    labelKey: 'settings.hostingDetails.guaranteeMoneyBackDays.label',
    hintKey: 'settings.hostingDetails.guaranteeMoneyBackDays.hint',
    maxLength: MONEY_BACK_DAYS_MAX_LENGTH,
    draft: useSettingDraft(settings, SettingKey.GuaranteeMoneyBackDays),
  },
  {
    group: HostingGroup.Support,
    key: SettingKey.SupportResponseChat,
    env: SettingEnv[SettingKey.SupportResponseChat],
    testid: 'support-response-chat',
    labelKey: 'settings.hostingDetails.supportResponseChat.label',
    hintKey: 'settings.hostingDetails.supportResponseChat.hint',
    maxLength: SHORT_TEXT_MAX_LENGTH,
    draft: useSettingDraft(settings, SettingKey.SupportResponseChat),
  },
  {
    group: HostingGroup.Support,
    key: SettingKey.SupportResponseTicket,
    env: SettingEnv[SettingKey.SupportResponseTicket],
    testid: 'support-response-ticket',
    labelKey: 'settings.hostingDetails.supportResponseTicket.label',
    hintKey: 'settings.hostingDetails.supportResponseTicket.hint',
    maxLength: SHORT_TEXT_MAX_LENGTH,
    draft: useSettingDraft(settings, SettingKey.SupportResponseTicket),
  },
  {
    group: HostingGroup.Support,
    key: SettingKey.SupportResponseEmail,
    env: SettingEnv[SettingKey.SupportResponseEmail],
    testid: 'support-response-email',
    labelKey: 'settings.hostingDetails.supportResponseEmail.label',
    hintKey: 'settings.hostingDetails.supportResponseEmail.hint',
    maxLength: SHORT_TEXT_MAX_LENGTH,
    draft: useSettingDraft(settings, SettingKey.SupportResponseEmail),
  },
  {
    group: HostingGroup.Support,
    key: SettingKey.SupportResponsePhone,
    env: SettingEnv[SettingKey.SupportResponsePhone],
    testid: 'support-response-phone',
    labelKey: 'settings.hostingDetails.supportResponsePhone.label',
    hintKey: 'settings.hostingDetails.supportResponsePhone.hint',
    maxLength: SHORT_TEXT_MAX_LENGTH,
    draft: useSettingDraft(settings, SettingKey.SupportResponsePhone),
  },
]

/** One on/off row of the card. */
interface HostingToggle {
  /** The group the switch is drawn in. */
  group: HostingGroup
  /** Settings key the switch edits. */
  key: SettingKey
  /** Suffix of the switch's test id. */
  testid: string
  /** Locale key of the label. */
  labelKey: string
  /** Locale key of the hint. */
  hintKey: string
}

/** The switches; each row is empty until the operator turns it on, so each reads as off by default. */
const TOGGLES: HostingToggle[] = [
  { group: HostingGroup.Features, key: SettingKey.FeaturesSsl, testid: 'features-ssl', labelKey: 'settings.hostingDetails.featuresSsl.label', hintKey: 'settings.hostingDetails.featuresSsl.hint' },
  { group: HostingGroup.Features, key: SettingKey.FeaturesDdos, testid: 'features-ddos', labelKey: 'settings.hostingDetails.featuresDdos.label', hintKey: 'settings.hostingDetails.featuresDdos.hint' },
  { group: HostingGroup.Guarantees, key: SettingKey.GuaranteeNoHiddenFees, testid: 'guarantee-no-hidden-fees', labelKey: 'settings.hostingDetails.guaranteeNoHiddenFees.label', hintKey: 'settings.hostingDetails.guaranteeNoHiddenFees.hint' },
]

/** The groups in the order they are drawn. */
const GROUPS: readonly HostingGroup[] = [HostingGroup.Performance, HostingGroup.Guarantees, HostingGroup.Support, HostingGroup.Features]

/**
 * The text fields of a group.
 *
 * @param group - The group.
 * @returns Its fields in card order.
 */
const fieldsOf = (group: HostingGroup): HostingField[] => FIELDS.filter((field) => field.group === group)

/**
 * The switches of a group.
 *
 * @param group - The group.
 * @returns Its switches in card order.
 */
const togglesOf = (group: HostingGroup): HostingToggle[] => TOGGLES.filter((toggle) => toggle.group === group)
</script>

<template>
  <SettingsSection
    :columns="1"
    :title="t('settings.hostingDetails.title')"
    :description="t('settings.hostingDetails.description')"
  >
    <SettingsSubsection
      v-for="group in GROUPS"
      :key="group"
      :title="t(`settings.hostingDetails.groups.${group}`)"
      :data-testid="`hosting-group-${group}`"
    >
      <SettingsTextField
        v-for="field in fieldsOf(group)"
        :key="field.key"
        :draft="field.draft"
        :setting-key="field.key"
        :env-name="field.env"
        :label="t(field.labelKey)"
        :placeholder="t(field.hintKey)"
        :maxlength="field.maxLength"
        :testid="`hosting-${field.testid}`"
      />
      <SettingToggleField
        v-for="toggle in togglesOf(group)"
        :key="toggle.key"
        :settings="settings"
        :setting-key="toggle.key"
        :label="t(toggle.labelKey)"
        :hint="t(toggle.hintKey)"
        :default-on="false"
        :testid="`hosting-${toggle.testid}`"
      />
    </SettingsSubsection>
  </SettingsSection>
</template>
