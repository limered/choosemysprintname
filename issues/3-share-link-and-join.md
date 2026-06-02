# 3 - Share link and join session with nickname

## Parent PRD

prd/sprint-name-voter.md

## What to build

Make sessions joinable by URL. The Session Room view exposes a copyable share link containing the session id. When a second user opens that link, they are prompted for a nickname before entering the room. Once they submit a nickname, they see the same candidate grid as the creator. Identity is anonymous to other voters (no participant list shown).

## Acceptance criteria

- [ ] Session Room view shows a copyable share link with the session id
- [ ] Opening the share link as a new visitor shows a nickname prompt before the room
- [ ] Submitting a nickname enters the room and shows the same candidates
- [ ] Backend endpoint to join a session with a nickname
- [ ] Nicknames are stored on the in-memory session (used later for vote enforcement)
- [ ] Other voters' nicknames and votes are not exposed in the session state response

## Blocked by

- Blocked by #2

## User stories addressed

- User story 2
- User story 3
- User story 4
- User story 5
