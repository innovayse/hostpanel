<script setup lang="ts">
/**
 * One plugin as a card, drawn from a {@link PluginCardModel} so the same card serves the installed list
 * and the registry. Top to bottom: the title row (logo, name, verified mark and the state badges), one
 * meta line (publisher, category, price), the description clamped to two lines, one row of tags, and a
 * footer with the downloads and version on the left and the `action` slot on the right.
 *
 * The whole card opens the plugin's details page: the name is a link stretched over the card, so a
 * click anywhere follows it and a keyboard user reaches it by tab. The `action` and `notice` slots sit
 * above that link, so their buttons stay clickable. The `notice` slot carries the refusal block of a
 * plugin the host skipped; the card itself never reads the refusal data.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { RouteLocationRaw } from 'vue-router'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiPluginAvatar from '@/components/ui/UiPluginAvatar.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { Icon } from '@/lib/icons'
import { PluginCardBadge } from '@/types/plugin'
import type { PluginCardModel } from '@/types/plugin'
import { UiSize } from '@/utils/ui/size'
import { UiTone } from '@/utils/ui/tone'

/** Translation function. */
const { t } = useI18n()

/** Props for PluginCard. */
const props = defineProps<{
  /** What the card shows. */
  plugin: PluginCardModel
  /** The plugin's details page, opened by a click anywhere on the card. */
  to: RouteLocationRaw
}>()

/** Slots of PluginCard. */
defineSlots<{
  /** The action buttons at the right end of the footer (install, update, remove). */
  action?: () => unknown
  /** The block under the tags, such as the refusal explanation. */
  notice?: () => unknown
}>()

/** Groups the digits of a download count the way the browser's language does. */
const downloadFormat = new Intl.NumberFormat()

/** Tone of each badge. */
const badgeTone: Record<PluginCardBadge, UiTone> = {
  [PluginCardBadge.Active]: UiTone.Success,
  [PluginCardBadge.PendingRestart]: UiTone.Warning,
  [PluginCardBadge.Refused]: UiTone.Danger,
  [PluginCardBadge.Staged]: UiTone.Info,
  [PluginCardBadge.Update]: UiTone.Warning,
  [PluginCardBadge.BuiltIn]: UiTone.Neutral,
}

/** The badges that are a state of the plugin and so carry a status dot. */
const dotBadges: PluginCardBadge[] = [PluginCardBadge.Active, PluginCardBadge.PendingRestart, PluginCardBadge.Refused]

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
    case PluginCardBadge.Staged:
      return t('plugins.pluginCard.staged', { version: props.plugin.stagedVersion })
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
    padding="md"
    class="relative flex h-full flex-col gap-3 border border-transparent transition-colors hover:border-border-strong has-[a:focus-visible]:border-text-accent"
    :data-testid="`plugin-card-${plugin.id}`"
  >
    <UiStack direction="row" align="center" justify="between" gap="sm">
      <UiStack direction="row" align="center" gap="sm" class="min-w-0">
        <UiPluginAvatar :name="plugin.name" :icon-url="plugin.iconUrl" :color="plugin.color ?? undefined" :size="UiSize.Lg" />
        <UiLink
          :to="to"
          class="min-w-0 truncate rounded-control outline-none after:absolute after:inset-0 after:content-['']"
          :data-testid="`plugin-card-link-${plugin.id}`"
        >
          <UiText as="span" tone="primary" size="lg" class="font-semibold">{{ plugin.name }}</UiText>
        </UiLink>
        <UiIcon
          v-if="plugin.verified"
          :markup="Icon.CheckCircle"
          size="sm"
          class="shrink-0 text-status-green"
          role="img"
          :aria-label="t('plugins.pluginRegistry.verified')"
          :data-testid="`plugin-card-verified-${plugin.id}`"
        />
      </UiStack>
      <UiStack direction="row" align="center" gap="xs" class="shrink-0">
        <UiBadge
          v-for="badge in plugin.badges"
          :key="badge"
          :tone="badgeTone[badge]"
          :dot="dotBadges.includes(badge)"
          small
          :data-testid="`plugin-card-badge-${badge}-${plugin.id}`"
        >
          {{ badgeLabel(badge) }}
        </UiBadge>
      </UiStack>
    </UiStack>

    <UiText v-if="metaParts.length > 0" tone="muted" size="xs" class="truncate" :data-testid="`plugin-card-meta-${plugin.id}`">
      {{ metaParts.join(' · ') }}
    </UiText>

    <UiText tone="muted" size="sm" class="line-clamp-2" :data-testid="`plugin-card-description-${plugin.id}`">
      {{ plugin.description }}
    </UiText>

    <UiStack
      v-if="plugin.tags.length > 0"
      direction="row"
      align="center"
      gap="xs"
      class="overflow-hidden"
      :data-testid="`plugin-card-tags-${plugin.id}`"
    >
      <UiBadge v-for="tag in plugin.tags" :key="tag" :tone="UiTone.Neutral" small>
        {{ t('plugins.pluginRegistry.tag', { tag }) }}
      </UiBadge>
    </UiStack>

    <UiStack v-if="$slots.notice" class="relative">
      <slot name="notice" />
    </UiStack>

    <UiStack direction="row" align="center" justify="between" gap="sm" class="mt-auto border-t border-border pt-3">
      <UiStack direction="row" align="center" gap="md" class="min-w-0">
        <UiStack v-if="plugin.downloads !== null" direction="row" align="center" gap="xs" :data-testid="`plugin-card-downloads-${plugin.id}`">
          <UiIcon :markup="Icon.Download" size="sm" />
          <UiText tone="muted" size="xs">
            {{ t('plugins.pluginRegistry.downloads', { count: downloadFormat.format(plugin.downloads) }) }}
          </UiText>
        </UiStack>
        <UiText tone="muted" size="xs" class="font-mono" :data-testid="`plugin-card-version-${plugin.id}`">
          {{ t('plugins.pluginRegistry.version', { version: plugin.version }) }}
        </UiText>
      </UiStack>
      <UiStack v-if="$slots.action" direction="row" align="center" gap="sm" class="relative shrink-0">
        <slot name="action" />
      </UiStack>
    </UiStack>
  </UiCard>
</template>
