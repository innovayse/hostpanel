/**
 * Look of the loading ring (`UiLoader`): the diameter per control size, as complete classes so Tailwind emits them.
 */
import type { UiSize } from '@/utils/ui/size'

/** Width and height of the ring per size. */
export const loaderSizeClasses: Record<UiSize, string> = {
  sm: 'h-4 w-4',
  md: 'h-5 w-5',
  lg: 'h-6 w-6',
}
