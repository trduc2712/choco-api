using System.ComponentModel.DataAnnotations;

namespace Choco.Infrastructure.Options;

internal sealed class StorageOptions : IValidatableObject
{
    public const string SectionName = "Storage";

    private const string BucketNamePattern = "^[a-z0-9-]+$";

    [Required(ErrorMessage = $"Missing '{SectionName}:{nameof(ServiceUrl)}'.")]
    public string ServiceUrl { get; set; } = string.Empty;

    [Required(ErrorMessage = $"Missing '{SectionName}:{nameof(Region)}'.")]
    public string Region { get; set; } = string.Empty;

    [Required(ErrorMessage = $"Missing '{SectionName}:{nameof(UploadsBucketName)}'.")]
    [StringLength(63, MinimumLength = 6, ErrorMessage = $"'{SectionName}:{nameof(UploadsBucketName)}' must be 6 to 63 characters.")]
    [RegularExpression(BucketNamePattern, ErrorMessage = $"'{SectionName}:{nameof(UploadsBucketName)}' must contain only lowercase letters, digits and hyphens.")]
    public string UploadsBucketName { get; set; } = string.Empty;

    [Required(ErrorMessage = $"Missing '{SectionName}:{nameof(MediaBucketName)}'.")]
    [StringLength(63, MinimumLength = 6, ErrorMessage = $"'{SectionName}:{nameof(MediaBucketName)}' must be 6 to 63 characters.")]
    [RegularExpression(BucketNamePattern, ErrorMessage = $"'{SectionName}:{nameof(MediaBucketName)}' must contain only lowercase letters, digits and hyphens.")]
    public string MediaBucketName { get; set; } = string.Empty;

    [Required(ErrorMessage = $"Missing '{SectionName}:{nameof(AccessKeyId)}'.")]
    public string AccessKeyId { get; set; } = string.Empty;

    [Required(ErrorMessage = $"Missing '{SectionName}:{nameof(SecretAccessKey)}'.")]
    public string SecretAccessKey { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Uri.TryCreate(ServiceUrl, UriKind.Absolute, out Uri? serviceUrl)
            || serviceUrl.Scheme is not ("http" or "https"))
        {
            yield return new ValidationResult(
                $"'{SectionName}:{nameof(ServiceUrl)}' must be an absolute http or https URL.",
                [nameof(ServiceUrl)]);
        }

        if (UploadsBucketName == MediaBucketName)
        {
            yield return new ValidationResult(
                $"'{SectionName}:{nameof(UploadsBucketName)}' and '{SectionName}:{nameof(MediaBucketName)}' must be different buckets.",
                [nameof(UploadsBucketName), nameof(MediaBucketName)]);
        }
    }
}
