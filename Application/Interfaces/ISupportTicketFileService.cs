using Application.Dtos;

namespace Application.Interfaces;

public interface ISupportTicketFileService
{
    Task UploadAsync(SupportTicketFileDto ticket, CancellationToken cancellationToken);
}
