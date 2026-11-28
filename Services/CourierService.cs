using Phloem.Services.Api;

namespace Phloem.Services;

/// <summary>
/// "Become a courier" on the API (<c>/api/couriers/me</c>): the person's registration, their vehicle details with two
/// live photos (checked by the AI at once), their documents, and sending it to Phloem staff.
/// </summary>
/// <remarks>Same checks for every courier: independent couriers, a shop's riders and sellers who deliver their own
/// orders. Staff approve it on the website (Admin → Couriers); then the person gets the Courier role.</remarks>
public sealed class CourierService(ApiClient api)
{
    /// <summary>The registration: vehicle, the documents needed for my country and vehicle, where it stands.</summary>
    public Task<ApiResult<MyCourierRegistrationDto>> GetMineAsync(CancellationToken ct = default) =>
        api.GetAsync<MyCourierRegistrationDto>("api/couriers/me/registration", ct);

    /// <summary>Sends vehicle details with the front (number plate) and side photos; the answer has hints to fix.</summary>
    /// <param name="mobility">How I get around.</param>
    /// <param name="registrationNumber">The number plate (motorbike, keke, car, van).</param>
    /// <param name="make">Make / model (optional).</param>
    /// <param name="colour">Colour (optional).</param>
    /// <param name="front">Front photo (null on foot).</param>
    /// <param name="side">Side photo (null on foot).</param>
    /// <param name="ct">Cancellation.</param>
    public async Task<ApiResult<MyVehicleDto>> SendVehicleAsync(MobilityType mobility, string? registrationNumber,
                                                                string? make, string? colour, FileResult? front,
                                                                FileResult? side, CancellationToken ct = default)
    {
        var fields = new Dictionary<string, string> { ["mobility"] = mobility.ToString() };
        if (!string.IsNullOrWhiteSpace(registrationNumber)) fields["registrationNumber"] = registrationNumber.Trim();
        if (!string.IsNullOrWhiteSpace(make)) fields["make"] = make.Trim();
        if (!string.IsNullOrWhiteSpace(colour)) fields["colour"] = colour.Trim();

        var streams = new List<Stream>();
        try
        {
            var files = new List<UploadFile>();
            foreach (var (field, photo) in new[] { ("frontPhoto", front), ("sidePhoto", side) })
            {
                if (photo is null) continue;
                var stream = await photo.OpenReadAsync();
                streams.Add(stream);
                files.Add(new UploadFile(field, stream, photo.FileName, ContentTypeOf(photo)));
            }
            return await api.UploadFilesAsync<MyVehicleDto>(HttpMethod.Post, "api/couriers/me/vehicle", files, fields, ct);
        }
        finally
        {
            foreach (var stream in streams) await stream.DisposeAsync();
        }
    }

    /// <summary>Gives one document (a photo from the camera) with its number; replaces the same type.</summary>
    public async Task<ApiResult<MyCourierRegistrationDto>> SendDocumentAsync(CourierDocumentType type, string? number,
                                                                             FileResult photo,
                                                                             CancellationToken ct = default)
    {
        var fields = new Dictionary<string, string> { ["type"] = type.ToString() };
        if (!string.IsNullOrWhiteSpace(number)) fields["number"] = number.Trim();

        await using var stream = await photo.OpenReadAsync();
        return await api.UploadAsync<MyCourierRegistrationDto>(HttpMethod.Post, "api/couriers/me/documents", "file",
                                                               stream, photo.FileName, ContentTypeOf(photo), fields, ct);
    }

    /// <summary>Sends the registration to Phloem staff.</summary>
    public Task<ApiResult<MyCourierRegistrationDto>> SubmitAsync(CancellationToken ct = default) =>
        api.PostAsync<MyCourierRegistrationDto>("api/couriers/me/registration/submit", null, ct);

    /// <summary>"This isn't my courier": alerts Phloem staff at once (my own delivery, while it's on its way).</summary>
    public Task<ApiResult<CourierConcernRaisedDto>> RaiseConcernAsync(CourierConcernKind kind, string reference,
                                                                      string? note, CancellationToken ct = default) =>
        api.PostAsync<CourierConcernRaisedDto>("api/couriers/concerns",
                                               new RaiseCourierConcernCommand(kind, reference, note), ct);

    private static string ContentTypeOf(FileResult photo) =>
        string.IsNullOrWhiteSpace(photo.ContentType) ? "image/jpeg" : photo.ContentType;
}
