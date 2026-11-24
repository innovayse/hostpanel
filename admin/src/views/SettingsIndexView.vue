<script setup lang="ts">
/**
 * Settings index: every settings section as a glass card, grouped by what it controls.
 *
 * The catalogue is {@link SETTINGS_INDEX}; the screen only renders it. Each card is one link to its
 * section: an icon, the section's name and a line on what it holds. The grid is one column on
 * phones, two on tablets and three on wide screens.
 */
import { useI18n } from 'vue-i18n'
import { schemaHeader } from '@/lib/schemaHeader'
import VersionLabel from '@/components/layout/VersionLabel.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiRowLink from '@/components/ui/UiRowLink.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { SETTINGS_INDEX } from '@/lib/settingsIndex'
import { UiRowLinkLook } from '@/utils/ui/rowLink'
import { UiAlignItems, UiDirection, UiGap } from '@/utils/ui/layout'
import { UiHeadingVariant, UiTextSize, UiTextTone } from '@/utils/ui/text'
import schema from '@/views/SettingsIndexView.schema'

const { t } = useI18n()
</script>

<template>
  <UiStack :gap="UiGap.None" data-testid="settings-index-view">
    <UiPageHeader v-bind="schemaHeader(schema, t)" />
    <!-- The installed version, quietly under the title; absent when it could not be loaded. -->
    <VersionLabel testid="settings-version" class="-mt-4 mb-4" />

    <UiStack v-for="group in SETTINGS_INDEX" :key="group.group" :gap="UiGap.Sm" class="mb-8" :data-testid="`settings-group-${group.group}`">
      <UiHeading :level="2" :variant="UiHeadingVariant.Section">
        {{ t(`settings.index.groups.${group.group}`) }}
      </UiHeading>
      <UiGrid :columns="3">
        <UiCard v-for="entry in group.entries" :key="entry.item">
          <UiRowLink :to="entry.to" :look="UiRowLinkLook.Card" :data-testid="`settings-card-${entry.item}`">
            <UiStack :direction="UiDirection.Row" :align="UiAlignItems.Start" :gap="UiGap.Md">
              <UiIcon :markup="entry.icon" class="mt-0.5 text-text-accent" />
              <UiStack :gap="UiGap.Xs" class="min-w-0">
                <UiText :tone="UiTextTone.Primary" :size="UiTextSize.Lg" class="font-semibold">
                  {{ t(`settings.index.items.${entry.item}.title`) }}
                </UiText>
                <UiText :tone="UiTextTone.Secondary" :size="UiTextSize.Md">
                  {{ t(`settings.index.items.${entry.item}.description`) }}
                </UiText>
              </UiStack>
            </UiStack>
          </UiRowLink>
        </UiCard>
      </UiGrid>
    </UiStack>
  </UiStack>
</template>
