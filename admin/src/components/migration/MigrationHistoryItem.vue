<script setup lang="ts">
/**
 * One migration job in the history list: its name, the job number, how far it has come (a progress bar), its state
 * and how long ago it was created, drawn as a selectable item that is highlighted while it is the open job. The delete
 * icon beside it (with a hint) only asks to delete; the screen confirms and deletes. The item is its own button, so it
 * can be chosen with the keyboard, and it tells assistive technology whether it is the chosen one.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import UiProgress from '@/components/ui/UiProgress.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import UiTooltip from '@/components/ui/UiTooltip.vue'
import { formatRelativeFromNow } from '@/lib/format'
import { Icon } from '@/lib/icons'
import { migrationStatusLabelKey } from '@/lib/migrationStatus'
import { migrationStatusTone } from '@/lib/tones'
import type { MigrationJob } from '@/types/migration'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiSize } from '@/utils/ui/size'

/** Translation function and the active locale (for the relative date). */
const { t, locale } = useI18n()

/** Props for MigrationHistoryItem. */
const props = defineProps<{
  /** The job the item shows. */
  job: MigrationJob
  /** Whether the job is the one open beside the history. */
  selected: boolean
  /** Whether the job is being deleted, which blocks its delete button. */
  deleting: boolean
}>()

const emit = defineEmits<{
  /** The operator chose the job. */
  select: [job: MigrationJob]
  /** The operator asked to delete the job. */
  delete: [job: MigrationJob]
}>()

/** The name of the job: the label the operator gave it, else the generic caption. */
const name = computed<string>(() => props.job.label ?? t('migration.migration.migration'))

/** How long ago the job was created, in words of the active locale. */
const createdAgo = computed<string>(() => formatRelativeFromNow(props.job.createdAt, locale.value))
</script>

<template>
  <UiStack
    direction="row"
    align="start"
    gap="xs"
    class="rounded-control border p-1.5 transition-colors"
    :class="selected ? 'border-border-control-hover bg-surface-active' : 'border-transparent hover:bg-surface-hover'"
    :data-testid="`migration-job-${job.id}`"
    :data-selected="selected ? 'true' : undefined"
  >
    <UiButton
      type="button"
      :variant="UiButtonVariant.Plain"
      class="min-w-0 flex-1 justify-start p-1.5 text-left"
      :aria-pressed="selected"
      :data-testid="`migration-select-${job.id}`"
      @click="emit('select', job)"
    >
      <UiStack gap="xs" class="w-full min-w-0">
        <UiStack direction="row" align="center" justify="between" gap="xs">
          <UiText size="sm" class="truncate font-medium">
            {{ name }}
          </UiText>
          <UiBadge :tone="migrationStatusTone[job.status]" small>
            {{ t(migrationStatusLabelKey[job.status]) }}
          </UiBadge>
        </UiStack>
        <UiProgress :value="job.overallPercent" :label="t('migration.migrationProgressCard.jobId', { id: job.id })" />
        <UiText tone="muted" size="xs" data-testid="migration-job-created">
          {{ createdAgo }}
        </UiText>
      </UiStack>
    </UiButton>
    <UiTooltip :text="t('migration.migration.deleteMigration')">
      <UiIconButton
        :label="t('migration.migration.deleteMigration')"
        size="sm"
        :disabled="deleting"
        :data-testid="`migration-delete-${job.id}`"
        @click="emit('delete', job)"
      >
        <UiIcon :markup="Icon.Trash" :size="UiSize.Sm" />
      </UiIconButton>
    </UiTooltip>
  </UiStack>
</template>
