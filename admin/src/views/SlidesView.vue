<script setup lang="ts">
/**
 * Slides settings view: list and manage homepage slides.
 *
 * Shows the slides in a table ordered by sort order, with drag-and-drop reordering through
 * vuedraggable, audience and status badges, and navigation to the dedicated create and edit pages.
 * A refused load offers a retry; a delete (confirmed first in a danger prompt) or a reorder shows its
 * outcome (success, or the API's sentence) above the header.
 */
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import draggable from 'vuedraggable'
import SettingsLoading from '@/components/settings/SettingsLoading.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiRowActions from '@/components/ui/UiRowActions.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiImage from '@/components/ui/UiImage.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import { Icon } from '@/lib/icons'
import { storefrontAssetUrl } from '@/lib/runtimeConfig'
import { useSlidesStore } from '@/stores/slides'
import type { Slide } from '@/types/slide'
import { SlideAudience } from '@/types/slideaudience'
import { UiTone } from '@/utils/ui/tone'
import { UiDialogVariant } from '@/utils/ui/dialog'
import { AppRoute, createPath, editPath } from '@/lib/routes'

const { t } = useI18n()

/** Router, used to open the create and edit pages. */
const router = useRouter()

/** Slides store: the list, the loading flag and the refusal sentences. */
const store = useSlidesStore()

/** Local mutable copy of the slides for drag-and-drop reordering. */
const localSlides = ref<Slide[]>([])

/** True while the new order is being saved after a drag. */
const savingOrder = ref(false)

/** The slide awaiting delete confirmation, null when nothing is staged. */
const deleteTarget = ref<Slide | null>(null)

/** True while the staged slide is being deleted. */
const deleting = ref(false)

/** Badge tone of each audience. */
const AUDIENCE_TONES: Record<SlideAudience, UiTone> = {
  [SlideAudience.All]: 'neutral',
  [SlideAudience.Guest]: 'primary',
  [SlideAudience.Authenticated]: 'success',
}

/** Label key of each audience. */
const AUDIENCE_LABEL_KEYS: Record<SlideAudience, string> = {
  [SlideAudience.All]: 'settings.slideForm.all',
  [SlideAudience.Guest]: 'settings.slideForm.guest',
  [SlideAudience.Authenticated]: 'settings.slideForm.authenticated',
}

/** Syncs the local draggable list from the store (sorted by sortOrder). */
const syncLocal = (): void => {
  localSlides.value = [...store.slides].sort((a, b) => a.sortOrder - b.sortOrder)
}

/** Locale key of the success sentence of the last delete or reorder, null when there is none to show. */
const successKey = ref<string | null>(null)

/** The outcome line: the API's refusal first, else the success sentence, else nothing. */
const outcomeMessage = computed<string | null>(() => {
  if (store.actionError) {
    return store.actionError
  }
  return successKey.value === null ? null : t(successKey.value)
})

/** Tone of the outcome line: danger for a refusal, success otherwise. */
const outcomeTone = computed<UiTone>(() => (store.actionError ? UiTone.Danger : UiTone.Success))

/**
 * Loads the slides and syncs the draggable list.
 *
 * @returns Resolves when the load has settled.
 */
const loadSlides = async (): Promise<void> => {
  await store.fetchSlides()
  syncLocal()
}

onMounted(loadSlides)

/**
 * Badge tone of a slide's audience; an audience this build does not know reads as neutral.
 *
 * @param audience - Audience as the API sent it.
 * @returns The kit tone.
 */
const audienceTone = (audience: string): UiTone => AUDIENCE_TONES[audience as SlideAudience] ?? 'neutral'

/**
 * Label of a slide's audience; an audience this build does not know is shown as received.
 *
 * @param audience - Audience as the API sent it.
 * @returns The translated label, or the audience itself.
 */
const audienceLabel = (audience: string): string => {
  const key = AUDIENCE_LABEL_KEYS[audience as SlideAudience]
  return key ? t(key) : audience
}

/** Navigates to the create slide page. */
const navigateCreate = (): void => {
  void router.push(createPath(AppRoute.SettingsSlides))
}

