using Application.Common.Models;

namespace Application.Interfaces;

public interface IImageStorage
{
    ImageUploadData CreateUploadData(Guid profileId, Guid attributeId);

    Task<bool> ExistsAsync(string publicId, CancellationToken cancellationToken);

    string CreateUrl(string publicId);

    Task DeleteAsync(string publicId, CancellationToken cancellationToken);
}
