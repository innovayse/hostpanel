/**
 * Which settings keys a card of the System Settings page owns.
 *
 * The cards edit every key of {@link SettingKey} and {@link PluginSettingKey}; the Advanced table lists
 * the rest, so a key is never editable in two places at once. Derived from the enums, so a key a card
 * starts to own leaves the table without a second list to keep in step.
 */
import { PluginSettingKey, SettingKey } from '@/types/setting'

/** Every key a card edits: the storefront keys and the plugin keys. */
export const CARD_OWNED_KEYS: ReadonlySet<string> = new Set<string>([
  ...Object.values(SettingKey),
  ...Object.values(PluginSettingKey),
])