/**
 * Navigates to the edit page for the given slide.
 *
 * @param slide - The slide to edit.
 */
const navigateEdit = (slide: Slide): void => {
  void router.push(editPath(AppRoute.SettingsSlides, slide.id))
}

/**
 * Resolves the title shown for a slide, falling back to its ID so a slide with no translation row
 * still names something the operator can recognise in the prompt.
 *
 * @param slide - The slide to label.
 * @returns The slide's first translated title, or a `Slide #id` placeholder.
 */
const slideTitle = (slide: Slide): string => slide.translations[0]?.title ?? t('settings.slides.slideNumber', { id: slide.id })

/**
 * Stages a slide for deletion. Only opens the prompt: {@link confirmDelete} does the work, so the
 * question is asked by the app's own dialog rather than the browser's blocking one.
 *
 * @param slide - The slide to delete.
 */
const handleDelete = (slide: Slide): void => {
  deleteTarget.value = slide
}

/** Deletes the staged slide, resyncs the draggable list, and clears the prompt. */
const confirmDelete = async (): Promise<void> => {
  const target = deleteTarget.value
  if (!target) {
    return
  }
  successKey.value = null
  deleting.value = true
  const deleted = await store.deleteSlide(target.id)
  syncLocal()
  deleting.value = false
  deleteTarget.value = null
  if (deleted) {
    successKey.value = 'shared.recordDeleted'
  }
}

/** Closes the delete confirmation; ignored while the delete runs. */
const dismissDelete = (): void => {
  if (!deleting.value) {
    deleteTarget.value = null
  }
}

/** Called after a drag ends: saves the new sort order to the backend. */
const onDragEnd = async (): Promise<void> => {
  successKey.value = null
  savingOrder.value = true
  const items = localSlides.value.map((slide, index) => ({
    id: slide.id,
    sortOrder: index,
  }))
  const saved = await store.updateOrder(items)
  syncLocal()
  savingOrder.value = false
  if (saved) {
    successKey.value = 'storefront.list.orderSaved'
  }
}

/**
 * Formats an ISO moment as a day in the viewer's locale.
 *
 * @param iso - ISO 8601 date string.
 * @returns The day as text.
 */
const formatDay = (iso: string): string => new Date(iso).toLocaleDateString()

/**
 * Formats a date range for display.
 *
 * @param from - ISO date string or null.
 * @param until - ISO date string or null.
 * @returns The range as text, or "Always" when it has no bounds.
 */
const formatDateRange = (from: string | null, until: string | null): string => {
  if (from && until) {
    return `${formatDay(from)} — ${formatDay(until)}`
  }
  if (from) {
    return t('settings.slides.from', { date: formatDay(from) })
  }
  if (until) {
    return t('settings.slides.until', { date: formatDay(until) })
  }
  return t('settings.slides.always')
}
</script>

