const LEFT_MENU = ['Doping Hafıza Desktop', 'Görünüm', 'Yardım']
const RIGHT_MENU = ['Tam Ekrandan Çık', 'Programı Kapat']

/** Decorative replica of the Doping Hafıza Desktop (Electron) window menubar. */
function DesktopTitleBar() {
  return (
    <div
      className="sticky top-0 z-20 flex h-7 shrink-0 items-center justify-between border-b border-line bg-surface px-3 text-xs text-ink-secondary select-none"
      aria-hidden="true"
    >
      <div className="flex items-center gap-5">
        {LEFT_MENU.map((item) => (
          <span key={item}>{item}</span>
        ))}
      </div>
      <div className="flex items-center gap-5">
        {RIGHT_MENU.map((item) => (
          <span key={item}>{item}</span>
        ))}
      </div>
    </div>
  )
}

export default DesktopTitleBar
