using Atlas.Api.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Atlas.Api.Services
{
    /// <summary>
    /// Abstraction for publishing booking workflow events to external systems.
    /// Currently backed by <see cref="NoOpBookingWorkflowPublisher"/> (no-op).
    /// Not invoked anywhere yet — retained for future integration (e.g., external
    /// workflow engines). See BL-011 in booking-lifecycle-gaps.md.
    /// </summary>
    [Obsolete("Not wired to any caller. Retained for planned workflow engine integration. See BL-011.")]
    public interface IBookingWorkflowPublisher
    {
        Task PublishBookingConfirmedAsync(
            Booking booking,
            Guest guest,
            IReadOnlyCollection<CommunicationLog> communicationLogs,
            OutboxMessage outboxMessage);
    }
}
