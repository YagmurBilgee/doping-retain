/*
 * The YKS background (public/images/yks-background.png, 672×324) contains
 * only artwork, so the copy stays as HTML on the sky area of the image.
 * No overflow-hidden on the card: that would stop aspect-ratio from growing
 * with the content, so the image carries the rounded corners instead.
 */
function JourneyCard() {
  return (
    <section className="relative aspect-[672/324] rounded-card shadow-card">
      <img
        src="/images/yks-background.png"
        alt=""
        width={672}
        height={324}
        className="absolute inset-0 size-full rounded-card object-cover"
      />
      <div className="relative rounded-card bg-linear-to-r from-white/85 via-white/60 via-45% to-transparent to-75% p-card">
        <h2 className="text-lg font-bold text-ink">YKS Yolculuğum!</h2>
        <p className="mt-1 max-w-[60%] text-sm text-ink-secondary">
          Hedefine ulaşman için sana özel hazırlanmış bir yolculuk.
        </p>
        <button type="button" className="btn-primary mt-3">
          Yolculuğa Git
        </button>
      </div>
    </section>
  )
}

export default JourneyCard
