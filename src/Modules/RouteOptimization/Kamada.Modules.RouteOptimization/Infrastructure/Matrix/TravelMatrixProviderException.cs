namespace Kamada.Modules.RouteOptimization.Infrastructure.Matrix;

/// <summary>The provider answered, but not with a usable matrix (error code, malformed payload).</summary>
internal sealed class TravelMatrixProviderException : Exception
{
    public TravelMatrixProviderException()
    {
    }

    public TravelMatrixProviderException(string message)
        : base(message)
    {
    }

    public TravelMatrixProviderException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
