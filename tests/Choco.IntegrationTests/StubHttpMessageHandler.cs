using System.Net;

namespace Choco.IntegrationTests;

internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly List<HttpRequestMessage> _requests = [];
    private Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _respond = NotConfigured;

    public IReadOnlyList<HttpRequestMessage> Requests => _requests;

    public void RespondWith(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        _respond = (request, _) => Task.FromResult(respond(request));
    }

    public void RespondWithAsync(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
    {
        _respond = respond;
    }

    public void Reset()
    {
        _requests.Clear();
        _respond = NotConfigured;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _requests.Add(request);

        return _respond(request, cancellationToken);
    }

    private static Task<HttpResponseMessage> NotConfigured(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
    }
}
