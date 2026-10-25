import { Megaphone } from 'lucide-react'

// No announcement source exists yet, so the panel always shows its empty state.
function Announcements() {
  return (
    <section className="min-w-0">
      <h2 className="panel-title">Duyurular</h2>
      <div className="panel-card mt-6 flex flex-col items-center justify-center px-6 text-center">
        <Megaphone
          size={64}
          fill="#f6a9ba"
          stroke="#ec7c90"
          strokeWidth={1.5}
          className="-rotate-12"
          aria-hidden="true"
        />
        <p className="mt-3 text-[15px] font-bold text-heading">Şu anda paylaşılmış bir duyuru yok!</p>
        <p className="mt-1.5 text-[13px] text-heading">Duyurular eklendiğinde burada göreceksin.</p>
      </div>
    </section>
  )
}

export default Announcements
