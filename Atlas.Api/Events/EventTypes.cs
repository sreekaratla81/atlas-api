namespace Atlas.Api.Events;

/// <summary>Well-known event type constants for Service Bus and outbox.</summary>
public static class EventTypes
{
    public const string BookingCreated = "booking.created";
    public const string BookingConfirmed = "booking.confirmed";
    public const string BookingCancelled = "booking.cancelled";

    public const string StayCheckedIn = "stay.checked_in";
    public const string StayCheckedOut = "stay.checked_out";
    public const string StayWelcomeDue = "stay.welcome.due";
    public const string StayPrecheckoutDue = "stay.precheckout.due";
    public const string StayPostcheckoutDue = "stay.postcheckout.due";

    public const string WhatsAppInboundReceived = "whatsapp.inbound.received";

    public static bool IsBookingEvent(string eventType) =>
        eventType == BookingCreated || eventType == BookingConfirmed || eventType == BookingCancelled;

    public static bool IsStayReminderEvent(string eventType) =>
        eventType == StayWelcomeDue || eventType == StayPrecheckoutDue || eventType == StayPostcheckoutDue;
}
