# Eventing & Service Bus Implementation Plan

**Objective:** End-to-end booking automation & messaging using Azure Service Bus with transactional outbox, idempotency, multi-tenant design, and MSG91 for SMS/WhatsApp/Email.

**Scope:** Guest/Admin booking flows → confirm/cancel/check-in/check-out notifications; stay reminders (welcome, precheckout, postcheckout); WhatsApp inbound webhook (stub).

---

## Checklist of Files to Create or Edit

### 1. Event envelope & constants

- [ ] **Create** `Atlas.Api/Events/EventTypes.cs` — constants: booking.created, booking.confirmed, booking.cancelled, stay.checked_in, stay.checked_out, stay.welcome.due, stay.precheckout.due, stay.postcheckout.due, whatsapp.inbound.received
- [ ] **Create** `Atlas.Api/Events/EventEnvelope.cs` — generic `EventEnvelope<T>` with EventId, TenantId, EventType, OccurredUtc, CorrelationId, EntityId, SchemaVersion, Payload

### 2. Azure Service Bus integration

- [ ] **Add package** `Atlas.Api/Atlas.Api.csproj` — Azure.Messaging.ServiceBus
- [ ] **Create** `Atlas.Api/Options/AzureServiceBusOptions.cs` — ConnectionString, Topic names (booking.events, stay.events, whatsapp.inbound), Subscription names (notifications, scheduler, conversation-orchestrator), EnableSessions
- [ ] **Create** `Atlas.Api/Services/EventBus/IEventBusPublisher.cs` — PublishAsync(envelope, topic, sessionId?)
- [ ] **Create** `Atlas.Api/Services/EventBus/AzureServiceBusPublisher.cs` — implement; set MessageId, CorrelationId, SessionId, ApplicationProperties (tenantId, eventType, entityId, schemaVersion, idempotencyKey)
- [ ] **Create** `Atlas.Api/Services/EventBus/InMemoryEventBusPublisher.cs` — for tests (captures published messages)

### 3. Outbox schema & entity

- [ ] **Edit** `Atlas.Api/Models/OutboxMessage.cs` — add Topic, Status (Pending/Published/Failed), NextAttemptUtc, CorrelationId, EntityId, OccurredUtc, SchemaVersion; keep AggregateType/AggregateId for backward compat or map to Topic/EntityId
- [ ] **Create** migration** — add columns and index (Status, NextAttemptUtc), (TenantId, CreatedUtc)
- [ ] **Edit** `Atlas.Api/Data/AppDbContext.cs` — configure new OutboxMessage properties and indexes

### 4. Outbox dispatcher hosted service

- [ ] **Create** `Atlas.Api/Services/Outbox/OutboxDispatcherHostedService.cs` — poll pending OutboxMessage (Status=Pending, NextAttemptUtc <= UtcNow), concurrency-safe (status transition/row lock), publish via IEventBusPublisher, mark Published/Failed, exponential backoff, max attempts
- [ ] **Edit** `Atlas.Api/Program.cs` — register AzureServiceBusOptions, IEventBusPublisher (Azure or InMemory by env), OutboxDispatcherHostedService

### 5. Wire booking/payment/cancel/check-in/check-out to outbox

- [ ] **Edit** `Atlas.Api/Controllers/BookingsController.cs` — on create/update when Confirmed: insert OutboxMessage (topic booking.events, eventType booking.confirmed); on Cancel: insert booking.cancelled; on CheckIn: insert booking.confirmed or stay.checked_in + schedule rows (see 8); on CheckOut: insert stay.checked_out
- [ ] **Refactor** `EnqueueBookingConfirmedWorkflowAsync` — only insert one OutboxMessage (booking.confirmed) with EventEnvelope payload; remove direct CommunicationLog creation from controller (consumer will create logs)
- [ ] **Edit** `Atlas.Api/Controllers/BookingsController.cs` — Cancel: after SaveChanges, insert OutboxMessage booking.cancelled
- [ ] **Edit** `Atlas.Api/Controllers/BookingsController.cs` — CheckIn: after SaveChanges, insert OutboxMessage stay.checked_in; CheckOut: insert stay.checked_out
- [ ] **Edit** payment flow (if Lead→Confirmed on payment)** — ensure PaymentsController or booking status update inserts OutboxMessage booking.confirmed when status becomes Confirmed (may be in BookingsController PUT already)

