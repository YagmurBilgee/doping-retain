/** A recall bullet in the micro-review, tagged with a Doping-style memory hint (served by GET /api/topics/{id}) */
export interface SummaryPoint {
  /** Short emoji marker, e.g. 📌 */
  icon: string
  /** Memory tag, e.g. "Temel Özdeşlik" */
  tag: string
  /** The formula or fact itself */
  text: string
  /** Optional plain-language reminder */
  note?: string
  /** Short lesson on the same point, shown step by step in the reinforcement flow */
  detail?: string
  /** Short worked example for the lesson */
  example?: string
}
