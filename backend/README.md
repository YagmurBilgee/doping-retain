# Doping Retain API

Local ASP.NET Core API for the Doping Retain demo (.NET 8, pinned in `global.json`).
Stores two demo groups in one SQLite file: the YKS (12th grade) demo student with its topics, and five
sixth-grade mock students sharing eight curriculum topics. Both groups use the same retention model and
review implementation.

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
`http://localhost:5080`, so run the API alongside it. The homepage review card, the topic detail
modal, the mini quiz (review sessions, answers, completion) and the "Demoyu Sıfırla" button (Alt+Shift+R,
shown when `Demo:Enabled` is true; it resets only the selected grade) all use the API; the app bundle
contains no answer keys or local scoring. The grade menu (top right) selects the group; grade and student
are kept in the URL (`?grade=6&student=grade6-elif`, `?grade=12&student=demo-student`).

## Tests

```sh
cd backend
dotnet test DopingRetain.Api.Tests
```

- `RetentionModelTests` cover the pure formula and schedule; `ReviewScheduleTests` cover the separate
  review scheduling policy (10 → 30 days, early retries, resets, schema update) with a fixed clock.
- `TopicsPersistenceTests`, `DemoClockTests`, `ReviewSessionServiceTests`, `ReviewCompletionTests`,
  `DemoResetTests`, `WeeklyReviewedTopicsTests`, `WeeklySummaryTests` and `TestResultServiceTests` use a temporary SQLite file
  per test and never touch the development database.
- `SixthGradeScenarioTests` cover the sixth-grade content, the seed matrix (hand-written expected states),
  student and group isolation and mini quiz recording; `DemoEditorTests` cover the editor (validation,
  server-side scoring, scoped history replacement, rollback, session invalidation) and group-scoped resets.

## Persistence

- **Location:** `Database:Path` in `appsettings.json` (default `App_Data/retain-phase7.db`).
  A relative path resolves against the content root, which `dotnet run --project DopingRetain.Api`
  sets to the project folder from any shell directory, so the default database is
  `backend/DopingRetain.Api/App_Data/retain-phase7.db`. An absolute path is used as is.
  The startup log prints the database path in use.
- **Schema:** created with `EnsureCreated` on startup; there are no migrations.
  `EnsureCreated` does nothing when the file already exists, so earlier schema changes used a new file.
  If the configured file lacks a table or column of the current schema, startup fails and lists the
  missing columns without altering the file. One exception is additive: a Phase 6 file that lacks only
  `ReviewSessions.PerfectReviewStreak` gets that column (`INTEGER NOT NULL DEFAULT 0`) on startup, in one
  transaction, and keeps every row. Existing completions read 0, so earlier history never counts toward
  the extended review interval. Files are never deleted or reseeded.
- **Seed (YKS scenario):** runs only when the database has no student; the YKS reset restores the same
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
  database gets the new scenario only through the explicit YKS reset (`POST /api/demo/groups/yks/reset`).
- **Seed (sixth-grade scenario):** added with the first seed, or on startup while the group has no students.
  The eight topics keep their curriculum catalog `topic_id` as Id, with subject (`category`), `unitId` and
  `unitName`: Türkçe 274513 (Dilimizin Zenginliği / Dinleme-İzleme) and 274523 (Farklı Dünyalar / Yazma),
  Matematik 274839 (Asal Sayılar) and 274894 (Üçgenin Açıları), Fen Bilimleri 275020 (Güneş Sistemi) and
  275040 (Yoğunluk), Sosyal Bilgiler 274665 (Konum Özellikleri) and 274669 (İlk Türk Devletleri). Each topic
  has a 10-question normal test (`NormalTestQuestions`), three Hap Bilgi reminders and a three-question mini
  quiz where question i checks reminder i (`Data/SixthGradeContent.cs`). The five students get only
  **synthetic** normal-test results with their ten answers (`NormalTestAnswers`), each a fixed number of
  days before one reference instant (`Data/SixthGradeSeed.cs`); no mini quizzes or sessions, so every weekly
  count starts at zero. Recommendations are never stored; the model evaluates them from these results.
- **Earlier databases** (kept, no longer used by default):
  - `App_Data/retain-phase6.db`: Phase 6 schema, one YKS student. Phase 7 added `Students.Group`,
    `Students.SortOrder`, `Topics.Group/UnitId/UnitName` and the `NormalTestQuestions` and
    `NormalTestAnswers` tables, so it is not used or altered; its saved YKS history is **not migrated** and
    stays only in that file. The new file starts from the initial scenarios of both groups.
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

## Review scheduling policy

The curve above is the baseline. `Memory/ReviewSchedulePolicy.cs` (pure) is a separate product rule that
decides which review time applies after a reminder mini quiz; it never changes h, the threshold or the
estimated recall. "On time" means completed at or after the topic's current recommended review time
(a due, support-needed or no-data topic is always on time).

