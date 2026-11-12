<script setup lang="ts">
/**
 * The one plugin card the catalogue grid and the installed grid both draw.
 *
 * Top to bottom: the picture, the name and the Verified mark together on the title row; one meta
 * line (publisher, category, price); the rating when the registry sends one; the description
 * clamped to two lines; the default slot for anything a source adds (a refusal notice); one row of
 * status badges followed by up to three tag chips and a "+N" chip; and a footer pinned to the
 * bottom with the download count and version on the left and the `actions` slot on the right. The
 * card stretches to the row, so cards of a grid share one height.
 *
 * The component only draws what it is given: the grid that owns the data maps it into these
 * props, words the badges, and fills the `actions` slot with the buttons its source offers. When
 * `openable` is set the name becomes a button that stretches over the whole card, so a click
 * anywhere outside the footer buttons asks to open the plugin's page, and the card shows a focus
 * state for the keyboard.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiPluginAvatar from '@/components/ui/UiPluginAvatar.vue'
import UiRating from '@/components/ui/UiRating.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { Icon } from '@/lib/icons'
import type { PluginRegistryRatingDto } from '@/types/plugin'
import type { PluginTileBadge } from '@/types/pluginTile'
import { UiTone } from '@/utils/ui/tone'

/** Translation function and the interface language. */
const { t, locale } = useI18n()

/** How many tag chips a tile shows before the rest collapse into one "+N" chip. */
const VISIBLE_TAGS = 3

/** Props for PluginTile. */
const props = withDefaults(defineProps<{
  /** Plugin id; the parts of the tile's test ids carry it. */
  id: string
  /** Display name. */
  name: string
  /** Address of the plugin's icon; null or omitted when it has none. */
  iconUrl?: string | null
  /** Hex colour of the fallback picture; a neutral grey when omitted. */
  color?: string
  /** True to show the Verified mark next to the name. */
  verified?: boolean
  /** True when the plugin has a page to open: the name becomes a button that covers the card. */
  openable?: boolean
  /** The parts of the one meta line, already worded (publisher, category, price); joined with a dot. */
  meta: string[]
  /** Short description, clamped to two lines. */
  description: string
  /** What reviewers think of it; null or omitted draws no rating. */
  rating?: PluginRegistryRatingDto | null
  /** Status badges, drawn before the tags in the same row. */
  badges?: PluginTileBadge[]
  /** Tags without the hash; the first three show, the rest fold into a "+N" chip. */
  tags?: string[]
  /** Downloads of every version added up; null or omitted draws none. */
  downloads?: number | null
  /** The version to show in the footer; null or omitted draws none. */
  version?: string | null
}>(), {
  iconUrl: null,
  color: undefined,
  verified: false,
  openable: false,
  rating: null,
  /**
   * Default for `badges`.
   *
   * @returns No badge.
   */
  badges: () => [],
  /**
   * Default for `tags`.
   *
   * @returns No tag.
   */
  tags: () => [],
  downloads: null,
  version: null,
})

/** Events emitted by PluginTile. */
const emit = defineEmits<{
  /** Emitted with the plugin id when the admin opens the plugin's page from the name. */
  open: [id: string]
}>()

/** The meta parts joined into the one line. */
const metaLine = computed<string>(() => props.meta.join(' · '))

/** The tags shown as chips. */
const visibleTags = computed<string[]>(() => props.tags.slice(0, VISIBLE_TAGS))

/** How many tags are not shown as chips. */
const hiddenTagCount = computed<number>(() => Math.max(props.tags.length - VISIBLE_TAGS, 0))

/** Formatter of download counts in the interface language. */
const downloadFormat = computed<Intl.NumberFormat>(() => new Intl.NumberFormat(locale.value))
</script>

<template>
  <UiCard
    padding="md"
    class="relative flex h-full flex-col gap-3 transition-colors hover:border-accent has-[:focus-visible]:border-accent"
  >
    <UiStack direction="row" align="center" gap="sm" class="min-w-0">
      <UiPluginAvatar :name="name" :icon-url="iconUrl" :color="color" />
      <UiHeading :level="3" variant="card" class="min-w-0 truncate">
        <UiButton
          v-if="openable"
          type="button"
          class="max-w-full cursor-pointer truncate text-left outline-none after:absolute after:inset-0 after:rounded-2xl focus-visible:after:ring-2 focus-visible:after:ring-primary-500/60"
          :data-testid="`plugin-tile-open-${id}`"
          @click="emit('open', id)"
        >
          {{ name }}
        </UiButton>
        <template v-else>{{ name }}</template>
      </UiHeading>
      <UiBadge v-if="verified" :tone="UiTone.Success" small :data-testid="`plugin-tile-verified-${id}`">
        {{ t('plugins.pluginTile.verified') }}
      </UiBadge>
    </UiStack>

    <UiText tone="muted" size="xs" class="truncate" :data-testid="`plugin-tile-meta-${id}`">{{ metaLine }}</UiText>

    <UiRating
      v-if="rating"
      :average="rating.average"
      :count="rating.count"
      :data-testid="`plugin-tile-rating-${id}`"
    />

    <UiText tone="muted" size="sm" class="line-clamp-2" :data-testid="`plugin-tile-description-${id}`">
      {{ description }}
    </UiText>

    <slot />

    <UiStack
      v-if="badges.length || tags.length"
      direction="row"
      align="center"
      gap="xs"
      wrap
      :data-testid="`plugin-tile-chips-${id}`"
    >
      <UiBadge
        v-for="badge in badges"
        :key="badge.key"
        :tone="badge.tone"
        small
        :data-testid="`plugin-tile-badge-${id}-${badge.key}`"
      >
        {{ badge.label }}
      </UiBadge>
      <UiBadge v-for="tag in visibleTags" :key="tag" :tone="UiTone.Neutral" small :data-testid="`plugin-tile-tag-${id}-${tag}`">
        {{ t('plugins.pluginTile.tag', { tag }) }}
      </UiBadge>
      <UiBadge v-if="hiddenTagCount" :tone="UiTone.Neutral" small :data-testid="`plugin-tile-more-tags-${id}`">
        {{ t('plugins.pluginTile.moreTags', { count: hiddenTagCount }) }}
      </UiBadge>
    </UiStack>

    <UiStack
      direction="row"
      align="center"
      justify="between"
      gap="sm"
      class="relative mt-auto border-t border-border pt-3"
      :data-testid="`plugin-tile-footer-${id}`"
    >
      <UiStack direction="row" align="center" gap="md" class="min-w-0">
        <UiStack v-if="downloads != null" direction="row" align="center" gap="xs" :data-testid="`plugin-tile-downloads-${id}`">
          <UiIcon :markup="Icon.Download" size="sm" />
          <UiText tone="muted" size="xs">
            {{ t('plugins.pluginTile.downloads', { count: downloadFormat.format(downloads) }) }}
          </UiText>
        </UiStack>
        <UiStack v-if="version" direction="row" align="center" gap="xs" :data-testid="`plugin-tile-version-${id}`">
          <UiIcon :markup="Icon.Tag" size="sm" />
          <UiText tone="muted" size="xs">{{ t('plugins.pluginTile.version', { version }) }}</UiText>
        </UiStack>
      </UiStack>
      <UiStack direction="row" align="center" gap="xs" class="shrink-0">
        <slot name="actions" />
      </UiStack>
    </UiStack>
  </UiCard>
</template>
