namespace TripAdvisorAgent.Core.Interfaces;

/// <summary>
/// Resolves the active <see cref="IFlightSearchProvider"/> based on application configuration
/// (e.g. <c>FlightSearch:Provider</c> in appsettings). Callers never depend on a concrete
/// provider directly — they depend only on this factory.
/// </summary>
public interface IFlightSearchProviderFactory
{
    /// <summary>
    /// Returns the configured flight search provider.
    /// </summary>
    IFlightSearchProvider GetProvider();
}