### 6. Notification module (MSG91)

- [ ] **Create** `Atlas.Api/Services/Notifications/INotificationProvider.cs` — SendSmsAsync, SendWhatsAppAsync, SendEmailAsync
- [ ] **Create** `Atlas.Api/Services/Notifications/Msg91NotificationProvider.cs` — implement with HttpClient, MSG91 APIs, resilient policies (Polly or retry)
- [ ] **Create** `Atlas.Api/Services/Notifications/NotificationOrchestrator.cs` — map event types to template keys and channels; call provider; log to CommunicationLog with IdempotencyKey (insert before send; on unique violation skip)
- [ ] **Edit** `Atlas.Api/Services/Msg91Settings.cs` — extend if needed for WhatsApp/Email MSG91 endpoints
- [ ] **Edit** `Atlas.Api/Program.cs` — register INotificationProvider (Msg91), NotificationOrchestrator; bind options

### 7. CommunicationLog idempotency

- [ ] **Verify** `Atlas.Api/Models/CommunicationLog.cs` — has TemplateKey (or use TemplateId), IdempotencyKey unique per tenant (already IX_CommunicationLog_TenantId_IdempotencyKey)
- [ ] **Edit** idempotency key format** — use deterministic key per event+booking+channel+template e.g. `{eventId}:{bookingId}:{channel}:{templateKey}` so duplicate delivery does not send twice

### 8. Service Bus consumers (hosted services)

- [ ] **Create** `Atlas.Api/Services/Consumers/BookingEventsNotificationConsumer.cs` — subscribe to booking.events / notifications, session-enabled; handle booking.confirmed, booking.cancelled; call NotificationOrchestrator; idempotent via CommunicationLog IdempotencyKey
- [ ] **Create** `Atlas.Api/Services/Consumers/StayEventsNotificationConsumer.cs` — subscribe to stay.events / notifications; handle stay.welcome.due, stay.precheckout.due, stay.postcheckout.due
- [ ] **Create** `Atlas.Api/Services/Consumers/WhatsAppInboundConsumerStub.cs` — subscribe to whatsapp.inbound / conversation-orchestrator; log only
- [ ] **Edit** `Atlas.Api/Program.cs` — register consumers (when Service Bus enabled); MaxConcurrentSessions from options

### 9. AutomationSchedule & scheduler

- [ ] **Edit** `Atlas.Api/Models/AutomationSchedule.cs` — add TimeZoneId, NextAttemptUtc if missing
- [ ] **Create** migration** — add TimeZoneId, NextAttemptUtc to AutomationSchedule if needed
- [ ] **Create** `Atlas.Api/Services/Scheduler/AutomationSchedulerHostedService.cs` — poll AutomationSchedule where Status=Pending and DueAtUtc/NextAttemptUtc <= UtcNow; publish stay.*.due to Outbox (or direct); mark Published
- [ ] **Edit** `Atlas.Api/Controllers/BookingsController.cs` — on CheckIn (or API that sets check-in): create AutomationSchedule rows for welcome, precheckout, postcheckout using listing timezone (Listing.CheckInTime/CheckOutTime or default)
- [ ] **Edit** `Atlas.Api/Data/AppDbContext.cs` — AutomationSchedule index for (Status, NextAttemptUtc) or (Status, DueAtUtc)

### 10. WhatsApp inbound webhook

