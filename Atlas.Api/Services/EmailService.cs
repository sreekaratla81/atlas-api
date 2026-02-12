using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Atlas.Api.Models;
using Atlas.Api.Data;
using Microsoft.EntityFrameworkCore;
using System;

namespace Atlas.Api.Services
{
    public class EmailService : IEmailService
    {
        private readonly SmtpConfig _smtpConfig;
        private readonly ILogger<EmailService> _logger;
        private readonly AppDbContext _context;

        public EmailService(
            IOptions<SmtpConfig> smtpConfig, 
            ILogger<EmailService> logger,
            AppDbContext context)
        {
            _smtpConfig = smtpConfig.Value;
            _logger = logger;
            _context = context;

            // Log SMTP configuration on service initialization (without sensitive data)
            _logger.LogInformation("EmailService initialized - SMTP Host: {Host}, Port: {Port}, FromEmail: {FromEmail}, FromName: {FromName}", 
                _smtpConfig.Host, _smtpConfig.Port, _smtpConfig.FromEmail, _smtpConfig.FromName);
            
            if (string.IsNullOrWhiteSpace(_smtpConfig.Username) || string.IsNullOrWhiteSpace(_smtpConfig.Password))
            {
                _logger.LogWarning("SMTP Username or Password is not configured. Email sending will fail.");
            }
        }

