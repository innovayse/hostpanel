<script setup lang="ts">
/**
 * Dialog for editing one product's specification: the lines the storefront's plan comparison
 * table is built from.
 *
 * Each line is saved on its own, matching the API: there is no bulk endpoint, and a failure
 * part-way through a list of edits should leave the lines that did save rather than roll the whole
 * dialog back to an unknown state.
 *
 * Rows line up across plans by label, so the label of an existing line is what an operator has to
 * keep consistent between products. The hint below the list says so, because nothing in the UI
 * would otherwise reveal it.
 *
 * A refused load shows the API's sentence with a retry; the outcome of an add, save or remove
 * (success, or the API's sentence) shows above the list; removing a line is confirmed first.
 */
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import UiButton from '@/components/ui/UiButton.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import { useResultBanner } from '@/composables/useResultBanner'
import { UiDialogVariant } from '@/utils/ui/dialog'
import { UiTone } from '@/utils/ui/tone'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { Icon } from '@/lib/icons'
import { useProductFeaturesStore } from '@/stores/productFeatures'
import type { Product } from '@/types/product'
import type { ProductFeature } from '@/types/productfeature'

const { t } = useI18n()

/** Props for ProductFeaturesModal. */
const props = defineProps<{
  /** Product whose specification is being edited. */
  product: Product
}>()

const emit = defineEmits<{
  /** Emitted when the user closes the dialog. */
  close: []
}>()

/** Product features store: the lines, and the refusal sentence of the last call. */
const store = useProductFeaturesStore()

/** The product's lines, as last loaded. */
const features = ref<ProductFeature[]>([])

/** Number of placeholder rows drawn while the lines load. */
const SKELETON_ROWS = 3

/** True while the lines are loading. */
const loading = ref(true)

/** Id of the line being saved or removed, null when idle. */
const busyId = ref<number | null>(null)

/** Label of the line about to be added. */
const newLabel = ref('')

/** Value of the line about to be added. */
const newValue = ref('')

/** True while a new line is being added. */
const adding = ref(false)

/** Whether the last load of the lines was refused. */
const loadFailed = ref(false)

/** The line waiting for the removal confirmation, null when the prompt is closed. */
const removeTarget = ref<ProductFeature | null>(null)

/** The outcome line of the last add, save or remove. */
const feedback = useResultBanner()

/** Tone of the outcome line: danger for a refusal, success otherwise. */
const feedbackTone = computed<UiTone>(() => (feedback.isError.value ? UiTone.Danger : UiTone.Success))

/**
 * Shows the outcome of a change: the success sentence, or the API's refusal.
 *
 * @param ok - Whether the API accepted the change.
 * @param successKey - Locale key of the success sentence.
 */
const report = (ok: boolean, successKey: string): void => {
  if (ok) {
    feedback.show(t(successKey), false)
  } else {
    feedback.show(store.error ?? t('errors.noAnswer'), true)
  }
}

/** Loads the product's lines, newest state from the server. */
const load = async (): Promise<void> => {
  loading.value = true
  const lines = await store.fetchFeatures(props.product.id)
  loadFailed.value = lines === null
  if (lines) {
    features.value = lines
  }
  loading.value = false
}

onMounted(load)

/** Adds a line at the end of the list. */
const add = async (): Promise<void> => {
  if (!newLabel.value.trim() || !newValue.value.trim()) {
    return
  }
  feedback.clear()
  adding.value = true
  const added = await store.addFeature(props.product.id, newLabel.value.trim(), newValue.value.trim(), features.value.length)
  report(added, 'shared.recordCreated')
  if (added) {
    newLabel.value = ''
    newValue.value = ''
    await load()
  }
  adding.value = false
}

/**
 * Saves an edited line.
 *
 * @param feature - The line to save, with the operator's current text.
 */
const save = async (feature: ProductFeature): Promise<void> => {
  if (!feature.label.trim() || !feature.value.trim()) {
    return
  }
  feedback.clear()
  busyId.value = feature.id
  const saved = await store.saveFeature(feature)
  report(saved, 'shared.changesSavedSuccessfully')
  if (!saved) {
    await load()
  }
  busyId.value = null
}

/** Removes the line the operator confirmed and shows the outcome. */
const remove = async (): Promise<void> => {
  const feature = removeTarget.value
  if (feature === null) {
    return
  }
  feedback.clear()
  busyId.value = feature.id
  const removed = await store.removeFeature(feature.id)
  busyId.value = null
  removeTarget.value = null
  report(removed, 'shared.recordDeleted')
  if (removed) {
    await load()
  }
}

