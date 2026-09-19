namespace Application.Common.Models;

public sealed record ImageUploadData(string UploadUrl, string PublicId, string ApiKey, long Timestamp, string Signature, string UploadPreset, string DeliveryType);
