using Choco.Contract.Abstractions;

namespace Choco.Contract.Reports;

/// <summary>The post that open reports are about.</summary>
/// <param name="Content">The post text.</param>
/// <param name="Author">The user who wrote the post.</param>
/// <example>{"content":"content","author":{"id":"00000000-0000-0000-0000-000000000002","username":"username"}}</example>
public sealed record ReportedPostResponse(string Content, ContentAuthorResponse Author);
