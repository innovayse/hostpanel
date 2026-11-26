<script setup lang="ts">
/**
 * One plugin as a card, drawn from a {@link PluginCardModel} so the same card serves the installed list
 * and the registry. Top to bottom: the title row (logo tile, the name over at most two lines, verified mark),
 * the row of state badges under it (so a long name is never squeezed by them), one meta line (publisher,
 * category, price), the description clamped to two lines, one row of tags, and a footer with the downloads
 * (in the short form of the active language) and version on one line on the left and the `action` slot on
 * the right. The card fills the height of its grid row and pins the footer to the bottom, so cards of a
 * row are equally tall.
 *
 * The whole card opens the plugin's details page: the name is a link stretched over the card, so a
 * click anywhere follows it and a keyboard user reaches it by tab. The `action` and `notice` slots sit
 * above that link, so their buttons stay clickable. The `notice` slot carries the refusal block of a
 * plugin the host skipped; the card itself never reads the refusal data.
 *
 * While the registry is still asked for a newer version (`checkingUpdate`), a placeholder holds the place
 * of the update badge, so the badge does not pop in. The footer has a fixed height, so the card keeps its
 * size whether its action area holds buttons, a placeholder or nothing.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { RouteLocationRaw } from 'vue-router'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiChipRow from '@/components/ui/UiChipRow.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiPluginAvatar from '@/components/ui/UiPluginAvatar.vue'
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { formatCompactNumber, joinParts } from '@/lib/format'
import { Icon } from '@/lib/icons'
import { PluginCardBadge } from '@/types/plugin'
import { failureSentence } from '@/lib/pluginChange'
import type { PluginCardModel } from '@/types/plugin'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiSize } from '@/utils/ui/size'
import { UiTone } from '@/utils/ui/tone'
import { UiCardPadding } from '@/utils/ui/card'
import { UiAlignItems, UiDirection, UiGap, UiJustify } from '@/utils/ui/layout'
import { UiTextSize, UiTextTag, UiTextTone } from '@/utils/ui/text'

/** Translation function and the active language. */
const { t, locale } = useI18n()

/** Props for PluginCard. */
const props = defineProps<{
  /** What the card shows. */
  plugin: PluginCardModel
  /** The plugin's details page, opened by a click anywhere on the card. */
  to: RouteLocationRaw
  /** True while the registry is checked for a newer version: a placeholder stands where the update badge will appear. */
  checkingUpdate?: boolean
}>()

/** Slots of PluginCard. */
defineSlots<{
  /** The action buttons at the right end of the footer (install, update, remove). */
  action?: () => unknown
  /** The block under the tags, such as the refusal explanation. */
  notice?: () => unknown
}>()

/** The download count in the short form of the active language, empty when the plugin has no count. */
const downloadsText = computed<string>(() =>
  props.plugin.downloads === null ? '' : formatCompactNumber(props.plugin.downloads, locale.value),
)

/** The tags as the chips read, one line of them with a "+N" chip for those that do not fit. */
const tagLabels = computed<string[]>(() => props.plugin.tags.map(tag => t('plugins.pluginRegistry.tag', { tag })))

/** True when the badge row has anything to draw: a badge, or the placeholder of the update badge. */
const hasBadgeRow = computed<boolean>(() => props.plugin.badges.length > 0 || props.checkingUpdate === true)

/** Tone of each badge. */
const badgeTone: Record<PluginCardBadge, UiTone> = {
  [PluginCardBadge.Active]: UiTone.Success,
  [PluginCardBadge.PendingRestart]: UiTone.Warning,
  [PluginCardBadge.Refused]: UiTone.Danger,
  [PluginCardBadge.LoadFailed]: UiTone.Danger,
  [PluginCardBadge.Staged]: UiTone.Info,
  [PluginCardBadge.ChangeFailed]: UiTone.Danger,
  [PluginCardBadge.Update]: UiTone.Warning,
  [PluginCardBadge.BuiltIn]: UiTone.Neutral,
}

/** The badges that are a state of the plugin and so carry a status dot. */
const dotBadges: PluginCardBadge[] = [
  PluginCardBadge.Active,
  PluginCardBadge.PendingRestart,
  PluginCardBadge.Refused,
  PluginCardBadge.LoadFailed,
  PluginCardBadge.ChangeFailed,
]

/**
 * Text of a badge, with the version it names where it names one.
 *
 * @param badge - The badge to label.
 * @returns The label in the operator's language.
 */
const badgeLabel = (badge: PluginCardBadge): string => {
  switch (badge) {
    case PluginCardBadge.Active:
      return t('shared.active')
    case PluginCardBadge.PendingRestart:
      return t('plugins.pluginCard.pendingRestart')
    case PluginCardBadge.Refused:
      return t('plugins.pluginCard.refused')
    case PluginCardBadge.LoadFailed:
      return t('plugins.pluginCard.loadFailed')
    case PluginCardBadge.Staged:
      return t('plugins.pluginCard.staged', { version: props.plugin.stagedVersion })
    case PluginCardBadge.ChangeFailed:
      return t('plugins.pluginCard.changeFailed')
    case PluginCardBadge.Update:
      return t('plugins.pluginCard.updateAvailable', { version: props.plugin.updateVersion })
    case PluginCardBadge.BuiltIn:
      return t('plugins.pluginCard.builtIn')
  }
}

