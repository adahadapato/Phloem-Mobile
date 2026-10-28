using Phloem.Services;

namespace Phloem.Helpers;

/// <summary>
/// Add to a page to keep the screen on while it's showing (if "Keep screen on" is switched on):
/// <c>&lt;ContentPage.Behaviors&gt;&lt;helpers:KeepScreenAwakeBehavior /&gt;&lt;/ContentPage.Behaviors&gt;</c>.
/// </summary>
public sealed class KeepScreenAwakeBehavior : Behavior<Page>
{
    private ScreenAwakeService? _awake;
    private bool _holding;

    protected override void OnAttachedTo(Page page)
    {
        base.OnAttachedTo(page);
        _awake = IPlatformApplication.Current?.Services.GetService<ScreenAwakeService>();
        page.Appearing += OnAppearing;
        page.Disappearing += OnDisappearing;
    }

    protected override void OnDetachingFrom(Page page)
    {
        page.Appearing -= OnAppearing;
        page.Disappearing -= OnDisappearing;
        OnDisappearing(page, EventArgs.Empty);
        base.OnDetachingFrom(page);
    }

    private void OnAppearing(object? sender, EventArgs e)
    {
        if (_holding || _awake is null) return;
        _holding = true;
        _awake.Hold();
    }

    private void OnDisappearing(object? sender, EventArgs e)
    {
        if (!_holding || _awake is null) return;
        _holding = false;
        _awake.Release();
    }
}
