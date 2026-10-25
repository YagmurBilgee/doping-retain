/*
 * Official Doping Hafıza logo (public/images/doping-logo.svg, 224×52).
 * The caller sizes the box; object-contain keeps the logo's aspect ratio.
 */
function Logo({ className }: { className?: string }) {
  return (
    <img
      src="/images/doping-logo.svg"
      alt="Doping Hafıza"
      width={224}
      height={52}
      className={`object-contain object-left ${className ?? ''}`}
    />
  )
}

export default Logo
