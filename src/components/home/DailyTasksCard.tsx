function DailyTasksCard() {
  return (
    <section className="card-brand flex w-full flex-col items-center justify-center gap-2 text-center sm:w-48">
      <h2 className="text-sm font-semibold">Günlük Görevlerim</h2>
      <span className="flex size-14 items-center justify-center rounded-full bg-white text-xl font-extrabold text-brand">
        50
      </span>
      <p className="text-xs text-white/80">5 video izle ve 50 puan kazan</p>
    </section>
  )
}

export default DailyTasksCard
