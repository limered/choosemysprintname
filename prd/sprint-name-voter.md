# Sprint Name Voter

## Problem Statement

Our team picks a fun themed name (e.g., a Pokemon) for each sprint, but the process is ad-hoc: someone shouts a letter, people throw out suggestions in chat, voting is informal, and we sometimes accidentally reuse the same name. We need a lightweight, shared way to pick a sprint name from a category (starting with Pokemon) based on a chosen first letter, vote on the candidates with a timer, and avoid repeating previous winners.

## Solution

A small Vue + C# webapp hosted on Render. One team member starts a session by picking a letter; the app auto-populates all Pokemon starting with that letter (with their sprite icons) by calling PokeAPI live, excluding past winners stored in a local SQLite database. Team members join via a shared link, enter a nickname, and cast one anonymous vote each. Anyone can start a configurable countdown timer, and anyone can extend it by one minute. When the timer ends, the candidate with the most votes wins. Ties trigger a new timed round restricted to the tied candidates, repeating until a single winner emerges. The winner is saved to the database so it isn't suggested again.

## User Stories

1. As a team member, I want to create a new voting session by picking a letter, so that we can start choosing a sprint name immediately.
2. As a session creator, I want to receive a shareable link, so that I can invite my teammates to vote.
3. As a team member, I want to join a session via a link, so that I can participate without needing an account.
4. As a voter, I want to enter a nickname when joining, so that the system can enforce one vote per person.
5. As a voter, I want my vote to be anonymous to other voters, so that I can vote freely without social pressure.
6. As a voter, I want to see all Pokemon candidates starting with the chosen letter automatically populated, so that I don't have to type suggestions manually.
7. As a voter, I want to see each candidate's sprite icon, so that I can recognize the Pokemon visually.
8. As a voter, I want past winning Pokemon to be excluded from the candidate list, so that we don't reuse the same sprint name.
9. As a voter, I want to cast exactly one vote per round, so that the vote is fair.
10. As a team member, I want to start the countdown timer with a custom duration, so that we can time-box the voting.
11. As any participant, I want to start the timer (no admin role), so that we don't need to wait for a specific person.
12. As any participant, I want to extend the timer by one minute via a "+" button, so that we can give late voters a chance.
13. As a voter, I want to see the timer counting down live, so that I know how much time I have left to vote.
14. As a voter, I want to see live vote tallies (via polling), so that I can see the state of the vote.
15. As a voter, I want the highest-voted candidate to win automatically when the timer expires, so that no manual resolution is needed.
16. As a voter, I want a new timed tie-breaker round restricted to tied candidates when there is a tie, so that we always converge on a single winner.
17. As a voter, I want tie-breaker rounds to repeat until resolved, so that we never end with an ambiguous result.
18. As a team member, I want the winner to be persisted, so that the same Pokemon isn't suggested in future sessions.
19. As a team member, I want session state (candidates, votes, timer) to live only in memory and reset on reload, so that the app stays simple and stateless beyond winners.
20. As a developer, I want the entire app to be deployable as a single Render service, so that hosting and operations are simple.
21. As a developer, I want the backend to call PokeAPI live, so that we don't have to maintain a cached Pokemon dataset.

## Implementation Decisions

### Modules

- **PokemonCatalog** (deep module, backend)
  - Responsibility: Fetch Pokemon by starting letter from PokeAPI, filter out excluded names.
  - Interface (conceptual): `GetPokemonByLetter(letter, excludedNames)` returning a list of `{ name, spriteUrl }`.
  - Wraps the PokeAPI HTTP client; the HTTP client is injected so the module can be tested in isolation with a fake.

- **SessionManager** (deep module, backend)
  - Responsibility: Own the in-memory state machine for all active sessions.
  - State machine: `Lobby` → `Voting` → (optional `TieBreaker` loop) → `Finished`.
  - Interface (conceptual): create session, get session, join session (with nickname), cast vote, start timer (with duration), extend timer by 60s, resolve outcome when timer expires.
  - Enforces one vote per nickname per round.
  - On `Finished`, hands the winning name to `WinnerHistoryStore`.