- [ ] **Create** `Atlas.Api/Controllers/WebhooksController.cs` or **Add** to existing — POST /webhooks/whatsapp/inbound; validate; persist WhatsAppInboundMessage; insert OutboxMessage (topic whatsapp.inbound, event whatsapp.inbound.received)
- [ ] **Verify** `Atlas.Api/Models/WhatsAppInboundMessage.cs` — has minimal fields (PayloadJson, FromNumber, etc.)

### 11. Configuration & DI

- [ ] **Edit** `Atlas.Api/appsettings.json` — add AzureServiceBus section (ConnectionString placeholder, Topics, Subscriptions, EnableSessions)
- [ ] **Edit** `Atlas.Api/Program.cs` — bind AzureServiceBusOptions; register IEventBusPublisher (Azure when connection string present, else InMemory for dev/tests); register OutboxDispatcherHostedService; register consumers (conditional); register scheduler; apply migrations in test bootstrap (existing IntegrationTest setup)

### 12. Tests

- [ ] **Create** `Atlas.Api.Tests/Events/EventEnvelopeTests.cs` — envelope serialization, idempotency key format
- [ ] **Create** `Atlas.Api.Tests/Services/NotificationOrchestratorTests.cs** — template mapping, idempotency (mock provider)
- [ ] **Create** `Atlas.Api.IntegrationTests/OutboxDispatcherTests.cs` — booking confirmed creates OutboxMessage; dispatcher marks Published when fake bus publish succeeds
- [ ] **Create** `Atlas.Api.IntegrationTests/NotificationConsumerIdempotencyTests.cs` — same message twice → one CommunicationLog per channel
- [ ] **Create** `Atlas.Api.IntegrationTests/SchedulerTests.cs` — check-in creates AutomationSchedule rows; scheduler publishes and marks Published (fake bus)
- [ ] **Edit** `Atlas.Api.IntegrationTests/CustomWebApplicationFactory.cs` — use InMemoryEventBusPublisher when not configured; ensure migrations applied

### 13. Documentation

- [ ] **Create** `Atlas.Api/docs/eventing-servicebus.md` — topics/subscriptions, envelope, sessions, outbox, idempotency, scheduler, MSG91, future worker extraction
- [ ] **Edit** any existing docs that mention Kafka — replace with Azure Service Bus

---

## Implementation order (phases)

1. **Phase 1 — Foundation:** EventTypes, EventEnvelope, OutboxMessage entity + migration, AzureServiceBusOptions, IEventBusPublisher + Azure + InMemory.
2. **Phase 2 — Outbox:** OutboxDispatcherHostedService, wire BookingsController (confirm/cancel/checkin/checkout) and payment→confirm to insert OutboxMessage only.
3. **Phase 3 — Notifications:** INotificationProvider, Msg91NotificationProvider, NotificationOrchestrator, BookingEventsNotificationConsumer, StayEventsNotificationConsumer; idempotent CommunicationLog.
4. **Phase 4 — Scheduler:** AutomationSchedule TimeZoneId/NextAttemptUtc (migration if needed), create schedule rows on check-in, AutomationSchedulerHostedService.
5. **Phase 5 — WhatsApp:** Webhook endpoint, persist inbound, outbox whatsapp.inbound.received, WhatsAppInboundConsumerStub.
6. **Phase 6 — Tests & docs:** Unit tests, integration tests (fake bus), eventing-servicebus.md.

---

## NFRs reminder

- **Transactional outbox:** Never publish inside booking transaction; only insert OutboxMessage at commit.
- **Idempotency:** CommunicationLog unique (TenantId, IdempotencyKey); key = f(eventId, bookingId, channel, template).
- **Multi-tenant:** Single topics; use application properties tenantId, eventType, entityId, schemaVersion, correlationId, idempotencyKey.
- **Sessions:** SessionId = `{tenantId}:{bookingId}` for booking/stay events.
- **CI:** No real Azure dependency; use InMemory/fake bus in integration tests.
- **Env rules:** Delete Restrict in prod, Cascade in test; migrations applied in test bootstrap.
