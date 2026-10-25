import { ChevronLeft, ChevronRight } from 'lucide-react'

// There is a single daily task, so the arrows stay disabled until more tasks exist.
const ARROW = 'text-white disabled:cursor-default'

function DailyTasksCard() {
  return (
    <section className="flex w-full flex-col items-center rounded-tile bg-[radial-gradient(circle_at_center,#70a8d2_0%,#3880c2_72%)] px-3.5 pt-5 pb-6 text-center font-manrope text-white shadow-card sm:w-[174px] sm:shrink-0">
      <h2 className="text-base font-extrabold whitespace-nowrap">Günlük Görevlerim</h2>

      <div className="mt-2 flex w-full max-w-40 items-center justify-between">
        <button type="button" aria-label="Önceki görev" className={ARROW} disabled>
          <ChevronLeft size={27} strokeWidth={2} />
        </button>
        <span className="flex size-[52px] items-center justify-center rounded-full border-[3px] border-[#1f187a] bg-[#fefcef] text-[26px] font-extrabold text-[#4989f7] shadow-[0_0_0_1.5px_#5589ef] [text-shadow:1.5px_1.5px_0_#1f187a]">
          50
        </span>
        <button type="button" aria-label="Sonraki görev" className={ARROW} disabled>
          <ChevronRight size={27} strokeWidth={2} />
        </button>
      </div>

      <p className="mt-6 text-[13px] leading-snug font-medium">5 video izle ve 50 puan kazan</p>
    </section>
  )
}

export default DailyTasksCard
