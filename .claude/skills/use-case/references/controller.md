# Documented Controller

Placeholders: `{Noun}` and `{noun}` (singular), `{Plural}` and `{plural}` (plural, `{plural}` also the route segment). Braces are used instead of angle brackets because `<Noun>` inside an XML doc comment reads as a tag. The `Title` field and its limit only show the shape; real limits come from the entity's constants.

## Controller

```csharp
/// <summary>
/// {Plural} of the signed-in user.
/// </summary>
[Route("api/{plural}")]
public sealed class {Plural}Controller : ApiController
{
    public {Plural}Controller(ISender sender) : base(sender) { }

    /// <summary>
    /// Creates a {noun}.
    /// </summary>
    /// <remarks>
    /// Any signed-in user may call it. The new {noun} belongs to the caller.
    /// Sending the same body twice creates two {plural}.
    /// </remarks>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <param name="command">The {noun} to create.</param>
    /// <response code="201">The {noun} was created; the Location header points to it.</response>
    /// <response code="400">A field is missing, too long or malformed; errors lists each field.</response>
    /// <response code="401">The request has no valid session token.</response>
    [HttpPost]
    [ProducesResponseType(typeof({Noun}Response), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create([FromBody] Create{Noun}Command command, CancellationToken cancellationToken)
    {
        Result<{Noun}Response> result = await Sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value)
            : HandleFailure(result);
    }

    /// <summary>
    /// Gets a {noun} by its id.
    /// </summary>
    /// <remarks>
    /// Any signed-in user may call it. A deleted {noun} is not found.
    /// </remarks>
    /// <param name="id" example="00000000-0000-0000-0000-000000000001">The {noun}'s id.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">The {noun}.</response>
    /// <response code="401">The request has no valid session token.</response>
    /// <response code="404">No {noun} has this id.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof({Noun}Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        Result<{Noun}Response> result = await Sender.Send(new Get{Noun}Query(id), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : HandleFailure(result);
    }
}
```

## Contract Records

```csharp
/// <summary>
/// A {noun} to create.
/// </summary>
/// <param name="Title">What the {noun} is called. Required, at most 200 characters.</param>
/// <example>{ "title": "title" }</example>
public sealed record Create{Noun}Command(string Title) : ICommand<{Noun}Response>;

/// <summary>
/// A {noun} as clients see it.
/// </summary>
/// <param name="Id">The {noun}'s id.</param>
/// <param name="Title">What the {noun} is called.</param>
/// <example>{ "id": "00000000-0000-0000-0000-000000000001", "title": "title" }</example>
public sealed record {Noun}Response(Guid Id, string Title);
```
