# Authorization

How choco-api decides what a signed-in user may do, and how to operate it. Why it works this way is in
[ADR 5](adr/0005-use-role-based-access-control-with-roles-in-the-database.md) and
[ADR 6](adr/0006-check-permissions-in-the-application-pipeline.md).

## How Permissions Are Computed

1. A permission is a code from a fixed catalog in code, such as `roles.assign`.
2. A role is a named set of permissions, stored in the `roles` table.
3. A user's permissions are those of the default role, which every signed-in user has without being assigned it, plus those of every role assigned to them in `user_roles`.
4. Code checks permissions, never role names. A request that needs a permission is rejected with `403 Permission.Denied` before it is validated.
5. Roles are platform-wide today. Roles scoped to a community come with communities.

Rules on assigning and revoking roles:

- Assigning or revoking a role needs `roles.assign`.
- A user can only assign or revoke a role whose permissions they all hold themselves, so nobody hands out more power than they have.
- The default role cannot be assigned or revoked.
- The last administrator cannot lose the administrator role (`409 Role.LastAdministrator`).
- Assigning a role the user already holds, or revoking one they do not hold, changes nothing.

## Setting the First Administrator

A new environment has no administrator, so nobody can assign roles. The first administrators come from configuration.

1. In the Clerk dashboard of the environment's Clerk instance, open **Users**, select the person and copy their **User ID** (`user_...`).
2. Set the setting `Authorization:BootstrapAdminIdentityIds` to a list of these ids:
   - On Render, declare the environment variable `Authorization__BootstrapAdminIdentityIds__0` (and `__1`, `__2` for more people) on the web service in the Terraform of `choco-infrastructure`, then apply it. Render is managed only by Terraform, so a variable set by hand in the dashboard is overwritten by the next apply.
   - Locally, run `dotnet user-secrets set "Authorization:BootstrapAdminIdentityIds:0" "user_..." --project src/Choco.Presentation`.

   Each environment has its own Clerk instance, so a person's Clerk user id differs between environments. The ids are not secret.
3. The person becomes an administrator:
   - at the next start of the API, if their user already exists in the database;
   - when their user is created, by the `user.created` webhook or their first request, if it does not exist yet.

The assignment records no assigner (`assigned_by` is null). It only adds the role: removing an id from the setting does not revoke anything. Revoke a role with `DELETE /api/users/{userId}/roles/{roleId}`.

If the setting is not empty and the API cannot reach the database at start, the API does not start. The administrator role is created by the migration that adds the `roles` table, so apply the setting after that migration has run in the environment; before it, the deployed code ignores the setting.

## Adding a Permission

1. Add a constant to the permission catalog in `src/Choco.Domain/Roles/Permission.cs` and to its list of all permissions.
2. Give the request that needs it an authorizer next to its handler, and declare `403` on its endpoint.
3. If a system role should have the permission when it is created, add it to `src/Choco.Domain/Roles/SystemRoles.cs`.

The seeder only creates system roles that are missing; it never changes a role that exists. A permission added in step 3 therefore reaches new databases only. To grant it to a role in an environment that already runs, write a migration that updates the role's `permissions`.

## Known Gaps

- There are no endpoints to create, rename or delete roles or to change their permissions. Changing a role needs a migration.
- A request with an authorizer whose endpoint does not declare `403` is missing `403` in the OpenAPI document, and nothing reports it.
