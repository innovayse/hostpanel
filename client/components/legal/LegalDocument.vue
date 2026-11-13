<template>
  <UiStack gap="none" data-testid="legal-document">
    <UiPageHero :badge="badge" :title="title" :subtitle="subtitle">
      <UiText
        v-if="meta"
        size="sm"
        tone="muted"
        class="mt-4"
        data-testid="legal-last-updated"
      >
        {{ meta }}
      </UiText>
    </UiPageHero>

    <!--
      The document sits in the shared container, the same width as the hero above it. From `lg` a
      sticky table of contents takes the left rail and the sections the rest; on a phone it is one
      column and the contents list is left out.
    -->
    <UiSection padding="md">
      <UiGrid cols="1" gap="xl" class="lg:grid-cols-[14rem_minmax(0,1fr)] lg:items-start" data-testid="legal-layout">
        <UiStack
          v-if="sections.length > 1"
          as="nav"
          gap="xs"
          :aria-label="t('legal.toc')"
          class="sticky top-24 hidden lg:flex"
          data-testid="legal-toc"
        >
          <UiHeading :level="2" size="label" class="mb-2">
            {{ t('legal.toc') }}
          </UiHeading>
          <UiLink
            v-for="(section, index) in sections"
            :key="section.id"
            :href="`#legal-section-${section.id}`"
            tone="muted"
            :underline="false"
            class="py-1 text-sm leading-snug"
            :testid="`legal-toc-${section.id}`"
          >
            {{ index + 1 }}. {{ section.title }}
          </UiLink>
        </UiStack>

        <UiStack gap="xl" class="min-w-0">
        <UiStack
          v-for="(section, index) in sections"
          :id="`legal-section-${section.id}`"
          :key="section.id"
          gap="md"
          :data-testid="`legal-section-${section.id}`"
          class="scroll-mt-24 animate-reveal-up motion-reduce:animate-none [.tpl-classic_&]:animate-none [.tpl-classic_&]:border-t [.tpl-classic_&]:border-classic-border [.tpl-classic_&]:pt-8 first:[.tpl-classic_&]:border-t-0 first:[.tpl-classic_&]:pt-0"
          :class="revealDelay(index * 100)"
        >
          <UiStack direction="row" align="center" gap="md">
            <UiTile
              size="xl"
              class="border border-primary-500/20 bg-primary-500/10 text-primary-700 dark:text-primary-300 [.tpl-classic_&]:rounded-md [.tpl-classic_&]:border-classic-border [.tpl-classic_&]:bg-classic-brand-soft [.tpl-classic_&]:text-classic-brand"
              data-testid="legal-section-marker"
            >
              <Icon v-if="section.icon" :name="section.icon" size="22" />
              <template v-else>
                {{ index + 1 }}
              </template>
            </UiTile>
            <UiHeading :level="2">
              {{ section.title }}
            </UiHeading>
          </UiStack>

          <UiStack gap="md" class="md:pl-14">
            <UiText
              v-for="(paragraph, paragraphIndex) in section.paragraphs"
              :key="paragraphIndex"
              tone="muted"
              size="lg"
              class="max-w-[75ch] leading-relaxed"
            >
              {{ paragraph }}
            </UiText>

            <UiList
              v-if="section.items?.length"
              :list-style="section.checklist ? 'plain' : 'marked'"
              data-testid="legal-section-items"
            >
              <UiListItem
                v-for="(item, itemIndex) in section.items"
                :key="itemIndex"
                :icon="section.checklist ? CheckCircle : undefined"
              >
                {{ item }}
              </UiListItem>
            </UiList>

            <UiCard
              v-for="(card, cardIndex) in section.cards"
              :key="`card-${cardIndex}`"
              padding="md"
              data-testid="legal-section-card"
            >
              <UiHeading :level="3" class="mb-2">
                {{ card.title }}
              </UiHeading>
              <UiText tone="muted" size="sm">
                {{ card.description }}
              </UiText>
            </UiCard>

            <UiAlert v-if="section.note" variant="info" :icon-size="20" data-testid="legal-section-note">
              {{ section.note }}
            </UiAlert>

            <UiLink
              v-if="section.link"
              :href="section.link.href"
              :underline="false"
              class="font-medium"
              testid="legal-section-link"
            >
              {{ section.link.label }}
            </UiLink>
          </UiStack>
        </UiStack>

        <UiCard
          v-if="contactTitle"
          padding="xl"
          data-testid="legal-contact"
          class="animate-reveal-up motion-reduce:animate-none [.tpl-classic_&]:animate-none [.tpl-classic_&]:rounded-lg [.tpl-classic_&]:border-classic-border [.tpl-classic_&]:bg-classic-surface-2 [.tpl-classic_&]:shadow-none"
          :class="revealDelay(200)"
        >
          <UiHeading :level="3" size="section" class="mb-4">
            {{ contactTitle }}
          </UiHeading>
          <UiText tone="muted" size="lg" class="mb-6 max-w-[75ch]">
            {{ contactDescription }}
          </UiText>
          <UiButton
            :to="localePath('/contact')"
            :variant="contactDanger ? 'danger' : 'primary'"
            size="lg"
            data-testid="legal-contact-link"
          >
            <component :is="contactIcon" v-if="contactIcon" :size="20" :stroke-width="2" class="mr-2" />
            {{ contactLink }}
            <ArrowRight :size="20" :stroke-width="2" class="ml-2" />
          </UiButton>
        </UiCard>
        </UiStack>
      </UiGrid>
    </UiSection>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * LegalDocument: the shared page body of every legal text (terms, privacy, cookies, refunds,
 * acceptable use): a hero with the title and the "last updated" line, a numbered or icon-led list
 * of sections, and a closing "contact us" card.
 *
 * The page owns its copy and SEO; this component only lays the text out, so the five documents
 * can not drift apart in how they look.
 *
 * The sections share the page container with the hero. From `lg` a sticky table of contents sits
 * on the left; paragraphs keep a reading measure of about 75 characters (`max-w-[75ch]`, on the
 * text only, never on the layout). Below `lg` the document is a single column.
 *
 * Under the classic template (the `tpl-classic` root class) the sections are ruled blocks with a
 * square brand-tint marker and the contact card is a flat neutral panel.
 */

import { revealDelay } from '~/utils/reveal'
import type { Component } from 'vue'
import { ArrowRight, CheckCircle } from 'lucide-vue-next'
import type { LegalSection } from '~/types/legalsection'

/** Props of LegalDocument. */
interface Props {
  /** Small pill above the title. */
  badge?: string
  /** The document title, rendered as the h1. */
  title: string
  /** One lead sentence under the title. */
  subtitle?: string
  /** The "last updated" line, already worded and dated by the page. */
  meta?: string
  /** The sections, in reading order. */
  sections: LegalSection[]
  /** Heading of the closing contact card; without it the card is not drawn. */
  contactTitle?: string
  /** One sentence in the closing contact card. */
  contactDescription?: string
  /** Label of the button that opens the contact page. */
  contactLink?: string
  /** Paints the contact button as a warning (the abuse-report card of the acceptable-use policy). */
  contactDanger?: boolean
  /** A lucide icon shown before the contact button label. */
  contactIcon?: Component
}

withDefaults(defineProps<Props>(), {
  badge: undefined,
  subtitle: undefined,
  meta: undefined,
  contactTitle: undefined,
  contactDescription: undefined,
  contactLink: undefined,
  contactDanger: false,
  contactIcon: undefined,
})

/** Translates a key in the active language. */
const { t } = useI18n()

/** Localised path builder from the i18n module. */
const localePath = useLocalePath()
</script>
