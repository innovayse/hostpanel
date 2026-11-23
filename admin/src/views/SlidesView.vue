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
import UiBadge from '@/components/ui/UiBadge.vue'
import UiDataTable from '@/components/ui/UiDataTable.vue'
import UiRowActions from '@/components/ui/UiRowActions.vue'
import type { UiMenuAction } from '@/utils/ui/menu'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiImage from '@/components/ui/UiImage.vue'
import { UiImageFit, UiImageRounded, UiImageSize } from '@/utils/ui/image'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiStatusIcon from '@/components/ui/UiStatusIcon.vue'
import UiSortableRows from '@/components/ui/UiSortableRows.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiSchemaTableHead from '@/components/ui/UiSchemaTableHead.vue'
import schema from '@/views/SlidesView.schema'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import { Icon } from '@/lib/icons'
import { storefrontAssetUrl } from '@/lib/runtimeConfig'
import { useSlidesStore } from '@/stores/slides'
import type { Slide } from '@/types/slide'
import { SlideAudience } from '@/types/slideaudience'
import { UiAlignItems, UiGap, UiJustify } from '@/utils/ui/layout'
import { UiSize } from '@/utils/ui/size'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'
import { UiDialogVariant } from '@/utils/ui/dialog'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiTableDensity } from '@/utils/ui/injection'
import { AppRoute, createPath, editPath } from '@/lib/routes'
import { EMPTY_VALUE } from '@/lib/format'
import { schemaHeader } from '@/lib/schemaHeader'
import { UiActionKind, actionIcons } from '@/utils/ui/actions'
import type { UiHeaderAction } from '@/utils/ui/actionBar'

const { t } = useI18n()

/** The header actions, each with its icon; on a phone only the primary one stays a button. */
const headerActions = computed<UiHeaderAction[]>(() => [
  { label: t('settings.slideForm.createSlide'), icon: actionIcons[UiActionKind.Add], variant: UiButtonVariant.Primary, testid: 'slides-add', to: createRoute },
])

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
const audienceTone = (audience: string): UiTone => AUDIENCE_TONES[audience as SlideAudience] ?? UiTone.Neutral

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

/** Route of the create slide page, opened by the Add link. */
const createRoute = createPath(AppRoute.SettingsSlides)

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
    return t('settings.slides.range', { from: formatDay(from), until: formatDay(until) })
  }
  if (from) {
    return t('settings.slides.from', { date: formatDay(from) })
  }
  if (until) {
    return t('settings.slides.until', { date: formatDay(until) })
  }
  return t('settings.slides.always')
}

/**
 * Row actions for a slide: edit as a real link to its page, and delete last (confirmed first).
 *
 * @param slide - The slide whose actions to build.
 * @returns The action list passed to UiRowActions.
 */
const slideActions = (slide: Slide): UiMenuAction[] => {
  /**
   * Forwards delete for this slide.
   * @returns Nothing.
   */
  const onDelete = (): void => handleDelete(slide)
  return [
    { label: t('shared.edit'), icon: Icon.Edit, testid: `slide-edit-${slide.id}`, to: editPath(AppRoute.SettingsSlides, slide.id) },
    { label: t('shared.delete'), icon: Icon.Trash, testid: `slide-delete-${slide.id}`, onSelect: onDelete, danger: true },
  ]
}
</script>

<template>
  <UiStack :gap="UiGap.None" data-testid="slides-view">
    <UiStatusMessage :message="outcomeMessage" :tone="outcomeTone" class="mb-4" data-testid="slides-action-error" />

    <UiPageHeader :actions="headerActions" v-bind="schemaHeader(schema, t)">
    </UiPageHeader>

    <UiErrorState v-if="store.error" :message="store.error" testid="slides-error" @retry="loadSlides" />

    <UiDataTable
      v-else
      :empty-text="t('settings.slides.noSlidesFoundAddYour')"
      :density="UiTableDensity.Comfortable"
      :empty="localSlides.length === 0"
      :loading="store.loading || savingOrder"
    >
      <UiSchemaTableHead :columns="schema.table.columns" />
      <UiSortableRows
        v-if="!store.loading && !savingOrder"
        v-model="localSlides"
        item-key="id"
        @end="onDragEnd"
      >
        <template #item="{ element: slide }">
          <UiTableRow hoverable :data-testid="`slide-row-${slide.id}`">
            <!-- Drag handle -->
            <UiTableCell>
              <UiStack :align="UiAlignItems.Center" :justify="UiJustify.Center" :gap="UiGap.None" class="drag-handle h-6 w-6 cursor-grab text-text-muted active:cursor-grabbing">
                <UiIcon :markup="Icon.Grip" :size="UiSize.Sm" />
              </UiStack>
            </UiTableCell>

            <!-- Thumbnail -->
            <UiTableCell>
              <UiImage
                :src="slide.imageUrl ? storefrontAssetUrl(slide.imageUrl) : undefined"
                :alt="slide.translations[0]?.title ?? t('settings.slides.slide')"
                :fit="UiImageFit.Cover"
                :rounded="UiImageRounded.Md"
                :size="UiImageSize.Thumb"
                fallback
              />
            </UiTableCell>

            <!-- Title -->
            <UiTableCell>
              <UiText :tone="UiTextTone.Primary" :size="UiTextSize.Lg" class="font-medium">{{ slide.translations[0]?.title ?? EMPTY_VALUE }}</UiText>
              <UiText v-if="slide.translations[0]?.tagline" :tone="UiTextTone.Muted" :size="UiTextSize.Xs">
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
              <UiStatusIcon
                :state="slide.isActive"
                :on-icon="Icon.CheckCircle"
                :off-icon="Icon.Close"
                :on-label="t('shared.active')"
                :off-label="t('shared.inactive')"
              />
            </UiTableCell>

            <!-- Date range -->
            <UiTableCell muted>
              {{ formatDateRange(slide.visibleFrom, slide.visibleUntil) }}
            </UiTableCell>

            <!-- Actions -->
            <UiTableCell actions>
              <UiRowActions :actions="slideActions(slide)" :testid="`slide-actions-${slide.id}`" />
            </UiTableCell>
          </UiTableRow>
        </template>
      </UiSortableRows>
    </UiDataTable>

    <!-- Delete confirmation -->
    <UiDialog
      v-if="deleteTarget"
      :variant="UiDialogVariant.Confirm"
      :title="t('settings.slides.deleteSlide2')"
      :message="t('settings.slides.deleteMessage', { title: slideTitle(deleteTarget) })"
      :confirm-label="t('shared.delete')"
      :loading="deleting"
      :tone="UiTone.Danger"
      @confirm="confirmDelete"
      @close="dismissDelete"
    />
  </UiStack>
</template>
