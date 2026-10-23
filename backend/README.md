# Doping Retain API

Local ASP.NET Core API for the Doping Retain demo (.NET 8, pinned in `global.json`).
Stores one demo student and the four demo topics in SQLite.

## Setup and run

```sh
cd backend
dotnet run --project DopingRetain.Api
```

The first `dotnet run` restores the NuGet package (`Microsoft.EntityFrameworkCore.Sqlite`).
The API listens on **http://localhost:5080** (set in `DopingRetain.Api/Properties/launchSettings.json`).
If that port is taken, override it for one run:

```sh
dotnet run --project DopingRetain.Api --urls http://localhost:5081
```

## Frontend

In development the Vite server (`npm run dev` in the repository root) proxies `/api` to
`http://localhost:5080`, so run the API alongside it. The homepage risk card, the topic detail
modal, the mini quiz (review sessions, answers, completion) and the dev-only "Demoyu Sıfırla" button
(Alt+Shift+R) all use the API; the app bundle contains no answer keys or local scoring.
`src/utils/memoryModel.ts` and `src/data/mockTopics.ts` remain only as the source of the parity fixture.

## Tests

```sh
cd backend
dotnet test DopingRetain.Api.Tests
```

- `MemoryModelParityTests` compare the C# memory model with outputs recorded from
  `src/utils/memoryModel.ts`. After changing the TypeScript model, regenerate the fixture
  from the repository root:

  ```sh
  node --experimental-strip-types --no-warnings backend/DopingRetain.Api.Tests/Fixtures/generate-memory-model-fixtures.ts
  ```

- `TopicsPersistenceTests`, `DemoClockTests`, `ReviewSessionServiceTests`, `ReviewCompletionTests` and
  `DemoResetTests` use a temporary SQLite file per test and never touch the development database.

## Persistence

- **Location:** `Database:Path` in `appsettings.json` (default `App_Data/retain-phase5.db`).
  A relative path resolves against the content root, which `dotnet run --project DopingRetain.Api`
  sets to the project folder from any shell directory, so the default database is
  `backend/DopingRetain.Api/App_Data/retain-phase5.db`. An absolute path is used as is.
  The startup log prints the database path in use.
- **Schema:** created with `EnsureCreated` on startup; there are no migrations.
  `EnsureCreated` does nothing when the file already exists, so each schema change uses a new file.
  If the configured file lacks a table or column of the current schema, startup fails and lists the
  missing columns; the file is never altered or deleted.
- **Seed:** runs only when the database has no student. It captures one UTC timestamp and sets each
  topic's `LastStudiedAt` to that time minus the topic's `lastStudiedDaysAgo` from
  `src/data/mockTopics.ts`. Restarts keep existing rows and dates, so with the system clock
  retention keeps decaying: the 46 / 55 / 86 / 51 % values hold only shortly after the first seed.
- **Earlier databases** (kept, no longer used by default):
  - `App_Data/retain-phase4.db` (if created): Phase 4 schema. Phase 5 added `StudentTopicStates.Version`
    and `ReviewSessions.StateVersionAtStart`, `CompletedAt`, `NextReviewAt`, `OutcomeJson`.
  - `App_Data/retain.db`: Phase 3 schema, without review-session tables. Setting `Database:Path` to it fails at startup.
  - `bin/Debug/net8.0/App_Data/doping-retain.db`: the original location, copied to `App_Data/retain.db`
    with SQLite's backup API.

## Initial memory state from a first test (not wired in yet)

`InitialMemoryState.FromFirstAttemptTest` (`Memory/InitialMemoryState.cs`) builds a new topic state from
one test result: `InitialRecall = correctCount / questionCount` (unrounded), `Strength = 14` days,
`LastStudiedAt` = completion time in UTC, and empty review fields. It is a pure function with no
endpoint, table or seed connection; the demo data and percentages are unchanged.

- Only single-topic, first-attempt, post-learning tests qualify. The caller resolves the topic and uses
  the result only when the student has no state for that topic; repeated attempts are not handled.
- Mixed-topic tests need a question-to-topic mapping first.
- `S = 14` is an unvalidated starting assumption.
- Test accuracy is a proxy for initial recall, not a direct measurement of memory.

## Fixed demo clock (Development only)

By default the API uses the system UTC clock. For a reproducible demo, set `Demo:FixedUtcNow`
to an ISO 8601 timestamp with an offset (`Z` or `+03:00`). Seeding and every topics response then
use that instant. Use it with a separate, fresh database so the seed is created at the same instant:

```sh
cd backend
dotnet run --project DopingRetain.Api -- \
  --Demo:FixedUtcNow=2026-10-21T09:00:00Z \
  --Database:Path=App_Data/retain-fixed-demo.db
```

This returns 46 / 55 / 86 / 51 % with `evaluatedAt` = `2026-10-21T09:00:00+00:00` on every run.
The same settings can come from environment variables (`Demo__FixedUtcNow`, `Database__Path`).

Startup fails instead of guessing when:

- the environment is not Development and `Demo:FixedUtcNow` is set;
- the value is not a valid timestamp with an offset;
- the fixed instant is earlier than a stored `LastStudiedAt` (for example, an older fixed time
  against the regular database). Records are never modified; use a later time or another database.

