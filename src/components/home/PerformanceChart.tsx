import { ChevronDown } from 'lucide-react'

const CHART_LABELS = ['03 Eki', '04 Eki', '05 Eki', '06 Eki', '07 Eki', '08 Eki', '09 Eki', '10 Eki', '11 Eki']

interface PerformanceChartProps {
  /**
   * False shows the empty chart (a flat zero line): there is no study-time data, and the YKS sample curve
   * must not look like a sixth-grade student's measured performance.
   */
  hasData: boolean
}

/** Static placeholder chart mirroring the reference screen. */
function PerformanceChart({ hasData }: PerformanceChartProps) {
  return (
    <section>
      <div className="mb-4 flex items-center justify-between">
        <h2 className="section-title">Günlük Çalışma Performansı</h2>
        <button type="button" className="btn-secondary bg-surface px-3 py-1.5 text-xs shadow-card">
          Video İzleme
          <ChevronDown size={14} aria-hidden="true" />
        </button>
      </div>
      <div className="card">
        <svg viewBox="0 0 600 180" preserveAspectRatio="none" className="h-44 w-full text-brand" role="img" aria-label={hasData ? 'Günlük çalışma süresi grafiği (örnek)' : 'Günlük çalışma süresi grafiği (henüz veri yok)'}>
          {[20, 60, 100, 140].map((y) => (
            <line key={y} x1="0" x2="600" y1={y} y2={y} className="stroke-line" strokeDasharray="4 6" vectorEffect="non-scaling-stroke" />
          ))}
          <path
            d={hasData ? 'M0 120 C40 60 70 150 110 110 S180 40 220 90 S300 150 340 100 S410 30 450 80 S530 140 600 60' : 'M0 178 H600'}
            fill="none"
            stroke="currentColor"
            strokeWidth="2.5"
            strokeLinecap="round"
            vectorEffect="non-scaling-stroke"
          />
        </svg>
        <div className="mt-2 flex justify-between text-xs text-ink-muted">
          {CHART_LABELS.map((label) => (
            <span key={label}>{label}</span>
          ))}
        </div>
      </div>
    </section>
  )
}

export default PerformanceChart
