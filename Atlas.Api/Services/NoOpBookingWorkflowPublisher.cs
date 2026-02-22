using Atlas.Api.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Atlas.Api.Services
{
    /// <summary>
    /// No-op implementation of <see cref="IBookingWorkflowPublisher"/>.
    /// Registered as the default; not invoked by any controller today.
    /// See BL-011 in booking-lifecycle-gaps.md for planned removal/replacement.
    /// </summary>
    [Obsolete("Not invoked anywhere. Retained for planned workflow engine integration. See BL-011.")]
#pragma warning disable CS0618 // Suppress obsolete warning for implementing obsolete interface
    public class NoOpBookingWorkflowPublisher : IBookingWorkflowPublisher
#pragma warning restore CS0618
    {
        public Task PublishBookingConfirmedAsync(
            Booking booking,
            Guest guest,
            IReadOnlyCollection<CommunicationLog> communicationLogs,
            OutboxMessage outboxMessage)
        {
            return Task.CompletedTask;
        }
    }
}