Do not reuse a fixed-clock database with the system clock if its timestamp lies in the future.

## Endpoints

| Method | Path          | Response |
|--------|---------------|----------|
| GET    | `/api/health` | `200 {"status":"ok"}` |
| GET    | `/api/topics` | Demo student's topics evaluated at one instant (`evaluatedAt`), in display order |
| GET    | `/api/topics/{id}` | One topic with its micro-review content (`404 topic_not_found` if unknown) |
| POST   | `/api/topics/{id}/review-sessions` | Start or resume a review session (see below) |
| POST   | `/api/review-sessions/{id}/answers` | Record one answer and return its feedback (see below) |
| POST   | `/api/review-sessions/{id}/complete` | Complete a fully answered session (see below) |
| POST   | `/api/demo/reset` | Development only: reset the demo student's learning data, `204` (see below) |

Review sessions belong to the seeded demo student. Starting or answering never changes memory state
(strength, recall, `LastStudiedAt`, review count, success rate); only completion does.

| Method | Path | Success | Errors |
|--------|------|---------|--------|
| POST | `/api/topics/{id}/review-sessions` | `201` new session, `200` existing open session | `404 topic_not_found`, `409 topic_has_no_questions` |
| POST | `/api/review-sessions/{id}/answers` | `200` feedback (also for an identical retry) | `400 invalid_request` / `question_not_in_session` / `option_out_of_range`, `404 session_not_found`, `409 session_closed` / `session_abandoned` / `answer_conflict` |
| POST | `/api/review-sessions/{id}/complete` | `200` outcome (the stored one on every retry) | `404 session_not_found`, `409 session_incomplete` / `stale_session` / `session_abandoned` |

- Session statuses: `Open`, `Completed`, `Abandoned` (stored as text; no schema change was needed).
- Starting resumes the open session only if the topic's memory `Version` still equals the version stored
  when it opened. Otherwise that session becomes `Abandoned` and a fresh one opens, in one transaction.
  An abandoned session is never resumed and rejects every answer and completion with `409 session_abandoned`.

- A session response has `sessionId`, `topicId`, `startedAt`, ordered `questions` (`id`, `text`, `options`)
  and `answers` with feedback for already answered questions only.
- An answer request is `{"questionId": "trig-q1", "selectedIndex": 3}`; feedback is
  `questionId`, `selectedIndex`, `isCorrect`, `correctIndex`, `explanation`. The first answer is stored
  before feedback is returned and can never be changed.
- At most one open session exists per student and topic, and each question is answered once per session;
  both rules are database constraints, so concurrent duplicate requests resolve to the stored row.
- Completion takes no request body. The score comes from the stored answers and every question must be
  answered. One timestamp is used for the whole completion. The updated memory state, the session's
  `Completed` status, `CompletedAt`, `NextReviewAt` (completion + 2 / 7 / 14 days) and the outcome
  snapshot are saved in one transaction. A completed session returns that snapshot unchanged.
- The outcome has the frontend `ReviewOutcome` fields (`updatedTopic` in the `Topic` shape without
  quiz content, `review`, `retentionAtNextReview`, `retentionGain`), plus `sessionId`, `score`, `total`,
  `riskBefore`, `riskAfter`, `completedAt`, `nextReviewAt` and `updatedTopic.lastStudiedAt`. Both risks are
  evaluated at the completion instant, so `riskAfter.retention - riskBefore.retention` is `retentionGain`.
- Concurrency: completion runs in a `BEGIN IMMEDIATE` transaction, so it and answer inserts never
  interleave; an answer is inserted only while its session is `Open` (one SQL statement). Completion
  updates the memory state only if its `Version` still equals the version stored when the session
  started, then increments it; otherwise it returns `409 stale_session` and writes nothing.
- Errors use one body shape: `{"code": "...", "message": "..."}`.

### Demo reset (Development only)

`POST /api/demo/reset` exists only in the Development environment; elsewhere it returns `404`.
In one `BEGIN IMMEDIATE` transaction it:

- deletes the demo student's review sessions and their answers;
- restores each of the student's topic states to the seed values in `SeedData`, with `LastStudiedAt`
  counted back from one timestamp of the app clock (so a fixed demo clock gives 46 / 55 / 86 / 51 %);
- increments each topic state's `Version`, so nothing captured before the reset can match afterwards.

Topic content, questions and the student record stay; the database file is never deleted or recreated.
Answers and completions that commit first are undone by the reset; ones that run after it find their
session gone and get `404 session_not_found`.

`GET /api/topics/{id}` returns the same fields for one topic plus `questionCount`, `summaryPoints`
(`icon`, `tag`, `text`, optional `note`) and `evaluatedAt`. The summary points are static demo content
served from `SeedData`, not stored in the database. Quiz answers and explanations are not included.

`GET /api/topics` returns, per topic, `id`, `title`, `category`, `retention` (display percentage),
`riskLevel` (`safe` / `review_soon` / `high_risk`, from unrounded retention), `isFollowUp`,
`lastStudiedAt` (UTC), `reviewCount` and `successRate`, plus `highestRiskTopicId` and `highRiskCount`.
Quiz answers and explanations are never included.
