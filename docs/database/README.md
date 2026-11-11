# choco

## Tables

| Name                                            | Columns | Comment                                                                                                      | Type       |
| ----------------------------------------------- | ------- | ------------------------------------------------------------------------------------------------------------ | ---------- |
| [public.comments](public.comments.md)           | 8       | Comments that users write on posts.                                                                          | BASE TABLE |
| [public.notifications](public.notifications.md) | 8       | Things that happened to a user, such as a new follower, shown to that user.                                  | BASE TABLE |
| [public.post_likes](public.post_likes.md)       | 4       | Active likes that users give to posts.                                                                       | BASE TABLE |
| [public.posts](public.posts.md)                 | 7       | Text posts that users publish.                                                                               | BASE TABLE |
| [public.roles](public.roles.md)                 | 7       | Named sets of permissions that users can hold. Roles and their permissions can change while the system runs. | BASE TABLE |
| [public.user_follows](public.user_follows.md)   | 4       | Active follow relationships between users.                                                                   | BASE TABLE |
| [public.user_roles](public.user_roles.md)       | 5       | Roles assigned to users, besides the default role that every signed-in user has.                             | BASE TABLE |
| [public.users](public.users.md)                 | 9       | People who use choco-api. Sign-in lives in Clerk; each row is the local copy of one Clerk user.              | BASE TABLE |

## Stored procedures and functions

| Name                   | ReturnType | Arguments                              | Type     |
| ---------------------- | ---------- | -------------------------------------- | -------- |
| public.unaccent        | text       | regdictionary, text                    | FUNCTION |
| public.unaccent        | text       | text                                   | FUNCTION |
| public.unaccent_init   | internal   | internal                               | FUNCTION |
| public.unaccent_lexize | internal   | internal, internal, internal, internal | FUNCTION |

## Relations

```mermaid
erDiagram

"public.comments" }o--|| "public.users" : "FOREIGN KEY (author_id) REFERENCES users(id) ON DELETE RESTRICT"
"public.comments" }o--|| "public.posts" : "FOREIGN KEY (post_id) REFERENCES posts(id) ON DELETE RESTRICT"
"public.notifications" }o--|| "public.users" : "FOREIGN KEY (actor_id) REFERENCES users(id) ON DELETE RESTRICT"
"public.notifications" }o--|| "public.users" : "FOREIGN KEY (recipient_id) REFERENCES users(id) ON DELETE RESTRICT"
"public.notifications" }o--o| "public.posts" : "FOREIGN KEY (post_id) REFERENCES posts(id) ON DELETE RESTRICT"
"public.notifications" }o--o| "public.comments" : "FOREIGN KEY (comment_id) REFERENCES comments(id) ON DELETE RESTRICT"
"public.post_likes" }o--|| "public.users" : "FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE RESTRICT"
"public.post_likes" }o--|| "public.posts" : "FOREIGN KEY (post_id) REFERENCES posts(id) ON DELETE RESTRICT"
"public.posts" }o--|| "public.users" : "FOREIGN KEY (author_id) REFERENCES users(id) ON DELETE RESTRICT"
"public.user_follows" }o--|| "public.users" : "FOREIGN KEY (followed_user_id) REFERENCES users(id) ON DELETE RESTRICT"
"public.user_follows" }o--|| "public.users" : "FOREIGN KEY (follower_id) REFERENCES users(id) ON DELETE RESTRICT"
"public.user_roles" }o--o| "public.users" : "FOREIGN KEY (assigned_by) REFERENCES users(id) ON DELETE RESTRICT"
"public.user_roles" }o--|| "public.users" : "FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE"
"public.user_roles" }o--|| "public.roles" : "FOREIGN KEY (role_id) REFERENCES roles(id) ON DELETE CASCADE"

"public.comments" {
  uuid author_id FK
  varchar_500_ content
  timestamp_with_time_zone created_at
  timestamp_with_time_zone deleted_at
  uuid id
  boolean is_deleted
  timestamp_with_time_zone modified_at
  uuid post_id FK
}
"public.notifications" {
  uuid actor_id FK
  uuid comment_id FK
  timestamp_with_time_zone created_at
  uuid id
  uuid post_id FK
  timestamp_with_time_zone read_at
  uuid recipient_id FK
  varchar_32_ type
}
"public.post_likes" {
  uuid id
  timestamp_with_time_zone liked_at
  uuid post_id FK
  uuid user_id FK
}
"public.posts" {
  uuid author_id FK
  varchar_500_ content
  timestamp_with_time_zone created_at
  timestamp_with_time_zone deleted_at
  uuid id
  boolean is_deleted
  timestamp_with_time_zone modified_at
}
"public.roles" {
  timestamp_with_time_zone created_at
  uuid id
  boolean is_default
  timestamp_with_time_zone modified_at
  varchar_64_ name
  varchar_64___ permissions
  varchar_64_ system_key
}
"public.user_follows" {
  timestamp_with_time_zone followed_at
  uuid followed_user_id FK
  uuid follower_id FK
  uuid id
}
"public.user_roles" {
  timestamp_with_time_zone assigned_at
  uuid assigned_by FK
  uuid id
  uuid role_id FK
  uuid user_id FK
}
"public.users" {
  varchar_2048_ avatar_url
  timestamp_with_time_zone created_at
  timestamp_with_time_zone deleted_at
  varchar_256_ display_name
  uuid id
  varchar_64_ identity_id
  boolean is_deleted
  timestamp_with_time_zone modified_at
  varchar_64_ username
}
```

---

> Generated by [tbls](https://github.com/k1LoW/tbls)
