<script setup lang="ts">
/**
 * The outside links of a plugin: documentation, support, source repository and where to buy a
 * licence, plus the licence name. A link the registry does not give is left out, and an address
 * that is not http or https is dropped (the registry is a third party, and a `javascript:` address
 * must never become a link). Renders nothing when there is nothing to show.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiCard from '@/components/ui/UiCard.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import type { RegistryPluginDetailsDto } from '@/types/plugin'
import { UiButtonVariant } from '@/utils/ui/button'

/** Translation function. */
const { t } = useI18n()

/** Props for PluginDetailsLinks. */
const props = defineProps<{
  /** The plugin's registry details. */
  details: RegistryPluginDetailsDto
}>()

/** Closed set of the links the page offers; the strings are the parts of the test ids and locale keys. */
const LinkKind = {
  /** The documentation. */
  Documentation: 'documentation',
  /** The support page. */
  Support: 'support',
  /** The source repository. */
  Repository: 'repository',
  /** Where to buy a licence. */
  Buy: 'buy',
} as const

/** One of the {@link LinkKind} values. */
type LinkKind = (typeof LinkKind)[keyof typeof LinkKind]

/** One link ready to render. */
interface LinkItem {
  /** Which link it is. */
  kind: LinkKind
  /** The address. */
  href: string
}

/**
 * Whether an address is safe to link to: an absolute http or https address.
 *
 * @param value - The address the registry gave, or null.
 * @returns True when it can be an anchor target.
 */
const isWebAddress = (value: string | null): value is string => {
  if (value === null) {
    return false
  }
  try {
    const protocol = new URL(value).protocol
    return protocol === 'https:' || protocol === 'http:'
  } catch {
    return false
  }
}

/** The links to show, in a fixed order, without the missing or unsafe ones. */
const links = computed<LinkItem[]>(() => {
  const candidates: { kind: LinkKind; href: string | null }[] = [
    { kind: LinkKind.Documentation, href: props.details.documentationUrl },
    { kind: LinkKind.Support, href: props.details.supportUrl },
    { kind: LinkKind.Repository, href: props.details.repositoryUrl },
    { kind: LinkKind.Buy, href: props.details.buyUrl },
  ]
  return candidates.flatMap(({ kind, href }) => (isWebAddress(href) ? [{ kind, href }] : []))
})
</script>

<template>
  <UiCard v-if="links.length > 0 || details.licence !== null" padding="md" data-testid="plugin-details-links">
    <UiStack gap="sm">
      <UiHeading :level="2" variant="section">{{ t('pluginDetails.links.heading') }}</UiHeading>
      <UiStack direction="row" align="center" gap="md" wrap>
        <UiLink
          v-for="link in links"
          :key="link.kind"
          :href="link.href"
          external
          :variant="UiButtonVariant.Link"
          :data-testid="`plugin-details-link-${link.kind}`"
        >
          {{ t(`pluginDetails.links.${link.kind}`) }}
        </UiLink>
      </UiStack>
      <UiText v-if="details.licence !== null" tone="muted" size="sm" data-testid="plugin-details-licence-name">
        {{ t('pluginDetails.links.licence', { licence: details.licence }) }}
      </UiText>
    </UiStack>
  </UiCard>
</template>
