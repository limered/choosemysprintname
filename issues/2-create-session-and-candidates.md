# 2 - Create session with letter and see all candidates with sprites

## Parent PRD

prd/sprint-name-voter.md

## What to build

End-to-end flow for creating a session: the user picks a letter on a "Create Session" view, the backend creates an in-memory session, calls PokeAPI live to fetch all Pokemon whose name starts with that letter (with sprite URLs), and the user lands on a Session Room view showing the candidate grid with sprite icons. No nicknames, voting, or timer yet.

Backend introduces the `PokemonCatalog` deep module (per PRD Modules section) and the beginning of `SessionManager` (creating + fetching a session). `PokemonCatalog` is unit-tested with a fake PokeAPI HTTP client.

## Acceptance criteria

- [ ] Create Session view with letter input/picker
- [ ] Backend endpoint to create a session given a letter, returning the session id
- [ ] Backend endpoint to fetch session state by id, including candidates with `{ name, spriteUrl }`
- [ ] `PokemonCatalog` module fetches Pokemon by letter from PokeAPI live and extracts sprite URLs
- [ ] `PokemonCatalog` accepts an excluded-names list (passed empty for now)
- [ ] `PokemonCatalog` unit tests cover: letter filtering, exclusion list applied, sprite URL extraction, empty result case
- [ ] Session Room view renders all candidates as a grid of sprites + names
- [ ] After clicking "Create", the browser navigates to the Session Room

## Blocked by

- Blocked by #1

## User stories addressed

- User story 1
- User story 6
- User story 7
- User story 21
