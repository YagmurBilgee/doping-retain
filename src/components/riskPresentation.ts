import type { RiskLevel } from '../types'

/** Visual mapping of risk levels to design-system classes, shared by Retain components. */
export const RISK_PRESENTATION: Record<RiskLevel, { label: string; badge: string; bar: string }> = {
  high_risk: { label: 'Yüksek Risk', badge: 'badge-danger', bar: 'bg-danger' },
  review_soon: { label: 'Tekrar Zamanı', badge: 'badge-risk', bar: 'bg-risk' },
  safe: { label: 'Güvende', badge: 'badge-success', bar: 'bg-success' },
}
