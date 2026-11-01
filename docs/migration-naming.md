# Migration Naming

Pattern: `<Verb><Object>[<Kind>][<Preposition><Table>]`

## Rules

1. PascalCase. No dates or sequence numbers: EF already prefixes the file with a timestamp.
2. Tables: PascalCase plural matching the database table (`users` → `Users`, `post_tags` → `PostTags`).
3. Columns: the C# property name (`DisplayName`, not `display_name`).
4. Standalone objects always carry a kind suffix: `Table`, `View`, `MaterializedView`, `Function`, `Trigger`, `Extension`, `Enum`, `Sequence`, `Schema`, `Index`, `ForeignKey`, `Check`.
5. Prepositions: `To` when adding to, `From` when removing from, `In` when changing inside.
6. Two objects are joined with `And`; more than two are named by purpose.
7. Verbs are a fixed set: `Add`, `Remove`, `Rename`, `Change`, `Make`, `Seed`, `Backfill`. No synonyms (`Create`, `Drop`, `Delete`, `Update`).
8. Indexes, foreign keys and constraints created together with a table belong to `Add<Name>Table` and are not named separately.
9. One logical change per migration. If no single name covers it, split it.

## Tables

| Case | Name |
|---|---|
| Create a table | `AddUsersTable` |
| Create a join table | `AddPostTagsTable` |
| Drop a table | `RemoveUsersTable` |
| Rename a table | `RenameUsersTableToMembers` |

## Columns

| Case | Name |
|---|---|
| Add a column | `AddBioToUsers` |
| Add two columns | `AddBioAndWebsiteToUsers` |
| Add several columns for one purpose | `AddProfileDetailsToUsers` |
| Drop a column | `RemoveBioFromUsers` |
| Rename a column | `RenameDisplayNameToFullNameInUsers` |
| Change the type | `ChangeBioTypeInUsers` |
| Change the length | `ChangeUsernameLengthInUsers` |
| Make required | `MakeAvatarUrlRequiredInUsers` |
| Make optional | `MakeAvatarUrlOptionalInUsers` |
| Add a default | `AddIsVerifiedDefaultToUsers` |
| Drop a default | `RemoveIsVerifiedDefaultFromUsers` |

## Indexes

| Case | Name |
|---|---|
| Index | `AddCreatedAtIndexToPosts` |
| Unique index | `AddUniqueUsernameIndexToUsers` |
| Multi-column index | `AddAuthorIdAndCreatedAtIndexToPosts` |
| Filtered index | `AddActiveUsernameIndexToUsers` |
| Drop an index | `RemoveCreatedAtIndexFromPosts` |
| Change an index | `ChangeUsernameIndexInUsers` |

## Keys and Constraints

| Case | Name |
|---|---|
| Add a foreign key | `AddAuthorForeignKeyToPosts` |
| Drop a foreign key | `RemoveAuthorForeignKeyFromPosts` |
| Change delete behavior | `ChangeAuthorForeignKeyInPosts` |
| Add a check constraint | `AddContentLengthCheckToPosts` |
| Drop a check constraint | `RemoveContentLengthCheckFromPosts` |
| Add a unique constraint | `AddUniqueSlugConstraintToPosts` |
| Change the primary key | `ChangePrimaryKeyInLikes` |

## Other Postgres Objects

| Case | Name |
|---|---|
| View | `AddUserStatsView`, `ChangeUserStatsView`, `RemoveUserStatsView` |
| Materialized view | `AddTrendingPostsMaterializedView` |
| Function | `AddSearchVectorFunction` |
| Trigger | `AddUpdateSearchVectorTriggerToPosts` |
| Extension | `AddPgTrgmExtension`, `AddCitextExtension` |
| Enum type | `AddPostVisibilityEnum`, `AddArchivedToPostVisibilityEnum` |
| Sequence | `AddOrderNumberSequence` |
| Schema | `AddAnalyticsSchema` |

## Data

| Case | Name |
|---|---|
| Seed fixed data | `SeedRoles` |
| Fill a new column | `BackfillDisplayNameInUsers` |
| Fix bad data | `ChangeEmptyBioToNullInUsers` |
