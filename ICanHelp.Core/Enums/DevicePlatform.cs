namespace ICanHelp.Core.Enums;

/// <summary>Where the push token (FCM) comes from – one sender for all of them (Krok 15).</summary>
public enum DevicePlatform
{
    /// <summary>Browser / installed PWA (Web Push through FCM).</summary>
    Web,

    /// <summary>Later: the store app (Capacitor, Krok 17).</summary>
    Android,

    /// <summary>Later: the store app (Capacitor, Krok 17).</summary>
    Ios
}
