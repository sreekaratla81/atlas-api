using Atlas.Api.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Atlas.Api.Services
{
    public class NoOpBookingWorkflowPublisher : IBookingWorkflowPublisher
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
