using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Models;

public sealed class CloudinaryOptions
{
    public const string SectionName = "Cloudinary";

    public string UploadPreset { get; init; } = string.Empty;

    public string DeliveryType {  get; init; } = string.Empty;

    public string CloudName { get; init; } = string.Empty;

    public string ApiKey { get; init; } = string.Empty;

    public string ApiSecret { get; init; } = string.Empty;
}
