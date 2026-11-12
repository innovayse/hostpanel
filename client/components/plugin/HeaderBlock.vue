<template>
  <UiStack gap="xs" data-testid="plugin-header-block">
    <UiStack v-if="block.compact" direction="row" gap="md" align="center" wrap data-testid="plugin-header-compact">
      <UiBackLink v-if="back" :to="back.to" :label="back.label" compact testid="plugin-page-back" />
      <UiStack v-if="title || subtitle" gap="none" class="min-w-0">
        <UiText as="p" weight="semibold" class="truncate" data-testid="plugin-header">{{ title }}</UiText>
        <UiText v-if="subtitle" as="p" size="xs" tone="muted" class="truncate font-mono" data-testid="plugin-header-subtitle">{{ subtitle }}</UiText>
      </UiStack>
      <UiStack direction="row" gap="md" align="center" wrap class="ml-auto">
        <PluginBlock v-for="action in actions" :key="action.key" :block="action.block" />
      </UiStack>
    </UiStack>
    <!-- Inside a host page the card around the view already says where it is: a section title, not the page title. -->
    <UiStack v-else-if="embedded" direction="row" gap="md" align="center" wrap data-testid="plugin-header-embedded">
      <UiHeading :level="UiHeadingLevel.H2" :size="UiHeadingSize.Subsection" class="min-w-0 truncate" data-testid="plugin-header">{{ title }}</UiHeading>
      <UiStack v-if="actions.length" direction="row" gap="md" align="center" wrap class="ml-auto">
        <PluginBlock v-for="action in actions" :key="action.key" :block="action.block" />
      </UiStack>
    </UiStack>
    <UiPageHeader v-else :title="title" class="!mb-0" data-testid="plugin-header">
      <template v-if="actions.length" #actions>
        <PluginBlock v-for="action in actions" :key="action.key" :block="action.block" />
      </template>
    </UiPageHeader>
    <UiText
      v-if="message?.text"
      size="xs"
      :tone="message.failed ? 'danger' : 'success'"
      class="sm:text-right"
      data-testid="plugin-header-message"
    >{{ message.text }}</UiText>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * PluginHeaderBlock - a plugin view's title, with its action buttons beside it. The title is interpolated, never
 * bound as HTML. Each action is drawn through PluginBlock, so its own `visibleWhen` applies like any block's.
 *
 * The sentence an action answers with is shown once, on one line under the whole header, not under its button: a
 * sentence inside one button's column made that column taller than the others and the row's buttons fell out of
 * line. The latest action's sentence replaces the previous one.
 *
 * In an embedded view (a slot widget on a host page) the header is a section title at the h2 level with its actions
 * beside it, not the page title.
 *
 * A `compact` header is one slim line for the top of a workspace page: the page's back link, a title and a smaller line
 * read from routes (`titleSource`, `subtitleSource`), and on the right whatever `actions` lines up, any block kind.
 */
import { providePluginActionMessages, usePluginBackLink, usePluginEmbedded, usePluginSource } from '~/composables/usePluginView'
import type { PluginHeaderBlock } from '~/types/pluginblock'
import { UiHeadingLevel, UiHeadingSize } from '~/utils/ui/enums'

/** Props of PluginHeaderBlock. */
interface Props {
  /** The block. */
  block: PluginHeaderBlock
}

/** The props. */
const props = defineProps<Props>()

/** Whether the view sits inside a host page, which gets the section header. */
const embedded = usePluginEmbedded()

/** The page's back link, which a compact header draws on its own line. */
const back = usePluginBackLink()

/** The title read, when the view names a source for it. */
const { value: titleRead } = usePluginSource(() => props.block.titleSource, () => props.block.id)

/** The smaller line read, when the view names a source for it. */
const { value: subtitleRead } = usePluginSource(() => props.block.subtitleSource, () => props.block.id)

/** The title shown: what was read, else what the view wrote. */
const title = computed(() => (typeof titleRead.value === 'string' && titleRead.value !== '' ? titleRead.value : props.block.title ?? ''))

/** The smaller line shown; empty for none. */
const subtitle = computed(() => (typeof subtitleRead.value === 'string' ? subtitleRead.value : ''))

/** The last sentence one of the header's actions answered with; null until one has. */
const message = providePluginActionMessages()

/**
 * The actions, each keyed by its id or kind; position is added because a header may repeat a kind but a view document
 * never reorders its actions.
 */
const actions = computed(() => (props.block.actions ?? []).map((action, position) => ({
  key: `${action.id ?? action.kind}:${position}`,
  block: action,
})))
</script>
