<script setup lang="ts">
/**
 * Placeholder with the shape of a {@link PluginTile}, shown in its place while a grid loads:
 * picture, title, meta line, two text lines, a badge row and a footer. It is built from
 * `UiSkeleton` and sized like the tile, so the grid does not jump when the real cards arrive.
 */
import UiCard from '@/components/ui/UiCard.vue'
import UiSkeleton from '@/components/ui/UiSkeleton.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { UiSkeletonShape } from '@/utils/ui/skeleton'
</script>

<template>
  <UiCard padding="md" class="flex h-full flex-col gap-3" data-testid="plugin-tile-skeleton">
    <UiStack direction="row" align="center" gap="sm">
      <UiSkeleton :shape="UiSkeletonShape.Circle" />
      <UiStack class="w-2/5">
        <UiSkeleton />
      </UiStack>
    </UiStack>
    <UiStack class="w-3/5">
      <UiSkeleton />
    </UiStack>
    <UiSkeleton :lines="2" />
    <UiStack direction="row" gap="xs" class="w-1/2">
      <UiSkeleton class="flex-1" />
      <UiSkeleton class="flex-1" />
    </UiStack>
    <UiStack direction="row" align="center" justify="between" class="mt-auto border-t border-border pt-3">
      <UiStack class="w-1/4">
        <UiSkeleton />
      </UiStack>
      <UiStack class="w-1/4">
        <UiSkeleton />
      </UiStack>
    </UiStack>
  </UiCard>
</template>