/** The meta line's parts that exist, in order: publisher, category, price. */
const metaParts = computed<string[]>(() =>
  [
    props.plugin.publisher ? t('plugins.pluginRegistry.byAuthor', { author: props.plugin.publisher }) : '',
    props.plugin.category,
    props.plugin.pricingLabel,
  ].filter(part => part !== ''),
)
</script>

<template>
  <UiCard
    :padding="UiCardPadding.Md"
    class="relative flex h-full flex-col gap-3 border border-transparent transition-colors hover:border-border-strong has-[a:focus-visible]:border-text-accent"
    :data-testid="`plugin-card-${plugin.id}`"
  >
    <UiStack :direction="UiDirection.Row" :align="UiAlignItems.Center" :gap="UiGap.Sm" class="min-w-0" :data-testid="`plugin-card-title-${plugin.id}`">
      <UiPluginAvatar :name="plugin.name" :icon-url="plugin.iconUrl" :color="plugin.color ?? undefined" :size="UiSize.Lg" />
      <UiLink
        :to="to"
        :variant="UiButtonVariant.Link"
        class="min-w-0 flex-1 after:absolute after:inset-0 after:content-['']"
        :data-testid="`plugin-card-link-${plugin.id}`"
      >
        <UiText :as="UiTextTag.Span" :tone="UiTextTone.Primary" :size="UiTextSize.Lg" class="line-clamp-2 min-w-0 break-words font-semibold">{{ plugin.name }}</UiText>
      </UiLink>
      <UiIcon
        v-if="plugin.verified"
        :markup="Icon.CheckCircle"
        :size="UiSize.Sm"
        class="shrink-0 text-status-green"
        role="img"
        :aria-label="t('plugins.pluginRegistry.verified')"
        :data-testid="`plugin-card-verified-${plugin.id}`"
      />
    </UiStack>

    <UiStack v-if="hasBadgeRow" :direction="UiDirection.Row" :align="UiAlignItems.Center" :gap="UiGap.Xs" wrap :data-testid="`plugin-card-badges-${plugin.id}`">
      <UiBadge
        v-for="badge in plugin.badges"
        :key="badge"
        :tone="badgeTone[badge]"
        :dot="dotBadges.includes(badge)"
        small
        :title="badgeLabel(badge)"
        :data-testid="`plugin-card-badge-${badge}-${plugin.id}`"
      >
        {{ badgeLabel(badge) }}
      </UiBadge>
      <UiSkeletonBar v-if="checkingUpdate" class="h-4 w-24 rounded-full" aria-hidden="true" :data-testid="`plugin-card-badge-checking-${plugin.id}`" />
    </UiStack>

    <UiText v-if="metaParts.length > 0" :tone="UiTextTone.Muted" :size="UiTextSize.Xs" class="truncate" :data-testid="`plugin-card-meta-${plugin.id}`">
      {{ joinParts(metaParts) }}
    </UiText>

    <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm" class="line-clamp-2" :data-testid="`plugin-card-description-${plugin.id}`">
      {{ plugin.description }}
    </UiText>

    <UiText
      v-if="plugin.failedChange !== null"
      :tone="UiTextTone.Danger"
      :size="UiTextSize.Sm"
      :data-testid="`plugin-card-failure-${plugin.id}`"
    >
      {{ failureSentence(plugin.failedChange) }}
    </UiText>

    <UiChipRow v-if="tagLabels.length > 0" :chips="tagLabels" :data-testid="`plugin-card-tags-${plugin.id}`" />

    <UiStack v-if="$slots.notice" class="relative">
      <slot name="notice" />
    </UiStack>

    <UiStack :direction="UiDirection.Row" :align="UiAlignItems.Center" :justify="UiJustify.Between" :gap="UiGap.Sm" class="mt-auto h-12 border-t border-border pt-3" :data-testid="`plugin-card-footer-${plugin.id}`">
      <UiStack :direction="UiDirection.Row" :align="UiAlignItems.Center" :gap="UiGap.Md" class="min-w-0 overflow-hidden">
        <UiStack v-if="plugin.downloads !== null" :direction="UiDirection.Row" :align="UiAlignItems.Center" :gap="UiGap.Xs" class="shrink-0" :data-testid="`plugin-card-downloads-${plugin.id}`">
          <UiIcon :markup="Icon.Download" :size="UiSize.Sm" />
          <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Xs" class="whitespace-nowrap">
            {{ t('plugins.pluginRegistry.downloads', { count: downloadsText }) }}
          </UiText>
        </UiStack>
        <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Xs" class="shrink-0 whitespace-nowrap font-mono" :data-testid="`plugin-card-version-${plugin.id}`">
          {{ t('plugins.pluginRegistry.version', { version: plugin.version }) }}
        </UiText>
      </UiStack>
      <UiStack v-if="$slots.action" :direction="UiDirection.Row" :align="UiAlignItems.Center" :gap="UiGap.Sm" class="relative shrink-0">
        <slot name="action" />
      </UiStack>
    </UiStack>
  </UiCard>
</template>
