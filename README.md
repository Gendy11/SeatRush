# SeatRush

Flash-sale cinema ticketing. When a hot showing goes on sale, thousands of people click the same seats in the same second. SeatRush is built to stay correct and responsive in that moment.

- **Live seat map** (3D and 2D). Seats turn held or booked for everyone in real time.
- **7-minute booking session** per user. Seats you hold are yours until it ends, then they free themselves.
- **Waiting room** for hot showings. Users get a place in line and are let in a few at a time.
- **Zero overselling.** The database refuses a double sale, whatever the app code does.
- **Built-in demo.** "Simulate rush" sends server-side bots through the real flow, and "Reset" cleans up after them.

> Built as if it ran at large scale (many API instances, sharded Redis, heavy contention), even though it's deployed as a single free-tier instance.

## Stack

| Layer | Tech |
|---|---|
| API | .NET 10, ASP.NET Core, modular monolith |
| Database | PostgreSQL 17 (EF Core, snake_case) |
| Fast shared state | Redis 7 (holds, sessions, waiting room, refresh tokens, SignalR backplane) |
| Real time | SignalR, one group per showing |
| Frontend | Angular 21 (zoneless, signals), three.js for the 3D hall |
| Tests | xUnit + Testcontainers (real Postgres and Redis) |

## Run it locally

Prerequisites: Docker, .NET 10 SDK, Node 20+.

```bash
# 1. Postgres (port 5433) and Redis (port 6379)
docker compose up -d

# 2. API on http://localhost:5286. Applies migrations and seeds venues, movies and showings on startup.
dotnet run --project SeatRush

# 3. Frontend on http://localhost:4200
cd client
npm install
npm start
```

Open http://localhost:4200, pick a showing and click seats. To watch live updates, open the same showing in a second browser (a private window is a different guest).

### Tests

```bash
dotnet test           # integration tests; Docker must be running (Testcontainers)
cd client && npm test # frontend unit tests
```

## How it works

### Project layout

```
Core/             entities, interfaces, models (no dependencies)
Infrastructure/   EF Core + migrations, Redis services, seed data, demo bots
SeatRush/         API: controllers, SignalR hub, rate limiting, background admitter
IntegrationTests/ tests against real Postgres and Redis
client/           Angular app
docs/             requirements.md (functional and non-functional requirements)
```

### Who owns what

| Data | Store | Why |
|---|---|---|
| Venues, movies, showings, booked seats, bookings | **Postgres** | Source of truth for sales. Must never be wrong. |
| Booking sessions (7 min) | Redis, TTL | Short-lived, hot, expires by itself |
| Seat holds | Redis, TTL | Thousands of claims per second. Freed when the session ends. |
| Waiting room line, entry passes | Redis sorted sets | Ordered line, cheap rank lookups |
| Refresh tokens | Redis, TTL (hash only) | Rotation and revocation |
| Seat change counter (`seq`) | Redis | Orders live updates |

All of one showing's Redis keys share the hash tag `{e:<eventId>}`, so on a Redis cluster they live on one shard and multi-key transactions stay possible.

### The booking flow

```
showing ─▶ (waiting room, hot showings only) ─▶ session (7 min) ─▶ hold seats ─▶ checkout ─▶ booking
```

1. **Session.** Opening the seat map starts a 7-minute session in Redis. Reopening it returns the same deadline.
2. **Hold.** Clicking a seat runs a Redis transaction: `SET hold NX` with the session's TTL. If two people click at the same instant, exactly one wins. Holds aren't cleaned up by a job; they expire with the session.
3. **Live update.** Every hold, release and booking increments the showing's `seq` and is pushed to the showing's SignalR group. Clients ignore anything older than what they've seen. After a reconnect they refetch the map.
4. **Checkout.** Pay at the counter. A single Postgres transaction marks the seats Booked with a conditional update (`WHERE status = Available`). If any seat was already taken, the whole booking rolls back. Unique indexes are a second safety net. An `Idempotency-Key` header means a retried request returns the original booking instead of booking twice.

### Waiting room (hot showings)

Showings flagged `HasWaitingRoom` (the IMAX showings of The Odyssey) put users in a line first.

- **Joining** gives you a ticket number (Redis `INCR`) and a place in a sorted set. With no crowd you walk straight in.
- **The admitter** is a background service. Every second it lets the lowest tickets in while fewer than 50 people are inside. Each admitted user gets a 60-second entry pass.
- **Polling.** Clients poll their place every ~3s (with jitter) and the tab title flashes when it's their turn.
- **Starting a session** on a hot showing without a pass returns **403**.
- **Silence isn't punished.** Users who stop polling (background tab, flaky network) are skipped, not removed. So are users whose pass expires unused. When they come back, they get their original ticket, at the front of the line.
- **One bouncer at a time.** A short Redis lock lease ensures only one admitter acts, even with many API instances.

