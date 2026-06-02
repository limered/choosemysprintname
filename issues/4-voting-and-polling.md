# 4 - Cast one vote per nickname with live tallies via polling

## Parent PRD

prd/sprint-name-voter.md

## What to build

Voting in a session without a timer yet. Each joined nickname can cast exactly one vote for one candidate. The Session Room view polls the session state every 1-2 seconds and renders the current vote tallies per candidate. Vote totals are visible to everyone; individual votes remain anonymous. Attempting to vote a second time with the same nickname is rejected by the backend.

Extends `SessionManager` with vote casting and tally state. Unit tests for `SessionManager` start here (one-vote-per-nickname enforcement, tally correctness).

## Acceptance criteria

- [ ] Clicking a candidate casts a vote for the current nickname
- [ ] Backend endpoint to cast a vote for a candidate in a session
- [ ] One-vote-per-nickname is enforced server-side; second attempt is rejected
- [ ] Session state response includes per-candidate vote counts
- [ ] Frontend polls session state every 1-2 seconds and re-renders tallies
- [ ] `SessionManager` unit tests cover: vote recorded, duplicate vote rejected, tally aggregation
- [ ] Voter identities are not exposed in the session state response

## Blocked by

- Blocked by #3

## User stories addressed

- User story 9
- User story 14
- User story 19