| Mini quiz | Next review | Perfect sequence |
|---|---|---|
| Perfect, on time, first of a sequence | formula: 10 days | 1 |
| Perfect, on time, after an on-time perfect one | `ReviewSchedule:ExtendedIntervalDays` (30) after completion; never longer | +1 |
| Perfect, early | unchanged: no extension, no shortening, no postponement | unchanged |
| 2/3 (early or on time) | formula: ≈ 4.15 days | reset to 0 |
| 0/3 or 1/3 (early or on time) | now (support needed) | reset to 0 |

- Only completed reminder mini quizzes count. A normal test never counts as a perfect review.
- A newer normal test becomes the latest observation and uses the formula; any extended schedule and the
  sequence no longer apply, so the next mini quizzes start a fresh sequence. A normal test completed
  before the latest result (submitted late) changes nothing.
- Persisted state: each completed session stores its effective `NextReviewAt` (existing column) and
  `PerfectReviewStreak`. While a mini quiz is the topic's latest result, its session's `NextReviewAt`
  decides `scheduled` vs `due` for topic queries, the due list and the homepage; recall (for ordering) and
  `support_needed` still come from the curve. Completion is idempotent, so a retried completion returns
  its stored outcome and never advances the sequence.
- `ExtendedIntervalDays` (default 30) is a configurable product policy, not a learned half-life or a
  scientific result. It must be at least the formula's interval for a perfect score,
  `h · log2(1 / threshold)` from the configured `RetentionModel` values (10 days by default); otherwise
  startup fails before the database is opened.

## Normal test results (demo integration contract)

`POST /api/students/{studentId}/topics/{id}/test-results` with `{ "correctCount", "totalQuestions", "completedAt" }`
(ISO 8601 with an offset) records a normal test for that student. Like quiz completion it only appends. It is a minimal demo contract,
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
| GET    | `/api/students` | All students with `group` (`yks` / `sixth_grade`), YKS first, each group in order (the first is its default), and `demoEnabled` |
| GET    | `/api/students/{studentId}/topics` | The student's topics evaluated at one instant (`evaluatedAt`), in display order |
| GET    | `/api/students/{studentId}/topics/{id}` | One topic with its micro-review content |
| GET    | `/api/students/{studentId}/weekly-summary` | Read-only summary of the current Monday–Sunday week (see below) |
| POST   | `/api/students/{studentId}/topics/{id}/test-results` | Demo contract: record a normal test, `201` (`400 invalid_request` / `invalid_score` / `future_completion`) |
| POST   | `/api/students/{studentId}/topics/{id}/review-sessions` | Start or resume a review session (see below) |
| POST   | `/api/students/{studentId}/review-sessions/{id}/answers` | Record one answer and return its feedback (see below) |
| POST   | `/api/students/{studentId}/review-sessions/{id}/complete` | Complete a fully answered session (see below) |
| POST   | `/api/demo/groups/{group}/reset` | `Demo:Enabled` only: reset one group (`yks` or `sixth_grade`), `204` |
| GET, PUT, DELETE | `/api/demo/...` | `Demo:Enabled` only: sixth-grade demo editor (see below) |

Every student-specific route names its student; there is no authentication, the id is the demo selection.
An unknown student gives `404 student_not_found`; a topic of the other group `404 topic_not_in_group`; a missing
or unassigned topic `404 topic_not_found`; a session of another student `404 session_not_found`. Starting or
answering never stores a test result or a review date; only completion does.

| Method | Path | Success | Errors |
|--------|------|---------|--------|
| POST | `/api/students/{studentId}/topics/{id}/review-sessions` | `201` new session, `200` existing open session | `404 topic_not_found` / `topic_not_in_group`, `409 topic_has_no_questions` |
| POST | `/api/students/{studentId}/review-sessions/{id}/answers` | `200` feedback (also for an identical retry) | `400 invalid_request` / `question_not_in_session` / `option_out_of_range`, `404 session_not_found`, `409 session_closed` / `session_abandoned` / `answer_conflict` |
| POST | `/api/students/{studentId}/review-sessions/{id}/complete` | `200` outcome (the stored one on every retry) | `404 session_not_found`, `409 session_incomplete` / `stale_session` / `session_abandoned` |

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
  session's `Completed` status, `CompletedAt`, `NextReviewAt` and `PerfectReviewStreak` (from the review
  scheduling policy) and the outcome snapshot are saved in one transaction. A completed session returns that snapshot unchanged and stores nothing.
- The outcome has `sessionId`, `score`, `total`, `reviewStatus`, `testResult`, `completedAt`,
  `nextReviewAt` and `updatedTopic` (`id`, `title`, `category`, `lastReviewedAt`).
