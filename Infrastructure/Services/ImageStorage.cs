using Application.Common.Models;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Infrastructure.Models;
using Application.Interfaces;
using Microsoft.Extensions.Options;


namespace Infrastructure.Services;

public sealed class ImageStorage : IImageStorage
{
    private readonly Cloudinary _cloudinary;
    private readonly CloudinaryOptions _cloudinaryOptions;

    public ImageStorage(Cloudinary cloudinary, IOptions<CloudinaryOptions> cloudinaryOptions)
    {
        _cloudinary = cloudinary;
        _cloudinaryOptions = cloudinaryOptions.Value;
    }

    public ImageUploadData CreateUploadData(Guid profileId, Guid attributeId)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var publicId = $"profiles/{profileId}/attributes/{attributeId}/{Guid.NewGuid().ToString()}";
        var parameters = new SortedDictionary<string, object>
        {
            ["public_id"] = publicId,
            ["timestamp"] = timestamp,
            ["type"] = _cloudinaryOptions.DeliveryType,
            ["upload_preset"] = _cloudinaryOptions.UploadPreset
        };

        var signature = _cloudinary.Api.SignParameters(parameters);
        var account = _cloudinary.Api.Account;

        var uploadUrl =
            $"https://api.cloudinary.com/v1_1/" +
            $"{account.Cloud}/image/upload";

        return new ImageUploadData(uploadUrl, publicId, account.ApiKey, timestamp, signature, _cloudinaryOptions.UploadPreset, _cloudinaryOptions.DeliveryType);
    }

    public string CreateUrl(string imgId)
    {
        if (string.IsNullOrWhiteSpace(imgId))
            throw new ArgumentException("Public id cannot be empty.", nameof(imgId));
        return _cloudinary.Api.UrlImgUp
            .Secure()
            .Signed(true)
            .Type(_cloudinaryOptions.DeliveryType)
            .Transform(new Transformation().Width(1200).Crop("limit").Quality("auto").FetchFormat("auto"))
            .BuildUrl(imgId);
    }

    public async Task DeleteAsync(string imgId, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(imgId))
            return;
        var parameters = new DeletionParams(imgId)
        {
            ResourceType = ResourceType.Image,
            Type = _cloudinaryOptions.DeliveryType,
            Invalidate = true
        };
        var result = await _cloudinary.DestroyAsync(parameters).WaitAsync(token);

        if (result.Error is not null)
            throw new InvalidOperationException(result.Error.Message);
    }

    public async Task<bool> ExistsAsync(string imgId, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(imgId))
            return false;
        var parameters = new GetResourceParams(imgId)
        {
            ResourceType = ResourceType.Image,
            Type = _cloudinaryOptions.DeliveryType
        };

        var result = await _cloudinary
            .GetResourceAsync(parameters)
            .WaitAsync(token);

        return result.Error is null &&
               string.Equals(result.PublicId, imgId, StringComparison.Ordinal);
    }
}