### Identity

Anonymous guests, no sign-up:

- `POST /api/auth/token` issues a 15-minute JWT access token, kept in memory by the client, and a 30-day refresh token in an HttpOnly, SameSite=Strict cookie.
- Refresh tokens are stored only as SHA-256 hashes in Redis and rotate on every use. Replaying a used one revokes the whole token family.
- Access tokens are validated statelessly (signature, issuer, audience, expiry), with no lookup per request.

### Protecting the system

- **Waiting room.** Sheds load before it reaches the seat map.
- **Rate limiting** (ASP.NET built-in, in-memory, per instance), returning **429** with `Retry-After`:

  | Policy | Limit |
  |---|---|
  | Global | 300/min per IP |
  | Guest creation | 20/min per IP |
  | Holds | burst of 20, then 2/s, per user |
  | Checkout | 10/min per user |

- **Redis down → fail closed.** Seat selection returns **503**, while browsing (Postgres) keeps working. Overselling is impossible either way, because Postgres has the final say. The API starts without Redis and reconnects on its own.

### Demo: simulate rush and reset

On any seat map:

- **Simulate rush** (normal showings): a bot walks in every 5–15s, picks seats near the centre, and usually books them.
- **Simulate extreme rush** (hot showings): 300 bots flood the waiting room. You're put in line behind ~150 of them, then watch the hall fill up live once you're in.
- **Reset**: stops the bots and deletes their bookings, while real users' bookings are kept. It clears holds, sessions and the line, and every viewer reloads live.

Bots go through the **same services as real users**: waiting room, holds, sessions, checkout. Their user ids share a fixed prefix, so they can be found and cleaned up. A rush stops after 5 minutes or when the showing sells out, and each showing has a 30-second cooldown between rushes.

## API

| Method | Route | |
|---|---|---|
| POST | `/api/auth/token` | Get or refresh a guest token |
| GET | `/api/movies` | Movies |
| GET | `/api/events` | Upcoming showings |
| GET | `/api/events/{id}` | Showing details |
| GET | `/api/events/{id}/seats` | Seat map + `seq` |
| POST | `/api/events/{id}/session` | Start or resume the booking session |
| POST / GET | `/api/events/{id}/queue` | Join the line / place in line |
| POST / DELETE | `/api/seats/{seatId}/hold` | Hold / release a seat |
| POST / GET | `/api/bookings` | Check out (`Idempotency-Key` required) / my tickets |
| POST / GET / DELETE | `/api/demo/events/{id}/rush` | Start / status / stop a rush |
| POST | `/api/demo/events/{id}/reset` | Reset a showing |
| WS | `/hubs/seats` | Live seat changes |

## Tradeoffs

| Decision | Why | Cost |
|---|---|---|
| **Modular monolith**, no microservices or message queues | One deployable, simple to reason about. The hot path is Redis + one Postgres transaction. | Modules scale together |
| **Holds in Redis, bookings in Postgres** | Redis absorbs the clicking storm, and Postgres only sees real checkouts | Two stores to keep consistent. Postgres wins any disagreement. |
| **Hold expiry via TTL**, not a cleanup job | Nothing to crash or fall behind. Seats free exactly on time. | Expiry is a Redis event, not a pushed update. Clients use the session deadline. |
| **Waiting room polling**, not push | Stateless, cache-friendly, survives reconnects. Jitter spreads the load. | ~3s latency on your place in line |
| **Skip-and-remember** for silent users | Background tabs and bad networks aren't punished | A returning user jumps ahead of people who joined later |
| **In-memory rate limiter** | Zero dependencies. Good enough per instance. | Limits are per instance, not global |
| **Rush simulator runs in-process** | Simple demo with no extra infrastructure | With multiple instances it would need a Redis lease to run once |
| **Anonymous guests** | No sign-up friction for a demo | Losing Redis data loses refresh tokens, so guests lose access to their past tickets (the bookings stay in Postgres) |
| **Redis without persistence locally** | Simple dev setup | Removing the container wipes holds and guest identities. Production would use managed Redis with persistence and replicas. |

## Requirements

Full functional and non-functional requirements, with status: [docs/requirements.md](docs/requirements.md).

**Not done yet:** k6 load test (10,000 users on one 500-seat showing, hold p95 < 200 ms), a Redis-down integration test, deployment, and running the API and frontend in docker-compose too.
