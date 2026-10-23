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

## Tests

```sh
cd backend
dotnet test DopingRetain.Api.Tests
```

- `RetentionModelTests` cover the pure formula and schedule.
- `TopicsPersistenceTests`, `DemoClockTests`, `ReviewSessionServiceTests`, `ReviewCompletionTests`,
  `DemoResetTests`, `WeeklyReviewedTopicsTests` and `TestResultServiceTests` use a temporary SQLite file
  per test and never touch the development database.

## Persistence

- **Location:** `Database:Path` in `appsettings.json` (default `App_Data/retain-phase6.db`).
  A relative path resolves against the content root, which `dotnet run --project DopingRetain.Api`
  sets to the project folder from any shell directory, so the default database is
  `backend/DopingRetain.Api/App_Data/retain-phase6.db`. An absolute path is used as is.
  The startup log prints the database path in use.
- **Schema:** created with `EnsureCreated` on startup; there are no migrations.
  `EnsureCreated` does nothing when the file already exists, so each schema change uses a new file.
  If the configured file lacks a table or column of the current schema, startup fails and lists the
  missing columns; the file is never altered or deleted.
- **Seed (demo scenario):** runs only when the database has no student; the demo reset restores the same
  scenario. Five topics from five subjects each get one **synthetic** normal-test result (made-up demo
  data, not a real student's). Its completion time is derived backwards from the moment its curve should
  reach the review threshold (`completedAt = target − h · log2(A / threshold)`), using one reference
  instant and the Europe/Istanbul calendar day; timestamps are stored in UTC.

  | Topic | Score | Threshold reached |
  |---|---|---|
  | Trigonometri (AYT Matematik) | 18/20 | today, ¼ of the way from 00:00 to now |
  | Hücre Bölünmeleri (TYT Biyoloji) | 16/20 | today, ½ of the way |
  | Hareket ve Kuvvet (TYT Fizik) | 11/15 | today, ¾ of the way |
  | Sözcükte Anlam (TYT Türkçe) | 19/20 | tomorrow 10:00 |
  | Atom ve Periyodik Sistem (TYT Kimya) | 22/25 | in three days, 10:00 |

  So three topics are due (also just after midnight), one is scheduled for tomorrow, one for three days
  later, and no review is completed yet. Restarts keep existing rows and dates, so with the system clock
  the statuses keep changing. `ayt-mat-fonksiyonlar` stays as content but is not assigned to the student.
  Every topic has three reminder points (served from `SeedData`) and three quiz questions. On an existing
  database, startup adds only the demo topics and questions it lacks (matched by Id); student data,
  results, sessions and existing content are never changed, and a second start adds nothing. An existing
  database gets the new scenario only through the explicit demo reset (`POST /api/demo/reset`, Alt+Shift+R).
- **Earlier databases** (kept, no longer used by default):
  - `App_Data/retain-phase5.db`: Phase 5 schema with the old stability model. Phase 6 added the
    `TestResults` table and `StudentTopicStates.LastReviewedAt`. Its stored R0/S values cannot be turned
    into real test results, so it is not migrated; setting `Database:Path` to it fails at startup.
  - `App_Data/retain-phase4.db` (if created): Phase 4 schema. Phase 5 added `StudentTopicStates.Version`
    and `ReviewSessions.StateVersionAtStart`, `CompletedAt`, `NextReviewAt`, `OutcomeJson`.
  - `App_Data/retain.db`: Phase 3 schema, without review-session tables. Setting `Database:Path` to it fails at startup.
  - `bin/Debug/net8.0/App_Data/doping-retain.db`: the original location, copied to `App_Data/retain.db`
    with SQLite's backup API.

## Retention model

`R(t) = A · 2^(−t / h)` (`Memory/RetentionModel.cs`, pure). `A` is correct / total of the topic's latest
completed test (normal test or reminder mini quiz, by completion time; equal times go to the later
recorded result), `t` is days since that test, and the review is due where the same curve reaches the
threshold: `t* = h · log2(A / threshold)`. A score at or below the threshold needs support now; a topic
without a test has insufficient data.

`RetentionModel:HalfLifeDays` (10) and `RetentionModel:ReviewThreshold` (0.50) in `appsettings.json` are
provisional, shared demo assumptions, not scientific constants; nothing learns or personalizes them.

## Normal test results (demo integration contract)

`POST /api/topics/{id}/test-results` with `{ "correctCount", "totalQuestions", "completedAt" }`
(ISO 8601 with an offset) records a normal test for the demo student. It is a minimal demo contract,
not an existing company integration. It updates the topic's test observation only: it does not complete
a review, so `lastReviewedAt` and the weekly review count are unchanged. Future completion times and
invalid scores are rejected.

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

This returns the same statuses and review dates with `evaluatedAt` = `2026-10-21T09:00:00+00:00` on every run.
The same settings can come from environment variables (`Demo__FixedUtcNow`, `Database__Path`).

Startup fails instead of guessing when:

- the environment is not Development and `Demo:FixedUtcNow` is set;
- the value is not a valid timestamp with an offset;
- the fixed instant is earlier than a stored test completion (for example, an older fixed time
  against the regular database). Records are never modified; use a later time or another database.

Do not reuse a fixed-clock database with the system clock if its timestamp lies in the future.

## Endpoints

| Method | Path          | Response |
|--------|---------------|----------|
| GET    | `/api/health` | `200 {"status":"ok"}` |
| GET    | `/api/topics` | Demo student's topics evaluated at one instant (`evaluatedAt`), in display order |
| GET    | `/api/topics/{id}` | One topic with its micro-review content (`404 topic_not_found` if unknown) |
| POST   | `/api/topics/{id}/test-results` | Demo contract: record a normal test, `201` (`400 invalid_request` / `invalid_score` / `future_completion`, `404 topic_not_found`) |
| POST   | `/api/topics/{id}/review-sessions` | Start or resume a review session (see below) |
| POST   | `/api/review-sessions/{id}/answers` | Record one answer and return its feedback (see below) |
| POST   | `/api/review-sessions/{id}/complete` | Complete a fully answered session (see below) |
| POST   | `/api/demo/reset` | Development only: reset the demo student's learning data, `204` (see below) |

Review sessions belong to the seeded demo student. Starting or answering never stores a test result or a
review date; only completion does.

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
  answered. One timestamp is used for the whole completion. The quiz's actual score is stored as a
  `reminder_mini_quiz` test result (unique per session), the topic's `LastReviewedAt` is set, and the
  session's `Completed` status, `CompletedAt`, `NextReviewAt` (from the retention model) and the outcome
  snapshot are saved in one transaction. A completed session returns that snapshot unchanged and stores nothing.
- The outcome has `sessionId`, `score`, `total`, `reviewStatus`, `testResult`, `completedAt`,
  `nextReviewAt` and `updatedTopic` (`id`, `title`, `category`, `lastReviewedAt`).
- Concurrency: completion runs in a `BEGIN IMMEDIATE` transaction, so it and answer inserts never
  interleave; an answer is inserted only while its session is `Open` (one SQL statement). Completion
  writes only if the topic state's `Version` still equals the version stored when the session
  started, then increments it; otherwise it returns `409 stale_session` and writes nothing.
- Errors use one body shape: `{"code": "...", "message": "..."}`.

### Demo reset (Development only)

`POST /api/demo/reset` exists only in the Development environment; elsewhere it returns `404`.
In one `BEGIN IMMEDIATE` transaction it:

- deletes the demo student's test results, review sessions and their answers;
- aligns the student's topics with the five demo topics (others are unassigned, their content stays),
  clears each topic's `LastReviewedAt` and re-adds the seed's synthetic normal-test results for one
  timestamp of the app clock (so a fixed demo clock always gives the same statuses);
- increments each topic state's `Version`, so nothing captured before the reset can match afterwards.

Topic content, questions and the student record stay; the database file is never deleted or recreated.
Answers and completions that commit first are undone by the reset; ones that run after it find their
session gone and get `404 session_not_found`.

`GET /api/topics/{id}` returns the same fields for one topic plus `questionCount`, `summaryPoints`
(`icon`, `tag`, `text`, optional `note`) and `evaluatedAt`. The summary points are static demo content
served from `SeedData`, not stored in the database. Quiz answers and explanations are not included.

`GET /api/topics` returns, per topic, `id`, `title`, `category`, `reviewStatus` (`insufficient_data` /
`scheduled` / `due` / `support_needed`), `latestTest` (`testType`, `completedAt`, `correctCount`,
`totalQuestions`), `nextReviewAt` and `lastReviewedAt`, plus `dueTopicIds` (due topics in review priority
order: lowest unrounded recall first, ties in display order) and `weeklyReviewedTopicCount`.
Quiz answers and explanations are never included.
