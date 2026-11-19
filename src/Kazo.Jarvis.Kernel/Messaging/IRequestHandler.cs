namespace Kazo.Jarvis.Kernel.Messaging;

/// <summary>
/// The single handler of a request type, registered in the service container as a singleton.
/// A slice registers it in its registration method (ADR 0002, section 6).
/// </summary>
/// <typeparam name="TRequest">Type of the request.</typeparam>
/// <typeparam name="TResult">Type of the answer.</typeparam>
public interface IRequestHandler<in TRequest, TResult>
    where TRequest : IRequest<TResult>
{
    /// <summary>Answers one request.</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancels the handling.</param>
    /// <returns>The answer.</returns>
    ValueTask<TResult> HandleAsync(TRequest request, CancellationToken cancellationToken);
}
