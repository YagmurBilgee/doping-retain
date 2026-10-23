import { BrainCircuit, CalendarCheck, RotateCcw, ShieldCheck } from 'lucide-react'
import type { LoadState, TopicsResponse, TopicSummary } from '../api/types'
import LatestTestSummary from './LatestTestSummary'
import { formatDate, getStatusPresentation } from './reviewPresentation'

interface MemoryRiskCardProps {
  /** Backend evaluation of the student's topics */
  topics: LoadState<TopicsResponse>
  onRetry: () => void
  /** Estimated micro-review duration shown on each due topic's button */
  reviewMinutes?: number
  onStartReview?: (topicId: string) => void
}

function MemoryRiskCard({ topics, onRetry, reviewMinutes = 5, onStartReview }: MemoryRiskCardProps) {
  const data = topics.status === 'ready' ? topics.data : undefined
  // Backend priority order; the heading counts exactly what is listed.
  const dueTopics = data?.dueTopicIds.flatMap((id) => data.topics.filter((topic) => topic.id === id)) ?? []
  const supportTopics = data?.topics.filter(({ reviewStatus }) => reviewStatus === 'support_needed') ?? []
  const noDataTopics = data?.topics.filter(({ reviewStatus }) => reviewStatus === 'insufficient_data') ?? []

  return (
    <section className="card" aria-labelledby="memory-risk-title" aria-busy={topics.status === 'loading'}>
      <header className="mb-4 flex items-start justify-between gap-3">
        <div>
          <h2 id="memory-risk-title" className="section-title">
            Dopingle Hatırla
          </h2>
          <p className="section-subtitle mt-0.5">
            {!data
              ? 'Tekrar planın'
              : dueTopics.length > 0
                ? `${dueTopics.length} konunun tekrar zamanı geldi`
                : 'Şu an tekrar zamanı gelen konu yok'}
          </p>
        </div>
        {dueTopics.length > 0 || !data ? (
          <span className="icon-tile size-9 bg-risk-soft text-risk-ink" aria-hidden="true">
            <BrainCircuit size={18} />
          </span>
        ) : (
          <span className="icon-tile size-9 bg-success-soft text-success" aria-hidden="true">
            <ShieldCheck size={18} />
          </span>
        )}
      </header>

      {topics.status === 'loading' && <p className="text-sm text-ink-secondary">Konuların yükleniyor…</p>}

      {topics.status === 'error' && (
        <div role="alert">
          <p className="text-sm text-ink-secondary">Konular yüklenemedi. {topics.message}</p>
          <button type="button" className="btn-secondary mt-3 inline-flex items-center gap-1.5" onClick={onRetry}>
            <RotateCcw size={14} aria-hidden="true" />
            Tekrar Dene
          </button>
        </div>
      )}

      {data &&
        (dueTopics.length > 0 ? (
          <DueTopics topics={dueTopics} evaluatedAt={data.evaluatedAt} reviewMinutes={reviewMinutes} onStartReview={onStartReview} />
        ) : (
          <NothingDue data={data} />
        ))}

      {data && supportTopics.length > 0 && (
        <TopicList id="support-topics-title" title="Pekiştirme Önerilen Konular" topics={supportTopics} evaluatedAt={data.evaluatedAt} onOpenTopic={onStartReview} />
      )}

      {data && noDataTopics.length > 0 && (
        <TopicList id="no-data-topics-title" title="Test Sonucu Bekleyen Konular" topics={noDataTopics} evaluatedAt={data.evaluatedAt} onOpenTopic={onStartReview} />
      )}

      {/* Only from a loaded response, so loading or errors never show a made-up zero. */}
      {data && <WeeklyReviews count={data.weeklyReviewedTopicCount} />}
    </section>
  )
}