<template>
  <UiStack gap="none" data-testid="slides-view">
    <UiStatusMessage :message="outcomeMessage" :tone="outcomeTone" class="mb-4" data-testid="slides-action-error" />

    <UiPageHeader :title="t('settings.slides.slides')" :subtitle="t('settings.slides.manageHomepageHeroSlidesDrag')">
      <template #actions>
        <UiText v-if="savingOrder" tone="muted" size="sm" role="status" data-testid="slides-saving-order">
          {{ t('settings.slides.savingOrder') }}
        </UiText>
        <UiButton type="button" variant="primary" data-testid="slides-add" @click="navigateCreate">
          {{ t('settings.slides.addSlide') }}
        </UiButton>
      </template>
    </UiPageHeader>

    <SettingsLoading v-if="store.loading" :message="t('settings.slides.loadingSlides')" />

    <UiErrorState v-else-if="store.error" :message="store.error" testid="slides-error" @retry="loadSlides" />

    <UiCard v-else class="overflow-hidden">
      <UiTable density="comfortable" :empty="localSlides.length === 0" :colspan="7">
        <template #empty>
          {{ t('settings.slides.noSlidesFoundAddYour') }}
        </template>
        <UiTableHead>
          <UiTableRow class="border-b border-border">
            <UiTableHeader class="w-10" />
            <UiTableHeader align="left">{{ t('settings.slides.image') }}</UiTableHeader>
            <UiTableHeader align="left">{{ t('shared.title') }}</UiTableHeader>
            <UiTableHeader align="left">{{ t('settings.slides.audience') }}</UiTableHeader>
            <UiTableHeader align="left">{{ t('shared.status') }}</UiTableHeader>
            <UiTableHeader align="left">{{ t('settings.slides.visibility') }}</UiTableHeader>
            <UiTableHeader align="right">{{ t('shared.actions') }}</UiTableHeader>
          </UiTableRow>
        </UiTableHead>
        <draggable
          v-model="localSlides"
          tag="tbody"
          item-key="id"
          handle=".drag-handle"
          ghost-class="opacity-40"
          :animation="200"
          class="divide-y divide-border"
          @end="onDragEnd"
        >
          <template #item="{ element: slide }">
            <UiTableRow hoverable :data-testid="`slide-row-${slide.id}`">
              <!-- Drag handle -->
              <UiTableCell>
                <UiStack align="center" justify="center" gap="none" class="drag-handle h-6 w-6 cursor-grab text-text-muted active:cursor-grabbing">
                  <UiIcon :markup="Icon.Grip" size="sm" />
                </UiStack>
              </UiTableCell>

              <!-- Thumbnail -->
              <UiTableCell>
                <UiImage
                  v-if="slide.imageUrl"
                  :src="storefrontAssetUrl(slide.imageUrl)"
                  :alt="slide.translations[0]?.title ?? t('settings.slides.slide')"
                  fit="cover"
                  rounded="md"
                  class="h-10 w-10 border border-border"
                />
                <UiStack v-else align="center" justify="center" gap="none" class="h-10 w-10 rounded-lg border border-border bg-surface-field text-text-muted">
                  <UiIcon :markup="Icon.Image" size="sm" />
                </UiStack>
              </UiTableCell>

              <!-- Title -->
              <UiTableCell>
                <UiText tone="primary" size="lg" class="font-medium">{{ slide.translations[0]?.title ?? '—' }}</UiText>
                <UiText v-if="slide.translations[0]?.tagline" tone="muted" size="xs">
                  {{ slide.translations[0].tagline }}
                </UiText>
              </UiTableCell>

              <!-- Audience -->
              <UiTableCell>
                <UiBadge :tone="audienceTone(slide.targetAudience)" small>
                  {{ audienceLabel(slide.targetAudience) }}
                </UiBadge>
              </UiTableCell>

              <!-- Active -->
              <UiTableCell>
                <UiBadge :tone="slide.isActive ? 'success' : 'neutral'" small>
                  {{ slide.isActive ? t('shared.active') : t('shared.inactive') }}
                </UiBadge>
              </UiTableCell>

              <!-- Date range -->
              <UiTableCell muted>
                {{ formatDateRange(slide.visibleFrom, slide.visibleUntil) }}
              </UiTableCell>

              <!-- Actions -->
              <UiTableCell align="right">
                <UiRowActions
                  :actions="[
                    { label: t('settings.slides.editSlide'), testid: `slide-edit-${slide.id}`, onSelect: () => navigateEdit(slide) },
                    { label: t('settings.slides.deleteSlide'), testid: `slide-delete-${slide.id}`, onSelect: () => handleDelete(slide), danger: true },
                  ]"
                />
              </UiTableCell>
            </UiTableRow>
          </template>
        </draggable>
      </UiTable>
    </UiCard>

    <!-- Delete confirmation -->
    <UiDialog
      v-if="deleteTarget"
      :variant="UiDialogVariant.Confirm"
      :title="t('settings.slides.deleteSlide2')"
      :message="t('settings.slides.deleteMessage', { title: slideTitle(deleteTarget) })"
      :confirm-label="t('shared.delete')"
      :loading-label="t('shared.deleting')"
      :loading="deleting"
      :tone="UiTone.Danger"
      @confirm="confirmDelete"
      @close="dismissDelete"
    />
  </UiStack>
</template>
