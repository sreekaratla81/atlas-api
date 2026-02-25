using Atlas.Api.Models;

namespace Atlas.Api.Services;

public static class DefaultTemplateSeeder
{
    public static List<MessageTemplate> GetDefaultTemplates(int tenantId)
    {
        return new List<MessageTemplate>
        {
            new()
            {
                TenantId = tenantId,
                EventType = "booking.confirmed",
                Channel = "WhatsApp",
                ScopeType = "tenant",
                Language = "en",
                TemplateVersion = 1,
                IsActive = true,
                Subject = "Booking Confirmed",
                Body = "Hi {{guest_name}}, your booking at {{listing_name}} is confirmed! Check-in: {{checkin_date}}, Check-out: {{checkout_date}}. Total: {{total_amount}}. Booking Ref: {{booking_id}}."
            },
            new()
            {
                TenantId = tenantId,
                EventType = "booking.confirmed",
                Channel = "Email",
                ScopeType = "tenant",
                Language = "en",
                TemplateVersion = 1,
                IsActive = true,
                Subject = "Booking Confirmed \u2014 {{listing_name}}",
                Body = "Dear {{guest_name}},\n\nYour booking at {{listing_name}} has been confirmed.\n\nCheck-in: {{checkin_date}}\nCheck-out: {{checkout_date}}\nTotal: {{total_amount}}\nBooking Reference: {{booking_id}}\n\nWe look forward to hosting you!\n\nBest regards,\nAtlas Homestays"
            },
            new()
            {
                TenantId = tenantId,
                EventType = "stay.welcome.due",
                Channel = "WhatsApp",
                ScopeType = "tenant",
                Language = "en",
                TemplateVersion = 1,
                IsActive = true,
                Subject = "Check-in Tomorrow",
                Body = "Hi {{guest_name}}, your check-in at {{listing_name}} is tomorrow ({{checkin_date}}). We're excited to welcome you! Please reach out if you need directions or have any questions."
            },
            new()
            {
                TenantId = tenantId,
                EventType = "stay.precheckout.due",
                Channel = "WhatsApp",
                ScopeType = "tenant",
                Language = "en",
                TemplateVersion = 1,
                IsActive = true,
                Subject = "Check-out Tomorrow",
                Body = "Hi {{guest_name}}, just a reminder that your check-out from {{listing_name}} is tomorrow ({{checkout_date}}). Please check out by the scheduled time. We hope you enjoyed your stay!"
            },
            new()
            {
                TenantId = tenantId,
                EventType = "stay.postcheckout.due",
                Channel = "Email",
                ScopeType = "tenant",
                Language = "en",
                TemplateVersion = 1,
                IsActive = true,
                Subject = "Thank you for staying at {{listing_name}}",
                Body = "Dear {{guest_name}},\n\nThank you for staying at {{listing_name}}! We hope you had a wonderful experience.\n\nWe would love your feedback \u2014 please consider leaving a review.\n\nBest regards,\nAtlas Homestays"
            },
            new()
            {
                TenantId = tenantId,
                EventType = "booking.cancelled",
                Channel = "Email",
                ScopeType = "tenant",
                Language = "en",
                TemplateVersion = 1,
                IsActive = true,
                Subject = "Booking Cancelled \u2014 {{listing_name}}",
                Body = "Dear {{guest_name}},\n\nYour booking at {{listing_name}} (Ref: {{booking_id}}) has been cancelled.\n\nIf you have any questions about refunds or would like to rebook, please contact us.\n\nBest regards,\nAtlas Homestays"
            }
        };
    }
}