/** Secondary footer with the server's weekly count of distinct reviewed topics. */
function WeeklyReviews({ count }: { count: number }) {
  return (
    <p className="mt-4 flex items-center gap-1.5 border-t border-line pt-3 text-xs text-ink-muted">
      <CalendarCheck size={14} className="shrink-0" aria-hidden="true" />
      {count > 0 ? `Bu hafta ${count} farklı konuyu tekrar ettin.` : 'Bu haftaki ilk tekrarını yapmaya hazır mısın?'}
    </p>
  )
}

interface DueTopicsProps {
  topics: TopicSummary[]
  evaluatedAt: string
  reviewMinutes: number
  onStartReview?: (topicId: string) => void
}

/** Every due topic in priority order; the card heading already says they are due, so rows carry no status label. */
function DueTopics({ topics, evaluatedAt, reviewMinutes, onStartReview }: DueTopicsProps) {
  return (
    <ul className="flex flex-col gap-2">
      {topics.map((topic) => (
        <li key={topic.id} className="list-row block">
          {/* The action shares the title row, so the test summary below gets the full width. */}
          <div className="flex items-start gap-2">
            <div className="min-w-0 flex-1">
              <p className="section-subtitle">{topic.category}</p>
              <p className="truncate text-sm font-semibold text-ink">{topic.title}</p>
            </div>
            <button
              type="button"
              className="btn-primary shrink-0 px-3 py-1.5 text-xs"
              aria-label={`${topic.title} konusunu tekrar et (${reviewMinutes} dakika)`}
              onClick={() => onStartReview?.(topic.id)}
            >
              Tekrar Et ({reviewMinutes} dk)
            </button>
          </div>
          <div className="mt-1">
            <LatestTestSummary test={topic.latestTest} evaluatedAt={evaluatedAt} />
          </div>
        </li>
      ))}
    </ul>
  )
}

interface TopicListProps {
  id: string
  title: string
  topics: TopicSummary[]
  evaluatedAt: string
  onOpenTopic?: (topicId: string) => void
}

/** Topics listed below the due topics; they are never counted as due. */
function TopicList({ id, title, topics, evaluatedAt, onOpenTopic }: TopicListProps) {
  return (
    <section className="mt-5 border-t border-line pt-4" aria-labelledby={id}>
      <h3 id={id} className="text-sm font-semibold text-ink">
        {title}
      </h3>
      <ul className="mt-2 flex flex-col gap-2">
        {topics.map((listed) => {
          const status = getStatusPresentation(listed.reviewStatus)
          return (
            <li key={listed.id}>
              <button type="button" className="list-row block w-full text-left" onClick={() => onOpenTopic?.(listed.id)}>
                <span className="flex items-start gap-2">
                  <span className="min-w-0 flex-1">
                    <span className="section-subtitle block">{listed.category}</span>
                    <span className="block truncate text-sm font-semibold text-ink">{listed.title}</span>
                  </span>
                  <span className={`badge ${status.badge} shrink-0`}>{status.label}</span>
                </span>
                {/* Without a test the status badge already says so. */}
                {listed.latestTest && (
                  <span className="mt-1 block">
                    <LatestTestSummary test={listed.latestTest} evaluatedAt={evaluatedAt} />
                  </span>
                )}
              </button>
            </li>
          )
        })}
      </ul>
    </section>
  )
}

/** Nothing is due (the heading says so): name the next scheduled review, if any. */
function NothingDue({ data }: { data: TopicsResponse }) {
  const next = data.topics
    .filter(({ reviewStatus, nextReviewAt }) => reviewStatus === 'scheduled' && nextReviewAt)
    .sort((a, b) => Date.parse(a.nextReviewAt!) - Date.parse(b.nextReviewAt!))[0]
  if (!next) return null

  return (
    <p className="text-sm text-ink-secondary">
      Sıradaki tekrar: <span className="font-semibold text-ink">{next.title}</span> · {formatDate(next.nextReviewAt!, data.evaluatedAt)}
    </p>
  )
}

export default MemoryRiskCard
