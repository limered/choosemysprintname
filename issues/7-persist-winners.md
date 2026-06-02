# 7 - Persist winners to SQLite and exclude from future sessions

## Parent PRD

prd/sprint-name-voter.md

## What to build

Introduce the only persistent piece of state: the winners table in SQLite. When a session transitions to `Finished`, the winning Pokemon name is saved via a new `WinnerHistoryStore` module. When a new session is created, `SessionManager` passes the list of all past winner names to `PokemonCatalog` as the exclusion list, so previously-won Pokemon are not shown as candidates again.

This is independent of the tie-breaker work and depends only on issue #5 (winners must exist before they can be persisted).

## Acceptance criteria

- [ ] `WinnerHistoryStore` module persists winner names to SQLite and retrieves all of them
- [ ] SQLite schema/table is created automatically on first run
- [ ] On session `Finished`, the winner name is saved via `WinnerHistoryStore`
- [ ] On session creation, past winner names are passed as the exclusion list to `PokemonCatalog`
- [ ] Candidate list in new sessions does not include any past winners
- [ ] Persistence survives a backend restart (verified by creating, finishing, restarting, creating again)
- [ ] `SessionManager` unit tests cover: winner is saved to the store on `Finished`, exclusion list is propagated on session creation (using a fake store)

## Blocked by

- Blocked by #5

## User stories addressed

- User story 8
- User story 18
