using System.Net;

namespace Choco.Application.Abstractions.Requests;

public interface IRequestContext
{
    string? ClientId { get; }
    string? TraceId { get; }
    string? UserAgent { get; }
    IPAddress? IpAddress { get; }
}
