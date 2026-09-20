# Recovery exercises

## Service Bus outage during agreement acceptance

The broker recovery drill proves that the origination API can accept an offer
while Azure Service Bus is unavailable, retain the resulting integration event
in its outbox, and deliver it after the broker returns.

Run the complete local platform, then execute:

```bash
make recovery-drill
```

The drill:

1. creates a priced synthetic application;
2. stops the local Service Bus emulator;
3. accepts the offer while the broker is unavailable;
4. restarts the emulator;
5. waits for the activation worker to create the workflow from the recovered
   `AgreementCreated` event;
6. returns a JSON result containing the synthetic application and agreement
   identifiers.

The drill fails unless the recovered workflow reaches `AwaitingDeposit`.
The script always attempts to restart the broker before exiting.

## Current persistence boundary

The local outbox, service repositories, and workflow inbox are in-memory
implementations. The outage exercise therefore covers a broker interruption
without restarting the producing or consuming application containers. A
process restart loses that process's synthetic state. The Azure deployment
design replaces these stores with service-owned PostgreSQL databases and
transactional outbox/inbox tables.

This limitation is explicit so the local exercise is not presented as a
database disaster-recovery test.

## Operator response

1. Confirm the broker and SQL dependency are healthy.
2. Inspect producer logs for retry exhaustion and the correlation ID.
3. Confirm the consumer subscription is active and inspect dead letters.
4. Restore the dependency before replaying messages.
5. Verify aggregate sequence and stable message ID before replay.
6. Record the replay reason and reviewer identity.
7. Confirm the business projection and source aggregate agree.
