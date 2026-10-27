using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Phloem.Services;
using Phloem.Services.Api.Contracts.Commands;
using Phloem.Services.Api.Contracts.Dtos;
using Phloem.Services.Api.Contracts.Enums;

namespace Phloem.ViewModels;

/// <summary>A topic tile on Contact us ("An order · Delivery, a refund, a cancellation").</summary>
public sealed partial class ContactTopicOption : ObservableObject
{
    public required ContactTopic Value { get; init; }
    public required string Label { get; init; }
    public required string Hint { get; init; }

    /// <summary>"Order number" / "Tracking number" when one helps; null otherwise.</summary>
    public string? ReferenceLabel { get; init; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

/// <summary>
/// Contact us (route "contact"), the same as the website's page: a short form (name, email, topic, order or tracking
/// number when it helps, message) and the other ways to reach the team (email, WhatsApp, phone, hours, address, from
/// the API). Signed-in people get their name and email filled in. After sending: a thank-you with the reply time.
/// </summary>
public partial class ContactViewModel(ContactService contact, SessionService session) : ObservableObject
{
    private const int MessageMin = 10;
    private const int MessageMax = 2000;

    public IReadOnlyList<ContactTopicOption> Topics { get; } =
    [
        new() { Value = ContactTopic.Order, Label = "An order", Hint = "Delivery, a refund, a cancellation", ReferenceLabel = "Order number" },
        new() { Value = ContactTopic.Parcel, Label = "A parcel", Hint = "Prices, weighing, customs, tracking", ReferenceLabel = "Tracking number" },
        new() { Value = ContactTopic.Selling, Label = "Selling on Phloem", Hint = "Opening a shop, approval, payouts" },
        new() { Value = ContactTopic.Account, Label = "My account", Hint = "Signing in, password, my details" },
        new() { Value = ContactTopic.Other, Label = "Something else", Hint = "Questions, ideas, feedback" },
    ];

    // ---------- Form ----------

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Email { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowReference), nameof(ReferenceLabel), nameof(ReferencePlaceholder))]
    public partial ContactTopicOption? SelectedTopic { get; set; }

    public bool ShowReference => SelectedTopic?.ReferenceLabel is not null;
    public string ReferenceLabel => $"{SelectedTopic?.ReferenceLabel} (optional)";
    public string ReferencePlaceholder => SelectedTopic?.Value == ContactTopic.Order ? "e.g. PH-261027-004821" : "e.g. PHS-261027-001234";

    [ObservableProperty]
    public partial string Reference { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CounterText))]
    public partial string Message { get; set; } = string.Empty;

    public string CounterText => $"{Message.Length:N0} / {MessageMax:N0}";

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsError { get; set; }

    // ---------- After sending ----------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFormVisible))]
    public partial bool IsSent { get; set; }

    public bool IsFormVisible => !IsSent;

    [ObservableProperty]
    public partial string ThanksTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ThanksText { get; set; } = string.Empty;

    // ---------- Other ways to reach us ----------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDetails), nameof(HasPhone), nameof(HasWhatsApp), nameof(HasAddress),
                              nameof(HasOpeningHours), nameof(IntroText))]
    public partial ContactDetailsDto? Details { get; set; }

    public bool HasDetails => Details is not null;
    public bool HasPhone => !string.IsNullOrWhiteSpace(Details?.Phone);
    public bool HasWhatsApp => !string.IsNullOrWhiteSpace(Details?.WhatsApp);
    public bool HasAddress => !string.IsNullOrWhiteSpace(Details?.Address);
    public bool HasOpeningHours => !string.IsNullOrWhiteSpace(Details?.OpeningHours);

    public string IntroText => Details?.ReplyTime is { } time
        ? $"A question about an order, a parcel or selling on Phloem? Send us a message and we'll reply {time}."
        : "A question about an order, a parcel or selling on Phloem? Send us a message and we'll get back to you.";

    /// <summary>Called when the page appears: fills in who's signed in and loads the contact details.</summary>
    public async Task LoadAsync()
    {
        if (session.Profile is { } profile)
        {
            if (string.IsNullOrWhiteSpace(Name)) Name = profile.FullName;
            if (string.IsNullOrWhiteSpace(Email)) Email = profile.Email;
        }

        if (Details is not null) return;
        var result = await contact.GetDetailsAsync();
        if (result.IsSuccess) Details = result.Value;
    }

    [RelayCommand]
    private void SelectTopic(ContactTopicOption topic)
    {
        foreach (var t in Topics) t.IsSelected = t == topic;
        SelectedTopic = topic;
    }

    [RelayCommand]
    private async Task Send()
    {
        if (IsBusy) return;
        var problem =
            string.IsNullOrWhiteSpace(Name) ? "Please enter your name." :
            !Regex.IsMatch(Email.Trim(), @"^\S+@\S+\.\S+$") ? "Please enter a valid email address so we can reply." :
            SelectedTopic is null ? "Please choose what your message is about." :
            Message.Trim().Length < MessageMin ? $"Please tell us a little more (at least {MessageMin} characters)." :
            Message.Length > MessageMax ? $"Please keep your message under {MessageMax:N0} characters." :
            null;
        if (problem is not null)
        {
            ShowError(problem);
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            var reference = ShowReference && !string.IsNullOrWhiteSpace(Reference) ? Reference.Trim() : null;
            var result = await contact.SendAsync(new SendContactMessageCommand(
                Name.Trim(), Email.Trim(), SelectedTopic!.Value, reference, Message.Trim()));
            if (result.IsFailure)
            {
                ShowError(result.Error!.DisplayMessage);
                return;
            }

            var firstName = Name.Trim().Split(' ')[0];
            ThanksTitle = $"Thanks, {firstName}!";
            ThanksText = $"We've got your message{(Details?.ReplyTime is { } t ? $" and we'll reply {t}" : "")}. " +
                         $"A copy is on its way to {Email.Trim()}. If it doesn't arrive, check your spam folder.";
            IsSent = true;
            Message = string.Empty;
            Reference = string.Empty;
            foreach (var topic in Topics) topic.IsSelected = false;
            SelectedTopic = null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SendAnother() => IsSent = false;

    // ---------- Other ways to reach us ----------

    [RelayCommand]
    private async Task EmailUs()
    {
        if (Details is null) return;
        await OpenAsync($"mailto:{Details.Email}");
    }

    [RelayCommand]
    private async Task CallUs()
    {
        if (Details?.Phone is { } phone) await OpenAsync($"tel:{phone.Replace(" ", "")}");
    }

    [RelayCommand]
    private async Task WhatsAppUs()
    {
        if (Details?.WhatsApp is { } number) await OpenAsync($"https://wa.me/{new string(number.Where(char.IsDigit).ToArray())}");
    }

    /// <summary>Quick answers: Help at a section ("orders", "parcels", "selling", "account").</summary>
    [RelayCommand]
    private async Task OpenHelp(string section) => await Phloem.AppNav.OpenLegalAsync("Help", section);

    private async Task OpenAsync(string uri)
    {
        try
        {
            await Launcher.Default.OpenAsync(uri);
        }
        catch (Exception)
        {
            ShowError("Your phone couldn't open that. You can copy the details from this page instead.");
        }
    }

    private void ShowError(string message)
    {
        IsError = true;
        StatusMessage = message;
    }
}
