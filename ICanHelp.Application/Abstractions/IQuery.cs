namespace ICanHelp.Application.Abstractions;

/// <summary>Marker for a request that only reads data and returns <typeparamref name="TResult"/>.</summary>
public interface IQuery<TResult>
{
}
