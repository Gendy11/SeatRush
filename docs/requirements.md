# SeatRush Requirements

Cinema-style flash-sale ticketing. Venues (screens) have fixed seat layouts; events are showings in a venue at a show time (UTC).

## Functional requirements

### 1. Venues & events
| ID | Requirement | Status |
|----|-------------|--------|
| FR1 | Venues with fixed seat layouts (rows × seats per row) are seeded at startup from `venues.json` if none exist. | Done |
| FR2 | Showings (event = name + venue + show time) are seeded at startup from `events.json` if none exist. Each gets its own seat inventory copied from the venue layout. Tickets are on sale as soon as the event exists. | Done |
| FR3 | Two showings in the same venue at the same show time are rejected (unique index + seeder check). | Done |

### 2. Browsing
| ID | Requirement | Status |
|----|-------------|--------|
| FR4 | List upcoming showings (show time in the future), soonest first. | Done |
| FR5 | View a showing's seat map with each seat's status (Available / Held / Booked). Expired holds show as Available. | |

### 3. Waiting room
| ID | Requirement | Status |
|----|-------------|--------|
| FR6 | When a showing is busy, joining users get a queue position (Redis sorted set). | |
| FR7 | Users are admitted at N per second and receive a short-lived access token. | |
| FR8 | Users can see their queue position while waiting. | |
| FR9 | Hold and checkout require a valid access token while the waiting room is active. | |

### 4. Seat hold
| ID | Requirement | Status |
|----|-------------|--------|
| FR10 | Hold a seat for 5 minutes. Two users grabbing the same seat at the same instant → exactly one wins. | |
| FR11 | Max 4 held seats per user per showing. | |
| FR12 | Re-holding a seat you already hold returns the existing hold (idempotent). | |
| FR13 | Release your own hold. | |
| FR14 | Expired holds are released by a background sweeper (~15s) and treated as available on read (lazy expiry). | |

### 5. Checkout
| ID | Requirement | Status |
|----|-------------|--------|
| FR15 | Check out held seats with a fake payment (random delay, ~10% failure). | |
| FR16 | Payment success → seats Booked. Failure or timeout → seats released. | |
| FR17 | Checkout requires an `Idempotency-Key` header; a duplicate request returns the original result. | |
| FR18 | View my bookings. | |

### 6. Real-time
| ID | Requirement | Status |
|----|-------------|--------|
| FR19 | Viewers of a showing see seat status changes live (SignalR group per showing). | |

### 7. Protection
| ID | Requirement | Status |
|----|-------------|--------|
| FR20 | Rate-limit hold and checkout per user and per IP (Redis); exceeding returns 429. | |

### 8. Demo
| ID | Requirement | Status |
|----|-------------|--------|
| FR21 | "Simulate rush" button spawns ~200 server-side bot buyers for a showing. | |
| FR22 | Reset a showing's seats so the demo can be re-run. | |

### 9. Identity
| ID | Requirement | Status |
|----|-------------|--------|
| FR23 | Anonymous session: a user gets an ID without signing up (minimal JWT / session ID). | |

## Non-functional requirements

### Correctness
- **NFR1** Zero overselling: a seat is never booked twice and bookings never exceed capacity, under any concurrency. Enforced by the database (conditional UPDATE, constraints), not only app code.
- **NFR2** Idempotent checkout: retries never double-book.

### Performance (proven locally with k6)
- **NFR3** 10,000 virtual users competing for 1,000 seats → exactly 1,000 booked, 0 oversold.
- **NFR4** Hold request p95 < 200 ms under load.
- **NFR5** Seat status change reaches viewers in < 1 s.

### Resilience / backpressure
- **NFR6** Under overload, shed traffic (waiting room, 429) instead of slowing down for everyone or crashing.
- **NFR7** Hold expiry still works if the sweeper fails (lazy expiry on read).
- **NFR8** If Redis is down, sales stay correct; only the waiting room and rate limiting degrade. Postgres is the source of truth.

### Operability / deployment
- **NFR9** Single process: API, SignalR hub and background jobs together.
- **NFR10** Runs on free tiers (Vercel, Render / MonsterASP.NET, Neon, Upstash) and tolerates cold starts.
- **NFR11** Whole stack runs locally with one `docker compose up`.

### Testability
- **NFR12** Concurrency rules covered by integration tests against real Postgres (Testcontainers).

### Simplicity
- **NFR13** Modular monolith; no microservices, queues or Kubernetes. Tradeoffs documented in the README.

## Conventions
- All times are stored and returned in UTC (`timestamptz`); the frontend converts for display.
