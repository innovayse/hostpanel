<script setup lang="ts">
/**
 * The quote filter fields of a list's filter dialog: the stage and the validity, each a `UiSelect` whose first choice
 * is "any". Shared by the admin quote list and the quotes tab of a client's profile, so both offer the same filters in
 * the same words. The values are the draft the dialog edits (text, an empty string meaning "any"); they reach the
 * address only when the reader applies the dialog.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiField from '@/components/ui/UiField.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { QUOTE_STAGE_OPTIONS, QUOTE_VALIDITY_OPTIONS } from '@/lib/constants'

const { t } = useI18n()

/** Props for QuoteFilterFields. */
defineProps<{
  /** Prefix of the test ids; the fields are `<prefix>-stage` and `<prefix>-validity`. */
  testidPrefix: string
}>()

/** The stage being chosen, a quote stage or an empty string for any. */
const stage = defineModel<string>('stage', { required: true })

/** The validity being chosen, a quote validity or an empty string for any. */
const validity = defineModel<string>('validity', { required: true })

/** Stage choices; the empty value means any. */
const stageOptions = computed(() => [{ value: '', label: t('shared.any') }, ...QUOTE_STAGE_OPTIONS])

/** Validity choices; the empty value means any. */
const validityOptions = computed(() => [{ value: '', label: t('shared.any') }, ...QUOTE_VALIDITY_OPTIONS])
</script>

<template>
  <UiStack>
    <UiField :label="t('shared.stage')">
      <template #default="{ id }">
        <UiSelect
          :id="id"
          v-model="stage"
          :options="stageOptions"
          :placeholder="t('shared.any')"
          :data-testid="`${testidPrefix}-stage`"
        />
      </template>
    </UiField>
    <UiField :label="t('billing.quotesList.validityPeriod')">
      <template #default="{ id }">
        <UiSelect
          :id="id"
          v-model="validity"
          :options="validityOptions"
          :placeholder="t('shared.any')"
          :data-testid="`${testidPrefix}-validity`"
        />
      </template>
    </UiField>
  </UiStack>
</template>
