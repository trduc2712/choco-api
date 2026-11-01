using System.Net;

namespace Choco.IntegrationTests;

internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly List<HttpRequestMessage> _requests = [];
    private Func<HttpRequestMessage, HttpResponseMessage> _respond = NotConfigured;

    public IReadOnlyList<HttpRequestMessage> Requests => _requests;

    public void RespondWith(Func<HttpRequestMessage, HttpResponseMessage> respond)
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

        return Task.FromResult(_respond(request));
    }

    private static HttpResponseMessage NotConfigured(HttpRequestMessage request)
    {
        return new HttpResponseMessage(HttpStatusCode.InternalServerError);
    }
}
