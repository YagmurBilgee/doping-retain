/*
 * Official Doping Hafıza logo (public/doping-hafiza-logo.png, 400×222).
 * The artwork occupies a centered 400×78 band of the transparent canvas,
 * so object-cover with that aspect ratio crops the empty margins.
 */
function Logo() {
  return (
    <img
      src="/doping-hafiza-logo.png"
      alt="Doping Hafıza"
      width={400}
      height={78}
      className="aspect-[400/78] w-full object-cover"
    />
  )
}

export default Logo
