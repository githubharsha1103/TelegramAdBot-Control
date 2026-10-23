# Telegram control bot routes

## Entry points

| Input | Destination / action |
| --- | --- |
| `/start`, `/panel`, `/dashboard`, `Main Menu` | Dashboard; reply keyboard is sent once and reused |
| `/status`, `📊 Status` | Status view |
| `/instance`, `🖥 Instances` | Manual instance manager |
| `/accounts`, `👤 Accounts` | Account mapping view; Configure opens the promotion workflow |
| `/bots`, `🤖 Bots` | Promotion bot manager |
| `/pause`, `/resume`, `⏸ Pause / Resume` | Pause or resume; returns status and dashboard |
| `/stop`, `🛑 Stop Promotion` | Confirmation screen, then stop |
| `/help` | Command list |

## Promotion workflow

```text
Dashboard --Configure--> Bots --Continue--> Instances --Continue--> Accounts
    ^                        ^                  |                       |
    |                        |                  +--Back-----------------+
    |                        +--Back             |                       |
    |                                           +--Continue--> next selected instance accounts
    |                                                              |
    |                                      last instance --Continue--> Cycles
    |                                                              |
    |              Instances <--Back-- Cycles <--Back-- Confirm <--valid cycles input
    |                           ^                         |
    +--Dashboard (any step)-----+-------------------------+
```

Selections and account mappings remain in the chat session while navigating Back. Continue validates bot selection, instance selection, and each selected instance's account mapping. Confirm is the only workflow route that starts a promotion. Cancel clears the session. A repeated Configure action redraws the current step; a repeated start callback is ignored while a start is in progress.

## Other screens

| Screen | Actions and destinations |
| --- | --- |
| Dashboard | Configure, Start, Pause/Resume, Stop confirmation, Instances, Accounts, Bots, Status, Refresh |
| Status | Pause/Resume, Stop confirmation, Refresh, Manage Bots, Dashboard |
| Bot manager | Add, Remove, List, Dashboard; remove selection returns to manager; cancel returns to manager |
| Add bot prompt | Text username, `cancel` returns to bot manager, `/panel` returns to Dashboard |
| Account mappings | Configure, Dashboard |
| Manual instance manager | Toggle selections, Continue opens selected profiles, Create, Refresh, Dashboard |
| Manual instance status | Finish each open profile, Refresh, Dashboard |
| Stop confirmation | Confirm stop, Cancel returns to Status, Dashboard |

Callbacks with a stale message ID, token, or workflow revision are ignored after their callback query is acknowledged. Unsupported callback payloads are ignored. This avoids stale buttons repeating actions.

## Concurrency notes

Callbacks are acknowledged before routing. Per-chat callback state transitions are serialized. Manual browser actions reserve a separate per-chat action gate and then run without holding the callback lock. Dashboard start and confirmed stop are supervised background tasks. Promotion Pause and Resume do not wait for the browser-start command semaphore.
