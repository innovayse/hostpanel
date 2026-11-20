/**
 * Skeleton schema of the fraud settings screen: a header over three cards, the protection switch, the risk score
 * thresholds and the per-address velocity limits.
 *
 * The switch takes a card of its own, so the note that the settings are off sits beside it; the two thresholds and the three
 * limits follow in cards of their own, each field named by its wire name like the real form binds it.
 */
import { navTitleKey } from '@/lib/navigation'
import { AppRoute } from '@/lib/routes'
import { FraudSettingsField } from '@/types/fraud'
import { SkeletonFieldKind, SkeletonKind, SkeletonSpan } from '@/utils/ui/skeletonSchema'
import type { FormSkeletonSchema } from '@/utils/ui/skeletonSchema'

/** What the fraud settings screen is made of. */
const schema: FormSkeletonSchema = {
  kind: SkeletonKind.Form,
  header: {
    title: navTitleKey(AppRoute.FraudSettings),
    subtitle: 'fraud.settings.subtitle',
  },
  columns: [
    [
      {
        key: 'protection',
        fields: [
          { key: 'enabled', label: 'fraud.settings.enabled', kind: SkeletonFieldKind.Toggle, span: SkeletonSpan.Full },
        ],
      },
      {
        key: 'thresholds',
        title: 'fraud.settings.sections.thresholds',
        fields: [
          { key: FraudSettingsField.ManualReviewRiskScore, label: 'fraud.settings.fields.manualReviewRiskScore.label' },
          { key: FraudSettingsField.AutoRejectRiskScore, label: 'fraud.settings.fields.autoRejectRiskScore.label' },
        ],
      },
      {
        key: 'velocity',
        title: 'fraud.settings.sections.velocity',
        fields: [
          { key: FraudSettingsField.VelocityWindowMinutes, label: 'fraud.settings.fields.velocityWindowMinutes.label' },
          { key: FraudSettingsField.VelocityMaxOrders, label: 'fraud.settings.fields.velocityMaxOrders.label' },
          { key: FraudSettingsField.MaxAccountsPerIp, label: 'fraud.settings.fields.maxAccountsPerIp.label' },
        ],
      },
    ],
  ],
}

export default schema
