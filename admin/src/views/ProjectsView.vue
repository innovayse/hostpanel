<script setup lang="ts">
/**
 * Storefront projects list: the portfolio projects shown on the portal.
 *
 * Shows the projects ordered by sort order with drag-and-drop reordering, cover image and status,
 * and navigation to the dedicated create and edit pages. Deleting asks first.
 */
import { computed, onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import { navTitleKey } from '@/lib/navigation'
import StorefrontListTable from '@/components/storefront/StorefrontListTable.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiImage from '@/components/ui/UiImage.vue'
import { UiImageFit, UiImageRounded, UiImageSize } from '@/utils/ui/image'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiText from '@/components/ui/UiText.vue'
import { useStorefrontList } from '@/composables/useStorefrontList'
import { storefrontAssetUrl } from '@/lib/runtimeConfig'
import { storefrontColumnCaptions } from '@/lib/storefrontList'
import { displayTranslation } from '@/lib/storefrontTranslations'
import { useProjectsStore } from '@/stores/projects'
import type { Project } from '@/types/project'
import { storefrontTestIds } from '@/types/storefront'
import { AppRoute, createPath, editPath } from '@/lib/routes'
import schema from '@/views/ProjectsView.schema'

const { t } = useI18n()

/** Test ids of the page. */
const testids = storefrontTestIds('projects')

/** Projects store: the list, the loading flag and the refusal sentences. */
const store = useProjectsStore()

/** Delete confirmation and reorder state of the page. */
const list = useStorefrontList<Project>(store)

/** Column captions between the drag handle and the actions. */
const columns = computed<string[]>(() => storefrontColumnCaptions(schema.table.columns, t))

/**
 * The name shown for a project: its title, or its slug when no translation has one.
 *
 * @param item - The project.
 * @returns The name.
 */
const projectName = (item: Project): string => displayTranslation(item.translations)?.title || item.slug

/** The question shown for the staged project. */
const deleteMessage = computed<string>(() =>
  t('storefront.projects.deleteMessage', { name: list.deleteTarget.value ? projectName(list.deleteTarget.value) : '' }),
)

onMounted(async () => {
  // Errors of another page must not show on this list.
  store.resetErrors()
  await store.fetchAll()
})

/** Route of the create page, opened by the Add link. */
const createRoute = createPath(AppRoute.StorefrontProjects)

/**
 * Route of the edit page of a project, opened by its row action.
 *
 * @param item - The project to edit.
 * @returns The path of its edit page.
 */
const editRoute = (item: Project): string => editPath(AppRoute.StorefrontProjects, item.id)
</script>

<template>
  <StorefrontListTable
    :testids="testids"
    :title="t(navTitleKey(AppRoute.StorefrontProjects))"
    :subtitle="t('storefront.projects.subtitle')"
    :add-label="t('storefront.projects.create')"
    :empty-label="t('storefront.projects.empty')"
    :delete-title="t('storefront.projects.deleteTitle')"
    :move-up-label="t('storefront.list.moveUp')"
    :move-down-label="t('storefront.list.moveDown')"
    :columns="columns"
    :items="store.items"
    :loading="store.loading"
    :error="store.error"
    :action-error="store.actionError"
    :outcome="list.outcome.value"
    :saving-order="list.savingOrder.value"
    :delete-target="list.deleteTarget.value"
    :deleting="list.deleting.value"
    :delete-message="deleteMessage"
    :add-to="createRoute"
    :edit-to="editRoute"
    @stage-delete="list.stageDelete"
    @confirm-delete="list.confirmDelete"
    @cancel-delete="list.cancelDelete"
    @reorder="list.saveOrder"
    @retry="store.fetchAll"
  >
    <template #row="{ item }">
      <UiTableCell>
        <UiImage
          :src="item.imageUrls[0] ? storefrontAssetUrl(item.imageUrls[0]) : undefined"
          :alt="projectName(item)"
          :fit="UiImageFit.Cover"
          :rounded="UiImageRounded.Md"
          :size="UiImageSize.ThumbWide"
          fallback
        />
      </UiTableCell>
      <UiTableCell>
        <UiText :tone="UiTextTone.Primary" :size="UiTextSize.Lg" class="font-medium">{{ projectName(item) }}</UiText>
        <UiText v-if="displayTranslation(item.translations)?.summary" :tone="UiTextTone.Muted" :size="UiTextSize.Xs">
          {{ displayTranslation(item.translations)?.summary }}
        </UiText>
      </UiTableCell>
      <UiTableCell muted class="font-mono">{{ item.slug }}</UiTableCell>
      <UiTableCell>
        <UiBadge :tone="item.isActive ? UiTone.Success : UiTone.Neutral" small>
          {{ item.isActive ? t('shared.active') : t('shared.inactive') }}
        </UiBadge>
      </UiTableCell>
    </template>
  </StorefrontListTable>
</template>
