using System.Text.Json;
using Microsoft.Extensions.Logging;
using Phloem.Services.Api;
using Phloem.Services.Api.Contracts.Dtos;
using Phloem.Services.Api.Contracts.Enums;

namespace Phloem.Services;

/// <summary>
/// The Terms &amp; Conditions and the Privacy Policy, from the API's database (<c>GET /api/legal/{type}</c>), so the
/// app and the website always show the same wording and the app has none built in.
/// </summary>
/// <remarks>The last copy of each is kept on the phone, so the pages still open without internet.</remarks>
public sealed class LegalService(ApiClient api, ILogger<LegalService> logger)
{
    /// <summary>Gets the current version (and keeps it on the phone); without internet, the copy kept last time.</summary>
    /// <param name="type">Terms or Privacy.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The document and null; or null and why it couldn't be loaded (nothing kept either).</returns>
    public async Task<(LegalDocumentDto? Document, ApiError? Error)> GetAsync(LegalDocumentType type,
                                                                            CancellationToken ct = default)
    {
        var result = await api.GetAsync<LegalDocumentDto>($"api/legal/{type}", ct);
        if (result.IsSuccess && result.Value is not null)
        {
            Save(type, result.Value);
            return (result.Value, null);
        }

        logger.LogInformation("{Type} not loaded from the API ({Code}); using the saved copy.", type, result.Error?.Code);
        var saved = ReadSaved(type);
        return saved is not null ? (saved, null) : (null, result.Error);
    }

    private static string Key(LegalDocumentType type) => $"phloem.legal.{type}";

    private static void Save(LegalDocumentType type, LegalDocumentDto document)
    {
        try { Preferences.Default.Set(Key(type), JsonSerializer.Serialize(document, ApiJson.Options)); }
        catch (Exception) { /* storage unavailable: the page still shows what it loaded */ }
    }

    private static LegalDocumentDto? ReadSaved(LegalDocumentType type)
    {
        try
        {
            var json = Preferences.Default.Get(Key(type), string.Empty);
            return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<LegalDocumentDto>(json, ApiJson.Options);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
