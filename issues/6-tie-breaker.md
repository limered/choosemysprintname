# 6 - Tie-breaker rounds, timed and repeating

## Parent PRD

prd/sprint-name-voter.md

## What to build

Handle ties at timer expiry. If multiple candidates are tied for the highest vote count when the timer ends, the session transitions to a `TieBreaker` phase containing only the tied candidates with votes reset. A new timer duration is entered and started. If the tie-breaker also ties, another tie-breaker round starts. This repeats until a single winner emerges, at which point the session transitions to `Finished`.

Extends `SessionManager` with the `TieBreaker` state transition described in the PRD's state machine.

## Acceptance criteria

- [ ] On timer expiry with a tie, session transitions to `TieBreaker` with only tied candidates and reset votes
- [ ] UI shows the tie-breaker candidates and prompts for a new duration to start the round
- [ ] Tie-breaker round behaves identically to a normal voting round (start, extend, resolve)
- [ ] A tie-breaker that also ties triggers another tie-breaker round
- [ ] When a tie-breaker resolves to a single winner, session transitions to `Finished`
- [ ] Existing one-vote-per-nickname rule applies per round (voters re-vote each tie-breaker)
- [ ] `SessionManager` unit tests cover: single tie-breaker resolves, repeated tie-breakers, votes reset between rounds

## Blocked by

- Blocked by #5

## User stories addressed

- User story 16
- User story 17
