using System;

public interface IAdService
{
    bool CanShowRewardedAd { get; }
    void ShowRewardedAd(Action<bool> onComplete);
}

public sealed class MockAdService : IAdService
{
    public bool CanShowRewardedAd => true;

    public void ShowRewardedAd(Action<bool> onComplete)
    {
        onComplete?.Invoke(true);
    }
}