/** Closes the removal confirmation; ignored while the removal runs. */
const dismissRemove = (): void => {
  if (busyId.value === null) {
    removeTarget.value = null
  }
}
</script>

<template>
  <UiDialog :title="t('settings.productFeatures.specification')" size="lg" @close="emit('close')">
    <UiText tone="secondary" size="md">
      {{ t('settings.productFeatures.shownInTheStorefrontS') }} {{ product.name }}.
    </UiText>

    <UiStatusMessage :message="feedback.message.value" :tone="feedbackTone" data-testid="product-features-error" />

    <UiErrorState
      v-if="loadFailed && !loading"
      :message="store.error ?? t('errors.noAnswer')"
      testid="product-features-load-error"
      @retry="load"
    />

    <template v-else>
      <!-- Placeholder rows with the shape of the real lines while they load -->
      <UiStack v-if="loading" gap="sm" aria-busy="true" data-testid="product-features-skeleton">
        <UiStack v-for="row in SKELETON_ROWS" :key="row" direction="row" align="center" gap="sm" aria-hidden="true">
          <UiSkeletonBar class="h-8 w-1/3 rounded-control" />
          <UiSkeletonBar class="h-8 flex-1 rounded-control" />
          <UiSkeletonBar class="h-8 w-16 rounded-control" />
          <UiSkeletonBar class="size-8 rounded-control" />
        </UiStack>
      </UiStack>

      <UiEmptyState
        v-else-if="features.length === 0"
        :title="t('settings.productFeatures.nothingSpecifiedYetTheComparison')"
        data-testid="product-features-empty"
      />

      <UiStack v-else gap="sm" data-testid="product-features-list">
        <UiStack v-for="feature in features" :key="feature.id" direction="row" align="center" gap="sm">
          <UiInput
            v-model="feature.label"
            type="text"
            size="sm"
            :placeholder="t('settings.productFeatures.disk')"
            :data-testid="`product-feature-label-${feature.id}`"
            class="w-1/3"
            @keyup.enter="save(feature)"
          />
          <UiInput
            v-model="feature.value"
            type="text"
            size="sm"
            :placeholder="t('settings.productFeatures.10Gb')"
            :data-testid="`product-feature-value-${feature.id}`"
            class="flex-1"
            @keyup.enter="save(feature)"
          />
          <UiButton
            type="button"
            variant="secondary"
            size="sm"
            :disabled="busyId === feature.id"
            :data-testid="`product-feature-save-${feature.id}`"
            @click="save(feature)"
          >
            {{ t('shared.save') }}
          </UiButton>
          <UiIconButton
            :label="t('settings.productFeatures.removeLine')"
            size="sm"
            :disabled="busyId === feature.id"
            :data-testid="`product-feature-remove-${feature.id}`"
            @click="removeTarget = feature"
          >
            <UiIcon :markup="Icon.Trash" size="sm" />
          </UiIconButton>
        </UiStack>
      </UiStack>

      <UiStack direction="row" align="center" gap="sm" class="border-t border-border pt-4">
        <UiInput
          v-model="newLabel"
          type="text"
          size="sm"
          :placeholder="t('settings.productFeatures.feature')"
          :disabled="loading"
          data-testid="product-feature-new-label"
          class="w-1/3"
          @keyup.enter="add"
        />
        <UiInput
          v-model="newValue"
          type="text"
          size="sm"
          :placeholder="t('shared.value')"
          :disabled="loading"
          data-testid="product-feature-new-value"
          class="flex-1"
          @keyup.enter="add"
        />
        <UiButton
          type="button"
          variant="primary"
          size="sm"
          :loading="adding"
          :disabled="loading || !newLabel.trim() || !newValue.trim()"
          data-testid="product-feature-add"
          @click="add"
        >
          {{ t('settings.productFeatures.add') }}
        </UiButton>
      </UiStack>

      <UiText tone="muted" size="sm">
        {{ t('settings.productFeatures.plansLineUpByFeature') }}
      </UiText>
    </template>

    <UiDialog
      :open="removeTarget !== null"
      :variant="UiDialogVariant.Confirm"
      :title="t('settings.productFeatures.confirmRemoveTitle')"
      :message="t('settings.productFeatures.confirmRemoveMessage', { label: removeTarget?.label ?? '' })"
      :confirm-label="t('settings.productFeatures.removeLine')"
      :loading="busyId !== null"
      :tone="UiTone.Danger"
      @confirm="remove"
      @close="dismissRemove"
    />
  </UiDialog>
</template>
