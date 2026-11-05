namespace ICanHelp.Application.Time;

/// <summary>Źródło bieżącego czasu (UTC). Domena dostaje czas parametrem, handlery biorą go stąd – w testach podstawiamy stały czas.</summary>
public interface IClock
{
    DateTime UtcNow { get; }
}
