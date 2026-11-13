<template>
  <UiStack
    direction="row"
    wrap
    justify="center"
    gap="sm"
    data-testid="faq-category-nav"
  >
    <UiButton
      v-if="showAll"
      :to="localePath('/faq')"
      variant="subtle"
      size="sm"
      :class="CLASSIC_CHIP"
      data-testid="faq-category-all"
    >
      {{ t('faq.categories.all') }}
    </UiButton>
    <UiButton
      v-for="category in categories"
      :key="category.value"
      :to="localePath(`/faq/${category.slug}`)"
      :variant="category.value === active ? 'primary' : 'subtle'"
      size="sm"
      :class="category.value === active ? CLASSIC_CHIP_ACTIVE : CLASSIC_CHIP"
      :data-testid="`faq-category-${category.slug}`"
    >
      {{ category.label }}
    </UiButton>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * FaqCategoryNav: the row of links to the FAQ topic pages.
 *
 * Real links rather than client-side tabs, so every topic page is reachable (and crawlable) from
 * the FAQ landing page. On a topic page the current topic is highlighted and an "All" link leads
 * back to the landing page. Under the classic template the links are flat bordered chips and the
 * current topic a solid brand chip; aurora and nova keep the kit's subtle and primary buttons.
 */
import { FaqCategory } from '~/types/faqcategory'
import { faqCategorySlug } from '~/utils/faqCategory'

/** Props of FaqCategoryNav. */
interface Props {
  /** The topic whose page is open; it is drawn as the selected link. */
  active?: FaqCategory
  /** Adds the leading "All" link to the landing page. */
  showAll?: boolean
}

withDefaults(defineProps<Props>(), {
  active: undefined,
  showAll: false
})

/** Classic-only look of a topic link: white chip, hairline border, charcoal text, brand on hover. */
const CLASSIC_CHIP = '[.tpl-classic_&]:rounded-md [.tpl-classic_&]:border [.tpl-classic_&]:border-classic-border-strong [.tpl-classic_&]:bg-classic-surface [.tpl-classic_&]:text-classic-ink [.tpl-classic_&]:hover:border-classic-brand [.tpl-classic_&]:hover:bg-classic-brand-soft'

/** Classic-only look of the current topic: solid brand fill. */
const CLASSIC_CHIP_ACTIVE = '[.tpl-classic_&]:rounded-md [.tpl-classic_&]:border [.tpl-classic_&]:border-classic-fill [.tpl-classic_&]:bg-classic-fill [.tpl-classic_&]:text-classic-on-brand [.tpl-classic_&]:hover:bg-classic-fill-hover'

/** Translates a key in the active language. */
const { t } = useI18n()

/** Localised path builder from the i18n module. */
const localePath = useLocalePath()

/** Every topic with its URL slug and its label in the active language. */
const categories = computed(() => Object.values(FaqCategory).map(value => ({
  value,
  slug: faqCategorySlug(value),
  label: t(`faq.categories.${faqCategorySlug(value)}`)
})))
</script>
