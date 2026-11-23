<script setup lang="ts">
/**
 * The time zone field of a settings card: a searchable list of IANA zones, grouped by region, each shown as its city
 * and current offset with the zone's own name in the interface language beneath.
 *
 * The stored value is the IANA id. A row that still holds the old free-text `UTC+4` shows as the fixed-offset zone it
 * means (`UTC+04:00`) and is rewritten as an id only when the operator picks a zone; a value the list does not know is
 * shown as it is. Empty is a valid choice ("none"), since the zone is only shown beside working hours.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import SettingNotSeeded from '@/components/settings/SettingNotSeeded.vue'
import SettingOutcome from '@/components/settings/SettingOutcome.vue'
import UiField from '@/components/ui/UiField.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import type { SettingDraft } from '@/composables/useSettingDraft'
import { isKnownTimezone, timezoneIdOf, timezoneLabel, timezoneOptions } from '@/lib/timezones'
import { UiGap } from '@/utils/ui/layout'
import type { UiSelectOption } from '@/utils/ui/select'

const { t, locale } = useI18n()

/** Props for SettingsTimezoneField. */
const props = defineProps<{
  /** The field's draft state. */
  draft: SettingDraft
  /** Settings key the field edits, named in the warning when the row is missing. */
  settingKey: string
  /** Environment variable the portal falls back to until the row exists. */
  envName: string
  /** Caption of the field (already translated). */
  label: string
  /** Help line under the field (already translated). */
  hint: string
  /** `data-testid` of the select. */
  testid: string
}>()

/** The draft's text, which the field edits in place; read through a computed so a replaced draft is followed. */
const text = computed(() => props.draft.value)

/** The zone the value stands for: an IANA id, or the fixed-offset zone of a legacy `UTC+4`. */
const zoneId = computed<string>(() => timezoneIdOf(text.value.value))

/** The "none" choice followed by every zone, in the interface language. */
const options = computed<readonly UiSelectOption<string>[]>(() => [
  { value: '', label: t('settings.timezone.none'), muted: true },
  ...timezoneOptions(locale.value),
])

/** Label of a value the list does not carry (a fixed-offset zone, or text that is no zone at all); undefined when the list has it. */
const valueLabel = computed<string | undefined>(() => {
  if (zoneId.value === '' || options.value.some((option) => option.value === zoneId.value)) {
    return undefined
  }
  return isKnownTimezone(zoneId.value) ? timezoneLabel(zoneId.value) : zoneId.value
})

/** The select's model; a pick is written into the draft as the IANA id. */
const model = computed<string>({
  /**
   * The zone the select shows.
   *
   * @returns The IANA id the draft stands for.
   */
  get: () => zoneId.value,
  /**
   * Records the picked zone in the draft.
   *
   * @param next - The IANA id picked, or empty for none.
   */
  set: (next: string) => {
    text.value.value = next
  },
})
</script>

<template>
  <SettingNotSeeded v-if="!draft.setting.value" :setting-key="settingKey" :env-name="envName" />
  <UiStack v-else :gap="UiGap.Xs">
    <UiField :label="label" :hint="hint">
      <template #default="{ id }">
        <UiSelect
          :id="id"
          v-model="model"
          :options="options"
          :value-label="valueLabel"
          :placeholder="t('settings.timezone.placeholder')"
          searchable
          wide
          :data-testid="testid"
        />
      </template>
    </UiField>
    <SettingOutcome :outcome="draft.outcome.value" />
  </UiStack>
</template>
