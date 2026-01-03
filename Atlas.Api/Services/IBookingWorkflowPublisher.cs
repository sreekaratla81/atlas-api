using Atlas.Api.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Atlas.Api.Services
{
    public interface IBookingWorkflowPublisher
    {
        Task PublishBookingConfirmedAsync(
            Booking booking,
            Guest guest,
            IReadOnlyCollection<CommunicationLog> communicationLogs,
            OutboxMessage outboxMessage);
    }
}