- **WinnerHistoryStore** (thin persistence, backend)
  - Responsibility: Persist and retrieve past winner names from SQLite.
  - Interface (conceptual): save a winner name, get all winner names.
  - This is the only persistent state in the system.

- **API Controllers** (thin HTTP layer, backend)
  - Translate REST calls to `SessionManager` / `PokemonCatalog` calls.
  - Provide a single polling endpoint that returns the full session state (candidates with sprite URLs, current votes, timer remaining, phase, winner if finished).

- **Vue Frontend** (single-page app)
  - Views: Create Session (letter picker), Session Room (candidate grid with sprites, nickname entry, vote button, live timer with "+1 min" button, winner display).
  - Polls the session state endpoint every 1–2 seconds while a session is active.
  - Served as static assets by the C# backend (single Render service).

### Architectural Decisions

- **Single Render service**: The C# backend serves the built Vue SPA's static assets as well as the API.
- **In-memory sessions**: Session state is not persisted. Reloading or restarting the server wipes active sessions. Only winners persist.
- **Live updates via polling**: No WebSockets/SignalR; the frontend polls the session state endpoint.
- **Identity via nickname only**: No authentication. The nickname is the identity key for "one vote per person." No cookie or IP enforcement.
- **PokeAPI called live**: No local Pokemon cache. The backend fetches on demand when a session is created.
- **No candidate cap**: All Pokemon starting with the chosen letter (minus past winners) are shown.
- **Tie-breaker**: A new timed round including only the tied candidates. Timer duration is configurable per round. If the tie-breaker also ties, another tie-breaker round starts. Repeats until a single winner emerges.

### API Contract (high level)

- Create a new session given a letter; returns a session id used to build the share link.
- Get full session state by id (used for polling).
- Join a session by id with a nickname.
- Cast a vote in a session for a candidate name.
- Start the timer in a session with a given duration in seconds.
- Extend the timer in a session by 60 seconds.

### Schema

- A single SQLite table for past winners (name, timestamp).

## Testing Decisions

### What makes a good test

Tests cover **external behavior** of a module through its public interface, not internal implementation details. A test should still pass after a refactor that preserves behavior. Avoid coupling tests to specific call sequences, private methods, or internal data structures.

### Modules to test

- **PokemonCatalog**: Tested with a fake PokeAPI HTTP client. Verifies letter-based filtering, exclusion of past winners, sprite URL extraction, and behavior when no Pokemon match.
- **SessionManager**: Tested in isolation with a fake `PokemonCatalog` and a fake `WinnerHistoryStore`. Verifies the full state-machine: session creation, joining, one-vote-per-nickname enforcement, timer start/extend, winner resolution on timer expiry, tie-breaker rounds (single and repeated), and persistence of the winner via the store on `Finished`.

### Not tested

- **API Controllers**: Thin HTTP layer with no logic worth isolating.
- **WinnerHistoryStore**: Thin SQLite wrapper; covered indirectly via `SessionManager` integration if desired.
- **Vue frontend**: Manually verified.

### Prior art

This is a new repository, so there is no prior testing convention to follow. Tests should be set up using the standard xUnit (or NUnit) test framework that ships with the .NET ecosystem and live in a sibling test project to the backend.

## Out of Scope

- Authentication / user accounts.
- Non-Pokemon categories (movies, cities, etc.).
- A page showing the history of past sprint names.
- Real-time updates via WebSockets or SignalR.
- Persistence of active session state across server restarts.
- Manual submission of candidate names by users.
- Multiple votes, ranked-choice voting, or weighted votes.
- Admin role or permissions.
- Mobile-optimized layout beyond what naturally falls out of a small Vue app.
- Caching of the PokeAPI dataset.
- Per-IP or per-cookie vote enforcement beyond nickname uniqueness.

## Further Notes

- The "+1 minute" timer extension UI was inspired by Miro's timer widget.
- Because nicknames are the only identity, two voters could collide by entering the same nickname; this is accepted given the small-team, trust-based context.
- PokeAPI rate limits should be considered, but for a small team creating a handful of sessions per sprint, live calls are expected to be well within limits.
- The letter "Q" example produces a small number of Pokemon (e.g., Qwilfish, Quagsire, Quaxly…), validating the "show all, no cap" decision.
