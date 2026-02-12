using Atlas.Api.Models;

namespace Atlas.Api.Services
{
    public interface IEmailService
    {
        /// <summary>
        /// Sends booking confirmation email after successful payment
        /// </summary>
        /// <param name="booking">Booking entity with related Guest, Listing, Property, and Payment data loaded</param>
        /// <param name="razorpayPaymentId">Razorpay payment ID for the transaction</param>
        /// <returns>True if email sent successfully, false otherwise</returns>
        Task<bool> SendBookingConfirmationEmailAsync(Booking booking, string razorpayPaymentId);

        /// <summary>
        /// Sends booking confirmation email with individual parameters (legacy method)
        /// </summary>
        Task<bool> SendBookingConfirmationEmailAsync(
            string guestName,
            string guestEmail,
            string bookingId,
            string propertyName,
            int adults,
            DateTime checkInDate,
            DateTime checkOutDate,
            decimal totalAmount,
            string currency,
            string paymentId);
    }
}
