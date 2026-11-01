# Test Data

Test values are neutral: they name the field they fill and carry no real names, places, languages or brands.

## Rules

1. Every field gets its own value. Two fields never share a value, so a swapped assignment fails the test.
2. A value names its field: `"identity-id"`, `"username"`, `"Display Name"`. A failure message then shows which field went wrong.
3. No sequence numbers on a single object. Add `-2`, `-3` only when a test needs several (`"username-1"`, `"username-2"`).
4. Values keep the real format of the field: a URL is an absolute URL, an email has `@`, a GUID is a GUID.
5. Anything that looks like a network address, contact or payment detail comes from the reserved ranges below, never from a real service.
6. Invalid-input theories cover one value per rejected class (`null`, `""`, `" "`) and assert which input was rejected: the parameter name with `WithParameterName` for guard clauses, the property with `ShouldHaveValidationErrorFor(...).Only()` for validators.
7. Values come from constants on the entity (`User.UsernameMaxLength`) when testing limits; never repeat the number in the test. Test both sides of every limit: a value at the limit passes, one past it fails.
8. Add an Object Mother or Test Data Builder only once several tests build the same object.
9. Times are fixed UTC instants on a whole second: `new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)`. Use the current time only where a library reads the system clock itself, such as webhook signatures and JWT lifetimes.
10. GUIDs are fixed literals, one per field, never `Guid.NewGuid()` in the test.
11. No random values. When a test needs many values, derive them from a fixed seed.

## Reserved Values

| Kind | Use | Source |
|---|---|---|
| Domain | `example.com`, `example.org`, `example.net` | RFC 2606 |
| Top-level domain | `.test`, `.example`, `.invalid` | RFC 6761 |
| URL | `https://example.com/avatar.png` | RFC 2606 |
| Email | `user@example.com` | RFC 2606 |
| IPv4 | `192.0.2.0/24`, `198.51.100.0/24`, `203.0.113.0/24` | RFC 5737 |
| IPv6 | `2001:db8::/32`, `3fff::/20` | RFC 3849, RFC 9637 |
| Phone (North America) | `+1 555-0100` to `+1 555-0199` | NANPA |
| Payment card | The provider's published test numbers | Provider docs |

## Examples

| Field | Value |
|---|---|
| `IdentityId` | `"identity-id"` |
| `Username` | `"username"` |
| `DisplayName` | `"Display Name"` |
| `AvatarUrl` | `"https://example.com/avatar.png"` |
| Second user | `"identity-id-2"`, `"username-2"`, `"Display Name 2"` |
