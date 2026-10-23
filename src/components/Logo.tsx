/*
 * Official Doping Hafıza logo (public/images/doping-logo.svg, 224×52).
 * The 400×78 box keeps the sidebar's existing footprint; object-contain
 * fits the logo inside it without distortion.
 */
function Logo() {
  return (
    <img
      src="/images/doping-logo.svg"
      alt="Doping Hafıza"
      width={400}
      height={78}
      className="aspect-[400/78] w-full object-contain object-left"
    />
  )
}

export default Logo
