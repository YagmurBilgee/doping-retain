/* Head profile facing right; split into a navy back half and a red front half. */
const HEAD_PATH =
  'M13 43V34C7 31 3 25 3 18 3 9 10 2 19 2 28 2 34 8 34 15L37.5 21 34 22.5 34.6 25 33.5 26.6C34 29 32 31 29 30.6H26V43Z'
const SPLIT_X = 18.5

/** Doping Hafıza logo mark, wordmark and slogan, recreated for the desktop shell. */
function Logo() {
  return (
    <div className="flex items-center gap-2" aria-label="Doping Hafıza" role="img">
      <svg viewBox="0 0 40 44" className="h-10 w-9 shrink-0" aria-hidden="true">
        <defs>
          <clipPath id="logo-clip-back">
            <rect x="0" y="0" width={SPLIT_X} height="44" />
          </clipPath>
          <clipPath id="logo-clip-front">
            <rect x={SPLIT_X} y="0" width={40 - SPLIT_X} height="44" />
          </clipPath>
        </defs>
        <path d={HEAD_PATH} clipPath="url(#logo-clip-back)" className="fill-logo-navy" />
        <path d={HEAD_PATH} clipPath="url(#logo-clip-front)" className="fill-logo-red" />
        {/* Cross-cut notch between the two halves */}
        <path d={`M${SPLIT_X} 0V44M${SPLIT_X} 17H40`} className="stroke-white" strokeWidth="1.8" />
      </svg>

      <div className="flex flex-col" aria-hidden="true">
        <p className="text-[22px] leading-none font-bold tracking-tight">
          <span className="text-logo-navy">doping</span>
          <span className="text-logo-red">hafıza</span>
          <sup className="ml-0.5 align-super text-[8px] font-medium text-logo-red">®</sup>
        </p>
        <p className="mt-1 self-end text-[10.5px] leading-none text-ink-muted">öğrenmenin dijital hali</p>
      </div>
    </div>
  )
}

export default Logo
