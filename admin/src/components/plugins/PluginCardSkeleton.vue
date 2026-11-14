<script setup lang="ts">
/**
 * The placeholder of a plugin card while the list loads: the same card with bars where the logo, the
 * name, the badges, two lines of description and the footer go, so the grid keeps its layout. Hidden
 * from assistive technology; the grid that holds the cards carries `aria-busy`.
 */
import UiCard from '@/components/ui/UiCard.vue'
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'
import UiStack from '@/components/ui/UiStack.vue'
</script>

<template>
  <UiCard padding="md" class="flex h-full flex-col gap-3 border border-transparent" aria-hidden="true" data-testid="plugin-card-skeleton">
    <UiStack direction="row" align="center" justify="between" gap="sm">
      <UiStack direction="row" align="center" gap="sm">
        <UiSkeletonBar class="h-12 w-12 rounded-control" />
        <UiSkeletonBar class="h-5 w-32 rounded-md" />
      </UiStack>
      <UiSkeletonBar class="h-5 w-16 rounded-full" />
    </UiStack>
    <UiSkeletonBar class="h-3 w-48 rounded-md" />
    <UiStack gap="xs">
      <UiSkeletonBar class="h-4 w-full rounded-md" />
      <UiSkeletonBar class="h-4 w-2/3 rounded-md" />
    </UiStack>
    <UiStack direction="row" align="center" justify="between" gap="sm" class="mt-auto border-t border-border pt-3">
      <UiSkeletonBar class="h-3 w-28 rounded-md" />
      <UiSkeletonBar class="h-8 w-20 rounded-control" />
    </UiStack>
  </UiCard>
</template>
