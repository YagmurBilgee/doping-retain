import type { DemoGroup } from '../../api/types'

/*
 * The YKS background (public/images/yks-background.png, 672×324) contains
 * only artwork, so the copy stays as HTML on the sky area of the image.
 * No overflow-hidden on the card: that would stop aspect-ratio from growing
 * with the content, so the image carries the rounded corners instead.
 */
function JourneyCard({ group }: { group: DemoGroup }) {
  return (
    <section className="relative flex aspect-[672/324] flex-col rounded-card font-manrope shadow-card">
      <img
        src="/images/yks-background.png"
        alt=""
        width={672}
        height={324}
        className="absolute inset-0 size-full rounded-card object-cover"
      />
      <div className="relative flex flex-1 flex-col items-start rounded-card bg-linear-to-r from-white/85 via-white/60 via-45% to-transparent to-75% px-3.5 pt-[18px] pb-[22px]">
        <h2 className="text-2xl leading-tight font-extrabold text-heading">
          {group === 'sixth_grade' ? 'Öğrenme Yolculuğum!' : 'YKS Yolculuğum!'}
        </h2>
        <p className="mt-1.5 max-w-60 text-sm leading-[1.55] text-[#282828]">
          Hedefine ulaşman için sana özel hazırlanmış bir yolculuk.
        </p>
        <button
          type="button"
          className="mt-auto rounded-md bg-heading px-3 py-[5px] text-[15px] font-bold text-white transition-colors hover:bg-heading/90 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-heading"
        >
          Yolculuğu Başlat
        </button>
      </div>
    </section>
  )
}

export default JourneyCard