        /// <summary>
        /// Sends booking confirmation email using Booking entity (recommended method)
        /// </summary>
        public async Task<bool> SendBookingConfirmationEmailAsync(Booking booking, string razorpayPaymentId)
        {
            try
            {
                // Ensure related entities are loaded
                if (booking.Guest == null)
                {
                    await _context.Entry(booking).Reference(b => b.Guest).LoadAsync();
                }

                if (booking.Listing == null)
                {
                    await _context.Entry(booking).Reference(b => b.Listing).LoadAsync();
                }

                if (booking.Listing?.Property == null)
                {
                    await _context.Entry(booking.Listing!).Reference(l => l.Property).LoadAsync();
                }

                var guestName = booking.Guest?.Name ?? "Guest";
                // Email comes from the original Razorpay order request (request.GuestInfo.Email)
                // which was stored in the Guest entity when booking was created
                var guestEmail = booking.Guest?.Email ?? string.Empty;
                var bookingId = booking.Id.ToString();
                var propertyName = booking.Listing?.Property?.Name ?? booking.Listing?.Name ?? "Property";
                var guests = booking.GuestsPlanned ?? 1;
                var checkInDate = booking.CheckinDate;
                var checkOutDate = booking.CheckoutDate;
                var totalAmount = booking.TotalAmount ?? booking.AmountReceived;
                var currency = booking.Currency ?? "INR";

                if (string.IsNullOrWhiteSpace(guestEmail))
                {
                    _logger.LogWarning("Cannot send booking confirmation email: Guest email is missing for booking {BookingId}", booking.Id);
                    return false;
                }

                _logger.LogInformation("Sending booking confirmation email FROM: {FromEmail} TO: {GuestEmail} for booking {BookingId}", 
                    _smtpConfig.FromEmail, guestEmail, bookingId);

                return await SendBookingConfirmationEmailAsync(
                    guestName,
                    guestEmail,
                    bookingId,
                    propertyName,
                    guests,
                    checkInDate,
                    checkOutDate,
                    totalAmount,
                    currency,
                    razorpayPaymentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send booking confirmation email for booking {BookingId}", booking.Id);
                return false;
            }
        }

        /// <summary>
        /// Sends booking confirmation email with individual parameters (legacy method)
        /// </summary>
        public async Task<bool> SendBookingConfirmationEmailAsync(
            string guestName,
            string guestEmail,
            string bookingId,
            string propertyName,
            int adults,
            DateTime checkInDate,
            DateTime checkOutDate,
            decimal totalAmount,
            string currency,
            string paymentId)
        {
            try
            {
                _logger.LogInformation("Preparing to send booking confirmation email to {GuestEmail} for booking {BookingId}", 
                    guestEmail, bookingId);

                var emailBody = BuildEmailBody(
                    guestName,
                    bookingId,
                    propertyName,
                    adults,
                    checkInDate,
                    checkOutDate,
                    totalAmount,
                    currency,
                    paymentId);

                // Validate SMTP configuration
                if (string.IsNullOrWhiteSpace(_smtpConfig.FromEmail))
                {
                    _logger.LogError("SMTP FromEmail is not configured");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(_smtpConfig.Username) || string.IsNullOrWhiteSpace(_smtpConfig.Password))
                {
                    _logger.LogError("SMTP Username or Password is not configured");
                    return false;
                }

                _logger.LogInformation("SMTP Configuration - Host: {Host}, Port: {Port}, FromEmail: {FromEmail}, Username: {Username}", 
                    _smtpConfig.Host, _smtpConfig.Port, _smtpConfig.FromEmail, _smtpConfig.Username);

                var message = new MailMessage
                {
                    From = new MailAddress(_smtpConfig.FromEmail, _smtpConfig.FromName),
                    To = { new MailAddress(guestEmail) },
                    Subject = $"Booking Confirmation - {bookingId}",
                    Body = emailBody,
                    IsBodyHtml = true
                };

                _logger.LogInformation("Attempting to send email FROM: {FromEmail} TO: {ToEmail} via SMTP {Host}:{Port}", 
                    _smtpConfig.FromEmail, guestEmail, _smtpConfig.Host, _smtpConfig.Port);

                using var smtpClient = new SmtpClient(_smtpConfig.Host, _smtpConfig.Port)
                {
                    EnableSsl = _smtpConfig.EnableSsl,
                    Credentials = new NetworkCredential(_smtpConfig.Username, _smtpConfig.Password),
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    Timeout = 30000
                };

                await smtpClient.SendMailAsync(message);
                
                _logger.LogInformation("Successfully sent booking confirmation email to {GuestEmail} for booking {BookingId}", 
                    guestEmail, bookingId);
                
                return true;
            }
            catch (SmtpException smtpEx)
            {
                _logger.LogError(smtpEx, "SMTP error sending email to {GuestEmail} for booking {BookingId}. StatusCode: {StatusCode}, Message: {Message}", 
                    guestEmail, bookingId, smtpEx.StatusCode, smtpEx.Message);
                
                // Common Gmail SMTP errors
                if (smtpEx.Message.Contains("authentication", StringComparison.OrdinalIgnoreCase) ||
                    smtpEx.Message.Contains("535", StringComparison.OrdinalIgnoreCase) ||
                    smtpEx.Message.Contains("5.7.1", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogError("Gmail authentication failed. Please use an App Password instead of regular password. " +
                        "Generate one at: https://myaccount.google.com/apppasswords");
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send booking confirmation email to {GuestEmail} for booking {BookingId}. Error: {ErrorMessage}", 
                    guestEmail, bookingId, ex.Message);
                _logger.LogError("Full exception details: {ExceptionDetails}", ex.ToString());
                return false;
            }
        }

        private string BuildEmailBody(
            string guestName,
            string bookingId,
            string propertyName,
            int adults,
            DateTime checkInDate,
            DateTime checkOutDate,
            decimal totalAmount,
            string currency,
            string paymentId)
        {
            var currencySymbol = GetCurrencySymbol(currency);
            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #4CAF50; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; background-color: #f9f9f9; }}
        .booking-details {{ background-color: white; padding: 15px; margin: 15px 0; border-left: 4px solid #4CAF50; }}
        .detail-row {{ margin: 10px 0; }}
        .label {{ font-weight: bold; color: #555; }}
        .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
        .contact-info {{ margin-top: 20px; padding-top: 20px; border-top: 1px solid #ddd; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Booking Confirmed!</h1>
        </div>
        <div class='content'>
            <p>Hi {guestName},</p>
            <p>Thank you for choosing Atlas Homes.</p>
            <p>Your booking has been successfully confirmed. Please find your reservation details below:</p>
            
            <div class='booking-details'>
                <div class='detail-row'>
                    <span class='label'>Booking ID:</span> {bookingId}
                </div>
                <div class='detail-row'>
                    <span class='label'>Property:</span> {propertyName}
                </div>
                <div class='detail-row'>
                    <span class='label'>Guests:</span> {adults} Adults
                </div>
                <div class='detail-row'>
                    <span class='label'>Check-in:</span> {checkInDate:dd MMM yyyy}
                </div>
                <div class='detail-row'>
                    <span class='label'>Check-out:</span> {checkOutDate:dd MMM yyyy}
                </div>
                <div class='detail-row'>
                    <span class='label'>Amount Paid:</span> {currencySymbol}{totalAmount:N2}
                </div>
                <div class='detail-row'>
                    <span class='label'>Payment ID (Razorpay):</span> {paymentId}
                </div>
            </div>
            
            <p>Your reservation is now secured.</p>
            
            <div class='contact-info'>
                <p>If you need any assistance, feel free to contact us:</p>
                <p>📞 Call/WhatsApp: +91-7032493290<br>
                📧 Email: atlashomeskphb@gmail.com</p>
            </div>
            
            <p>We look forward to hosting you and making your stay comfortable.</p>
            <p>Warm regards,<br>Team Atlas Homes</p>
        </div>
        <div class='footer'>
            <p>This is an automated confirmation email. Please do not reply to this email.</p>
        </div>
    </div>
</body>
</html>";
        }

        /// <summary>
        /// Gets currency symbol for display in email
        /// </summary>
        private string GetCurrencySymbol(string currency)
        {
            return currency?.ToUpper() switch
            {
                "INR" => "₹",
                "USD" => "$",
                "EUR" => "€",
                "GBP" => "£",
                _ => currency + " "
            };
        }
    }

    public class SmtpConfig
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 587;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FromEmail { get; set; } = string.Empty;
        public string FromName { get; set; } = "Atlas Homes";
        public bool EnableSsl { get; set; } = true;
    }
}
