# 5 - Configurable timer with start, +1 min extend, and auto-resolve winner

## Parent PRD

prd/sprint-name-voter.md

## What to build

Add the timer and winner resolution. Any participant can start the timer by entering a duration in seconds. Once started, anyone can extend it by 60 seconds via a "+" button. The live countdown is shown in the Session Room (updated via the existing polling). When the timer reaches zero, the backend resolves the round: if there is a single highest-voted candidate, the session moves to `Finished` and the winner is displayed prominently in the UI. Ties are handled in issue #6.

Extends `SessionManager` with timer state and the state-machine transitions described in the PRD.

## Acceptance criteria

- [ ] "Start timer" UI accepts a custom duration (in seconds) and starts the round
- [ ] "+1 min" button extends the running timer by 60 seconds
- [ ] Session state response includes timer-remaining and phase (`Lobby` / `Voting` / `Finished`)
- [ ] Timer counts down live in the UI based on polled state
- [ ] On expiry with a unique top candidate, session transitions to `Finished` with that winner
- [ ] Winner is displayed prominently in the Session Room when `Finished`
- [ ] Voting endpoint rejects votes when the session is not in `Voting` phase
- [ ] `SessionManager` unit tests cover: start timer, extend timer, expiry resolves unique winner, votes rejected outside `Voting`

## Blocked by

- Blocked by #4

## User stories addressed

- User story 10
- User story 11
- User story 12
- User story 13
- User story 15
