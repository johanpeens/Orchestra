# Roles and Permissions

A **role** is a badge that says what you are allowed to do. Think of a school: pupils, prefects, teachers and the headmaster all have different keys.

Orchestra has four roles.

| Role | In one line |
|---|---|
| **Viewer** | Can look and ask the bot. Cannot change anything. |
| **Member** | Can add and update steps, with screenshots. Cannot delete or reorder. |
| **Manager** | Can create, edit and delete folders, processes, steps, departments and people. |
| **Admin** | Can do everything a manager can, plus let new people in and change roles. |

## The full table

| What you want to do | Viewer | Member | Manager | Admin |
|---|:---:|:---:|:---:|:---:|
| Look at processes, team and training data | Yes | Yes | Yes | Yes |
| Ask the chat bot | Yes | Yes | Yes | Yes |
| See the activity log | Yes | Yes | Yes | Yes |
| Add or edit a step (with screenshot) | No | Yes | Yes | Yes |
| Delete or reorder steps | No | No | Yes | Yes |
| Create, edit, delete folders and processes | No | No | Yes | Yes |
| Add, edit or delete people and departments | No | No | Yes | Yes |
| Approve or reject new accounts | No | No | No | Yes |
| Change someone's role | No | No | No | Yes |

## New people have no role

When someone creates an account, they have **no role at all**. They cannot do anything until an admin approves them and picks a role. See [Approving New People](Approving-New-People.md).

## What if I try something I am not allowed to?

Orchestra shows a message like "You do not have permission to do that". Nothing is changed. The check happens on the server, so hiding a button is never the only protection.

## Where the rules live in the code

If you are a developer: the rules are in `AuthorisationGuard`, and the role names are in `AppRoles`. Both are in `src/TeamPortal.Application/Common`. See [How It Is Built](How-It-Is-Built.md).
