import type { RiskLevel, TopicRisk } from '../types'

export interface RiskPresentation {
  label: string
  badge: string
  bar: string
  /** Text color, used as currentColor for retention rings */
  text: string
  /** Tinted surface + border for status panels */
  surface: string
}

/** Visual mapping of risk levels to design-system classes, shared by Retain components. */
export const RISK_PRESENTATION: Record<RiskLevel, RiskPresentation> = {
  high_risk: { label: 'Yüksek Risk', badge: 'badge-danger', bar: 'bg-danger', text: 'text-danger', surface: 'border-danger bg-danger-soft' },
  review_soon: { label: 'Tekrar Zamanı', badge: 'badge-risk', bar: 'bg-risk', text: 'text-risk', surface: 'border-risk bg-risk-soft' },
  safe: { label: 'Güvende', badge: 'badge-success', bar: 'bg-success', text: 'text-success', surface: 'border-success bg-success-soft' },
}

/** Weak last review: same amber treatment as review_soon, labelled as being watched. */
const FOLLOW_UP_PRESENTATION: RiskPresentation = { ...RISK_PRESENTATION.review_soon, label: 'Takipte' }

export function getRiskPresentation(risk: TopicRisk): RiskPresentation {
  return risk.isFollowUp ? FOLLOW_UP_PRESENTATION : RISK_PRESENTATION[risk.riskLevel]
}
