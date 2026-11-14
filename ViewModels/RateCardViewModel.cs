using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Phloem.Services;
using Phloem.Services.Api.Contracts.Commands;
using Phloem.Services.Api.Contracts.Dtos;
using Phloem.Services.Api.Contracts.Enums;

namespace Phloem.ViewModels;

/// <summary>
/// The "How was it?" card on the order and parcel pages (<see cref="Views.Controls.RateCard"/>): five stars to tap,
/// an optional comment and "show my comment on Phloem". Hidden until delivered; afterwards it shows my review and
/// what happens to the comment. Same rules and words as the website.
/// </summary>
public partial class RateCardViewModel(ReviewService reviews) : ObservableObject
{
    /// <summary>Same as the API's <c>Review.CommentMaxLength</c>.</summary>
    public const int CommentMaxLength = 500;

    private static readonly string[] Words = ["", "Poor", "Not great", "OK", "Good", "Excellent"];

    private ReviewKind _kind;
    private string _reference = string.Empty;
    private int _loadVersion;

    /// <summary>"How was your food from Mama's Kitchen?" / "How did we do with your parcel?"</summary>
    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    /// <summary>The state from the API (null until loaded, or when not delivered).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsVisible), nameof(CanWrite), nameof(HasReview), nameof(ShowAsText),
                              nameof(MyStarsText), nameof(MyWordText), nameof(MyCommentText), nameof(HasMyComment),
                              nameof(StatusText), nameof(IsWaiting))]
    public partial ReviewStateDto? State { get; set; }

    /// <summary>The stars tapped (0 = none yet).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStars), nameof(StarWord), nameof(Star1), nameof(Star2), nameof(Star3),
                              nameof(Star4), nameof(Star5))]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial int Stars { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasComment), nameof(CounterText))]
    public partial string Comment { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool ShowPublicly { get; set; } = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial bool IsSending { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string Error { get; set; } = string.Empty;

    public bool IsVisible => State is { } s && (s.CanReview || s.Review is not null);
    public bool CanWrite => State is { CanReview: true, Review: null };
    public bool HasReview => State?.Review is not null;
    public bool HasStars => Stars > 0;
    public bool HasComment => !string.IsNullOrWhiteSpace(Comment);
    public bool HasError => !string.IsNullOrEmpty(Error);
    public string StarWord => Words[Math.Clamp(Stars, 0, 5)];
    public string CounterText => $"{Comment.Length}/{CommentMaxLength}";
    public string ShowAsText => State is { } s ? $"Show my comment on Phloem as {s.ShowAs}" : string.Empty;

    /// <summary>Whether each star is lit (gold); the page colours them with a trigger.</summary>
    public bool Star1 => Stars >= 1;
    public bool Star2 => Stars >= 2;
    public bool Star3 => Stars >= 3;
    public bool Star4 => Stars >= 4;
    public bool Star5 => Stars >= 5;

    public string MyStarsText => State?.Review is { } r ? new string('★', r.Stars) + new string('☆', 5 - r.Stars) : string.Empty;
    public string MyWordText => State?.Review is { } r ? Words[Math.Clamp(r.Stars, 0, 5)] : string.Empty;
    public string MyCommentText => State?.Review?.Comment is { } c ? $"\"{c}\"" : string.Empty;
    public bool HasMyComment => State?.Review?.Comment is not null;

    /// <summary>What happens to my comment, in plain words (the stars always count straight away).</summary>
    public string StatusText => State?.Review switch
    {
        null => string.Empty,
        { Comment: null } => "Thank you! Your rating has been added.",
        { ShowPublicly: false } => "Thank you! Your rating counts, and your comment is just for us.",
        { Status: ReviewStatus.Checking } => "Thank you! Your rating counts. Your comment will appear on Phloem in a moment.",
        { Status: ReviewStatus.WaitingForStaff } => "Thank you! Your rating counts. Our team is taking a quick look at your comment before it goes on Phloem.",
        { Status: ReviewStatus.Published } r => $"Thank you! Your review is on Phloem as {r.DisplayName}.",
        { Status: ReviewStatus.Rejected } => "Thank you! Your rating counts. Your comment won't be shown on Phloem, but our team has read it.",
        _ => "Thank you! Your rating counts. Your comment is no longer shown on Phloem."
    };

    public bool IsWaiting => State?.Review is { Comment: not null, ShowPublicly: true, Status: ReviewStatus.Checking or ReviewStatus.WaitingForStaff };

    /// <summary>
    /// Shows the card for an order or parcel. Only asks the API once it's delivered; otherwise hides the card.
    /// Safe to call every time the page refreshes (a later call wins).
    /// </summary>
    public async Task LoadAsync(ReviewKind kind, string reference, bool delivered, string title)
    {
        var version = ++_loadVersion;
        if (_kind != kind || _reference != reference)
        {
            _kind = kind; _reference = reference;
            State = null; Stars = 0; Comment = string.Empty; ShowPublicly = true; Error = string.Empty;
        }
        Title = title;
        if (!delivered || string.IsNullOrWhiteSpace(reference)) { State = null; return; }

        try
        {
            var result = await reviews.GetStateAsync(kind, reference);
            if (version != _loadVersion) return;
            if (result.IsSuccess) State = result.Value;
        }
        catch (Exception)
        {
            // The card just doesn't show; the rest of the page is unaffected
        }
    }

    /// <summary>Tap a star (1–5).</summary>
    [RelayCommand]
    private void PickStar(string value)
    {
        if (int.TryParse(value, out var n)) Stars = Math.Clamp(n, 1, 5);
    }

    private bool CanSend() => Stars > 0 && !IsSending;

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task Send()
    {
        if (State is not { } state) return;
        IsSending = true;
        Error = string.Empty;
        try
        {
            var comment = Comment.Trim();
            var result = await reviews.SubmitAsync(_kind, _reference,
                new RateRequest(Stars, comment.Length == 0 ? null : comment, ShowPublicly));
            if (result.IsFailure) { Error = result.Error!.DisplayMessage; return; }
            State = state with { CanReview = false, Review = result.Value };
        }
        catch (Exception)
        {
            Error = "We couldn't send your review. Please check your connection and try again.";
        }
        finally
        {
            IsSending = false;
        }
    }
}
