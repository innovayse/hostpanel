<script setup lang="ts">
/**
 * A plugin's picture in a fixed square: its icon when the plugin has one, otherwise a colour
 * block with the first letter of its name. The picture is decorative (empty alt, hidden from
 * assistive technology) because the name is always shown next to it; the letter's colour is
 * picked by contrast against the block.
 *
 * The icon is shown with `object-fit: contain` so it is never cropped. When the address is
 * missing, or the browser cannot load the picture, the colour block takes its place. The block is
 * an SVG whose `fill` takes the plugin's hex colour, because a colour known only at run time
 * cannot be a Tailwind class and an inline `style` is not allowed.
 */
import { computed, ref, watch } from 'vue'
import UiImage from '@/components/ui/UiImage.vue'
import { NEUTRAL_COLOR, readableTextColor } from '@/utils/ui/color'
import { UiImageFit, UiImageRounded } from '@/utils/ui/image'
import { UiSize } from '@/utils/ui/size'

/** Props for UiPluginAvatar. */
const props = withDefaults(defineProps<{
  /** Display name of the plugin: the source of the fallback letter (the picture itself is decorative, the name is shown beside it). */
  name: string
  /** Address of the plugin's icon; null or omitted when it has none. */
  iconUrl?: string | null
  /** Hex colour of the fallback block; a neutral grey when omitted. */
  color?: string
  /** Size of the square. */
  size?: UiSize
}>(), {
  iconUrl: null,
  color: undefined,
  size: UiSize.Md,
})

/** True once the browser failed to load the current icon address. */
const failed = ref(false)

/** A new address deserves a new attempt. */
watch(() => props.iconUrl, () => {
  failed.value = false
})

/** True when the icon should be drawn. */
const showIcon = computed<boolean>(() => Boolean(props.iconUrl) && !failed.value)

/** Colour of the fallback block: the declared colour, or the neutral grey. */
const fillColor = computed<string>(() => props.color ?? NEUTRAL_COLOR)

/** Colour of the letter, chosen for contrast against the block. */
const letterColor = computed<string>(() => readableTextColor(fillColor.value))

/** First letter of the name, upper case; a question mark for an empty name. */
const initial = computed<string>(() => props.name.trim().charAt(0).toUpperCase() || '?')

/** Literal Tailwind classes of the square per size. */
const sizeClasses: Record<UiSize, string> = {
  sm: 'h-8 w-8',
  md: 'h-10 w-10',
  lg: 'h-12 w-12',
}

/** Marks the icon as broken so the colour block replaces it. */
const onError = (): void => {
  failed.value = true
}
</script>

<template>
  <span class="inline-flex shrink-0 overflow-hidden rounded-control" :class="sizeClasses[size]" data-testid="plugin-avatar">
    <UiImage
      v-if="showIcon"
      :src="iconUrl ?? undefined"
      alt=""
      aria-hidden="true"
      :fit="UiImageFit.Contain"
      :rounded="UiImageRounded.None"
      class="h-full w-full"
      data-testid="plugin-avatar-image"
      @error="onError"
    />
    <svg
      v-else
      class="h-full w-full"
      viewBox="0 0 32 32"
      aria-hidden="true"
      data-testid="plugin-avatar-fallback"
    >
      <rect x="0" y="0" width="32" height="32" :fill="fillColor" />
      <text x="16" y="21" text-anchor="middle" font-size="14" font-weight="600" :fill="letterColor">{{ initial }}</text>
    </svg>
  </span>
</template>
