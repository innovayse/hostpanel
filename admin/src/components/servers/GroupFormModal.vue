<script setup lang="ts">
/**
 * Dialog for creating or editing a server group.
 *
 * Displays a dual-list selector for assigning servers. Emits `saved` with the payload on submit,
 * and `close` on cancel. Validation belongs to the server: a refusal comes back through the
 * `error` and `fieldErrors` props as the sentences the API returned.
 */
import { ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { FILL_TYPE_LABELS, ServerFillType } from '@/types/server'
import type { ServerGroupDto, ServerGroupPayload, ServerOptionDto } from '@/types/server'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiField from '@/components/ui/UiField.vue'
import UiForm from '@/components/ui/UiForm.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiLabel from '@/components/ui/UiLabel.vue'
import UiRadio from '@/components/ui/UiRadio.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { Icon } from '@/lib/icons'
import { UiSize } from '@/utils/ui/size'
import { UiActionKind } from '@/utils/ui/actions'
import { UiButtonType, UiButtonVariant } from '@/utils/ui/button'
import { UiCardPadding } from '@/utils/ui/card'
import { UiDialogSize } from '@/utils/ui/dialog'
import { UiInputType } from '@/utils/ui/input'
import { UiGap } from '@/utils/ui/layout'
import { UiLabelVariant, UiTextSize, UiTextTone } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'

/** Translation function. */
const { t } = useI18n()

/** Props for GroupFormModal. */
const props = withDefaults(defineProps<{
  /** Group to edit, or null when creating. */
  group: ServerGroupDto | null
  /** All available servers to choose from. */
  availableServers: ServerOptionDto[]
  /** True while the save request is in flight. */
  saving: boolean
  /** Sentence for the last refused save, null when none. */
  error?: string | null
  /** Per-field sentences of the last refused save, keyed by camelCase field name. */
  fieldErrors?: Record<string, string>
}>(), {
  error: null,
  /**
   * Default for the field errors.
   *
   * @returns A new empty map, so instances never share one.
   */
  fieldErrors: () => ({}),
})

/** Events emitted by GroupFormModal. */
const emit = defineEmits<{
  /** Emitted when the user submits the form. */
  saved: [payload: ServerGroupPayload]
  /** Emitted when the user closes/cancels the dialog. */
  close: []
}>()

/** Fill strategies offered, in display order. */
const fillTypes: ServerFillType[] = [ServerFillType.LeastFull, ServerFillType.FillUntilFull]

/** Group name field. */
const name = ref('')

/** Chosen fill strategy. */
const fillType = ref<ServerFillType>(ServerFillType.LeastFull)

/** Ids of the servers assigned to the group. */
const selectedIds = ref<number[]>([])

// Loads the edited group into the form, or resets it when creating.
watch(() => props.group, (g) => {
  if (g) {
    name.value = g.name
    fillType.value = g.fillType
    selectedIds.value = g.servers.map(s => s.id)
  } else {
    name.value = ''
    fillType.value = ServerFillType.LeastFull
    selectedIds.value = []
  }
}, { immediate: true })

/**
 * Servers NOT yet in the group.
 *
 * @returns The available servers whose id is not selected.
 */
const unselected = (): ServerOptionDto[] => props.availableServers.filter(s => !selectedIds.value.includes(s.id))

/**
 * Servers in the group.
 *
 * @returns The available servers whose id is selected.
 */
const selected = (): ServerOptionDto[] => props.availableServers.filter(s => selectedIds.value.includes(s.id))

/**
 * Toggles a server in/out of the group.
 *
 * @param id - Id of the server to move.
 */
const toggle = (id: number): void => {
  if (selectedIds.value.includes(id)) {
    selectedIds.value = selectedIds.value.filter(x => x !== id)
  } else {
    selectedIds.value = [...selectedIds.value, id]
  }
}

/** Emits the save payload. */
const handleSubmit = (): void => {
  emit('saved', { name: name.value, fillType: fillType.value, serverIds: selectedIds.value })
}
</script>

<template>
  <UiDialog
    :title="props.group ? t('servers.groupForm.editGroup') : t('servers.groupForm.createNewGroup')"
    :size="UiDialogSize.Lg"
    @close="emit('close')"
  >
    <UiForm stacked @submit.prevent="handleSubmit">
      <UiAlert v-if="props.error" :variant="UiTone.Danger" data-testid="group-form-error">{{ props.error }}</UiAlert>

      <!-- Name -->
      <UiField :label="t('servers.groupForm.groupName')" :error="props.fieldErrors.name" required>
        <template #default="{ id, invalid, describedby }">
          <UiInput
            :id="id"
            v-model="name"
            :type="UiInputType.Text"
            :invalid="invalid"
            :aria-describedby="describedby"
            :placeholder="t('servers.groupForm.eGWebServers')"
            data-testid="group-form-name"
          />
        </template>
      </UiField>

      <!-- Fill type -->
      <UiStack :gap="UiGap.Sm">
        <UiLabel :variant="UiLabelVariant.Field">{{ t('servers.groupForm.fillType') }}</UiLabel>
        <UiLabel v-for="ft in fillTypes" :key="ft" :variant="UiLabelVariant.Inline">
          <UiRadio v-model="fillType" :value="ft" :data-testid="`group-form-fill-${ft}`" />
          {{ FILL_TYPE_LABELS[ft] }}
        </UiLabel>
      </UiStack>

      <!-- Server selector -->
      <UiStack :gap="UiGap.Sm">
        <UiLabel :variant="UiLabelVariant.Field">{{ t('servers.groupForm.servers') }}</UiLabel>
        <UiGrid :columns="2" :gap="UiGap.Sm" container>
          <UiStack :gap="UiGap.Xs">
            <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Xs">{{ t('servers.groupForm.available') }}</UiText>
            <UiCard :padding="UiCardPadding.Sm" class="min-h-[120px] max-h-[160px] overflow-y-auto" data-testid="group-form-available">
              <UiStack :gap="UiGap.Xs">
                <UiButton
                  v-for="s in unselected()"
                  :key="s.id"
                  :type="UiButtonType.Button"
                  :variant="UiButtonVariant.Ghost"
                  :size="UiSize.Sm"
                  :data-testid="`group-form-add-${s.id}`"
                  @click="toggle(s.id)"
                >
                  {{ s.name }} +
                </UiButton>
                <UiText v-if="unselected().length === 0" :tone="UiTextTone.Muted" :size="UiTextSize.Sm">
                  {{ t('servers.groupForm.allAssigned') }}
                </UiText>
              </UiStack>
            </UiCard>
          </UiStack>

          <UiStack :gap="UiGap.Xs">
            <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Xs">{{ t('servers.groupForm.inGroup') }}</UiText>
            <UiCard :padding="UiCardPadding.Sm" class="min-h-[120px] max-h-[160px] overflow-y-auto" data-testid="group-form-selected">
              <UiStack :gap="UiGap.Xs">
                <UiButton
                  v-for="s in selected()"
                  :key="s.id"
                  :type="UiButtonType.Button"
                  :variant="UiButtonVariant.Ghost"
                  :size="UiSize.Sm"
                  :data-testid="`group-form-remove-${s.id}`"
                  @click="toggle(s.id)"
                >
                  {{ s.name }}
                  <UiIcon :markup="Icon.Close" :size="UiSize.Xs" />
                </UiButton>
                <UiText v-if="selected().length === 0" :tone="UiTextTone.Muted" :size="UiTextSize.Sm">
                  {{ t('servers.groupForm.noneSelected') }}
                </UiText>
              </UiStack>
            </UiCard>
          </UiStack>
        </UiGrid>
      </UiStack>
    </UiForm>

    <template #footer>
      <UiButton :action="UiActionKind.Cancel" :type="UiButtonType.Button" :variant="UiButtonVariant.Secondary" data-testid="group-form-cancel" @click="emit('close')">
        {{ t('shared.cancel') }}
      </UiButton>
      <UiButton :action="UiActionKind.Save"
        :type="UiButtonType.Button"
        :variant="UiButtonVariant.Primary"
        :loading="props.saving"
        data-testid="group-form-save"
        @click="handleSubmit"
      >
        {{ props.group ? t('shared.save') : t('servers.servers.createGroup') }}
      </UiButton>
    </template>
  </UiDialog>
</template>
