<template>
  <UiAccent :color="product.color">
    <UiCard
      hover
      padding="none"
      class="group relative flex flex-col self-stretch overflow-hidden [.tpl-classic_&]:[--accent:rgb(var(--classic-brand))] hover:-translate-y-1 hover:shadow-lg dark:hover:shadow-none [.tpl-classic_&]:hover:translate-y-0 [.tpl-classic_&]:hover:shadow-md transition-all duration-300 animate-reveal-up motion-reduce:animate-none"
      :class="revealDelay(index * 100)"
      :data-testid="`product-card-${product.id}`"
    >
      <NuxtLink :to="href" class="flex flex-col flex-1" :data-testid="`product-link-${product.id}`">
        <!-- Cover: the operator's group image, or the illustration generated for the family's type -->
        <UiStack
          gap="none"
          :class="[
            accent.tintStrong,
            'relative h-36 shrink-0 overflow-hidden [.tpl-classic_&]:bg-classic-brand-soft',
          ]"
          data-testid="product-cover"
        >
          <UiImage
            v-if="product.image"
            :src="product.image"
            :alt="product.name"
            width="800"
            height="288"
            class="absolute inset-0 w-full h-full"
            testid="product-cover-image"
          />
          <UiDecor
            v-else
            :class="[accent.fill, coverClass, 'inset-0 opacity-80 [.tpl-classic_&]:bg-classic-fill [.tpl-classic_&]:opacity-60']"
            data-testid="product-cover-art"
          />
          <UiBadge
            v-if="product.featured"
            variant="primary"
            size="sm"
            rounded
            class="absolute top-3 left-3 shadow-sm"
            data-testid="product-featured"
          >
            {{ $t('products.products.featured') }}
          </UiBadge>
        </UiStack>

        <!-- Icon chip, overlapping the cover edge -->
        <UiStack gap="none" class="relative z-10 -mt-7 ml-5 w-fit rounded-2xl bg-ui-surface p-1 [.tpl-classic_&]:rounded-lg">
          <UiAccentTile :icon="product.icon" size="sm" class="group-hover:scale-110 [.tpl-classic_&]:group-hover:scale-100" />
        </UiStack>

        <!-- Body: text and bullets take the room that is left, the action stays at the bottom -->
        <UiStack gap="none" class="flex-1 px-5 pb-5 pt-3 sm:px-6 sm:pb-6">
          <UiStack gap="none" class="flex-1">
            <UiHeading :level="3" class="break-words text-xl font-bold group-hover:text-primary-700 dark:group-hover:text-primary-300 [.tpl-classic_&]:font-semibold [.tpl-classic_&]:text-classic-ink [.tpl-classic_&]:group-hover:text-classic-brand transition-colors">
              {{ product.name }}
            </UiHeading>
            <!-- Only the static domains card carries a tagline; families have no such field. -->
            <UiText v-if="product.tagline" :as="UiTextTag.Span" size="xs" weight="bold" tone="inherit" :class="[accent.text, 'block uppercase tracking-wider mt-1']">{{ product.tagline }}</UiText>

            <!-- Clamped: a long description never makes one card taller than the others in its row. -->
            <UiText tone="muted" size="sm" class="mt-2 leading-relaxed line-clamp-3 break-words [.tpl-classic_&]:text-classic-muted">{{ product.description }}</UiText>

            <!-- Feature highlights -->
            <UiStack v-if="bullets.length" gap="xs" class="mt-4" data-testid="product-bullets">
              <UiStack v-for="(feature, featureIndex) in bullets" :key="featureIndex" direction="row" align="center" gap="xs">
                <CheckCircle :size="16" :stroke-width="2" :class="[accent.text, 'shrink-0 [.tpl-classic_&]:text-classic-brand']" />
                <UiText :as="UiTextTag.Span" size="xs" tone="inherit" class="text-gray-700 dark:text-gray-300 line-clamp-1 [.tpl-classic_&]:text-classic-ink">{{ feature }}</UiText>
              </UiStack>
            </UiStack>
          </UiStack>

          <!-- Action (the whole card is the link) -->
          <UiStack :as="UiStackTag.Span" direction="row" align="center" justify="center" gap="xs" :class="[accent.tintStrong, accent.borderMedium, 'relative mt-5 px-6 py-3 rounded-xl font-bold text-gray-900 dark:text-white border transition-all duration-300 overflow-hidden group/btn [.tpl-classic_&]:rounded-md [.tpl-classic_&]:border-transparent [.tpl-classic_&]:bg-classic-fill [.tpl-classic_&]:font-semibold [.tpl-classic_&]:text-classic-on-brand [.tpl-classic_&]:group-hover:bg-classic-fill-hover']">
            <UiStack :as="UiStackTag.Span" direction="row" align="center" gap="xs" class="relative z-10 group-hover/btn:translate-x-1 [.tpl-classic_&]:group-hover/btn:translate-x-0 transition-transform">
              {{ $t('products.pricing.cta.learnMore') }}
              <Icon name="mdi:arrow-right" :size="18" />
            </UiStack>
            <UiDecor :as="UiDecorTag.Span" class="inset-0 opacity-0 group-hover/btn:opacity-100 transition-opacity duration-300 bg-[linear-gradient(to_right,color-mix(in_srgb,var(--accent)_25%,transparent),color-mix(in_srgb,var(--accent)_12%,transparent))]" />
          </UiStack>
        </UiStack>
      </NuxtLink>
    </UiCard>
  </UiAccent>
</template>

<script setup lang="ts">
/**
 * One product-family card: a cover, an icon chip over the cover edge, the title, a clamped
 * description, up to three feature bullets and a button pinned to the bottom.
 *
 * The cover is the image an operator uploaded for the group, or else the illustration generated
 * for the family's product type (`utils/productTypeCover.ts`), drawn in the family accent.
 * Under the classic template the same markup turns into a flat, single-brand-colour card.
 * Shared by the products page and the home page so the two cards cannot drift apart.
 */

import { CheckCircle } from 'lucide-vue-next'
import { computed } from 'vue'
import { FAMILY_BULLET_LIMIT } from '~/constants/planFeatureLimit'
import { productTypeCover } from '~/utils/productTypeCover'
import { revealDelay } from '~/utils/reveal'
import { ACCENT_CLASSES } from '~/utils/ui/accent'
import type { ProductFamily } from '~/types/productfamily'
import { siteProductFamilyPath } from '~/constants/siteRoutes'
import { UiDecorTag, UiStackTag, UiTextTag } from '~/utils/ui/enums'

/** Props of ProductFamilyCard. */
interface Props {
  /** The family the card shows. */
  product: ProductFamily
  /** Position of the card in its grid; staggers the entrance. */
  index?: number
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  index: 0,
})

/** Prefixes a path with the active language. */
const localePath = useLocalePath()

/** Accent classes the card reads (the colour itself is set by the surrounding UiAccent). */
const accent = ACCENT_CLASSES

/** The localized address the card links to: the family's learn-more page, or its detail page when it has none. */
const href = computed<string>(() => localePath(props.product.learnMoreUrl || siteProductFamilyPath(props.product.id)))

/** Mask classes of the generated cover for the family's product type. */
const coverClass = computed<string>(() => productTypeCover(props.product.type))

/** The feature lines the card lists, never more than {@link FAMILY_BULLET_LIMIT}. */
const bullets = computed<string[]>(() => props.product.features.slice(0, FAMILY_BULLET_LIMIT))
</script>