- Concurrency: completion runs in a `BEGIN IMMEDIATE` transaction, so it and answer inserts never
  interleave; an answer is inserted only while its session is `Open` (one SQL statement). Completion
  writes only if the topic state's `Version` still equals the version stored when the session
  started, then increments it; otherwise it returns `409 stale_session` and writes nothing.
- Errors use one body shape: `{"code": "...", "message": "..."}`.

### Demo endpoints (`Demo:Enabled`)

The reset and editor endpoints exist only when `Demo:Enabled` is `true` (default `false`;
`appsettings.Development.json` sets `true`); otherwise they return `404`. To enable them for a presentation
in another environment, set it in configuration, for example `Demo__Enabled=true dotnet run --project
DopingRetain.Api` or `--Demo:Enabled=true`. The editor reveals answer keys, so keep it off elsewhere.

**Reset.** `POST /api/demo/groups/{group}/reset` (`yks` or `sixth_grade`) resets one group only; no route resets
both. In one `BEGIN IMMEDIATE` transaction it deletes that group's students' test results (with answers), review
sessions and their answers, aligns their topics with the group's demo topics, clears `LastReviewedAt`, re-adds
the group's synthetic normal-test results for one timestamp of the app clock and increments every topic state's
`Version`, so nothing captured before can match afterwards. Content and student records stay. Answers and
completions that commit first are undone; ones that run after it find their session gone (`404 session_not_found`).

**Sixth-grade editor** (`group_not_editable` 409 for YKS students):

| Method | Path | Result |
|--------|------|--------|
| GET | `/api/demo/sixth-grade` | The five students with their eight evaluated topics and latest normal test |
| GET | `/api/demo/students/{studentId}/topics/{id}/normal-test` | The ten questions with answer keys and the student's saved answers (`saved` is null without a normal test) |
| PUT | `/api/demo/students/{studentId}/topics/{id}/normal-test` | `{ "answers": [{ "questionId", "selectedIndex" }] × 10, "completedAt": "2026-10-20T14:00+03:00" }` |
| DELETE | `/api/demo/students/{studentId}/topics/{id}/history` | "No test history": the topic gets `insufficient_data` |

Saving computes the score from the answers (it cannot be sent) and validates the answer count, unknown and
duplicate question ids, option indexes and the completion time (ISO 8601 with `Z` or an offset, not in the
future). It then **replaces all history of that one student and topic** in one transaction: normal tests with
their answers, mini quiz results, review sessions and session answers are deleted, `LastReviewedAt` is cleared,
`Version` is incremented and one normal test with its ten answers is inserted. Clearing does the same without
inserting. So an edit also removes that topic's mini quizzes from the weekly summary. Other topics and students
are not touched. Normal quiz submission stays append-only.

`GET /api/students/{studentId}/topics/{id}` returns the same fields for one topic plus `questionCount`, `summaryPoints`
(`icon`, `tag`, `text`, optional `note`, optional `detail`) and `evaluatedAt`. `detail` is a longer
explanation of the same point; the frontend's reinforcement flow (a topic opened from the support-needed
list: optional lesson video, the three explanations, then the same mini quiz) shows it step by step.
That flow completes an ordinary review session, so every scheduling rule above applies unchanged. The summary points are static demo content
served from `SeedData`, not stored in the database. Quiz answers and explanations are not included.

`GET /api/students/{studentId}/weekly-summary` returns `firstDate`, `lastDate` and `today` (Istanbul calendar dates),
`distinctTopicCount` (distinct topics over the whole week, the same number as `weeklyReviewedTopicCount`),
`completedReviewCount`, `activeDayCount` and seven `days` (Monday to Sunday) with `date`, `distinctTopicCount` and
`topics`: one row per topic with `reviewCount` and the day's latest completion (`lastCompletedAt`, `correctCount`,
`totalQuestions`). Both endpoints count the same rows (`Topics/ReviewWeek.cs`): the student's `Completed` review
sessions (normal reviews and reinforcements alike) from Monday 00:00 to the next Monday 00:00 in Europe/Istanbul and
not after the evaluation instant. Normal tests and open or abandoned sessions never count; a retried completion is
one session, so it counts once. Timestamps stay UTC; only the grouping into days and weeks uses Istanbul time.

`GET /api/students/{studentId}/topics` returns, per topic, `id`, `title`, `category`, `unitName` (null for YKS), `reviewStatus` (`insufficient_data` /
`scheduled` / `due` / `support_needed`), `latestTest` (`testType`, `completedAt`, `correctCount`,
`totalQuestions`), `nextReviewAt` and `lastReviewedAt`, plus `dueTopicIds` (due topics in review priority
order: lowest unrounded recall first, ties in display order) and `weeklyReviewedTopicCount`.
Quiz answers and explanations are never included.
