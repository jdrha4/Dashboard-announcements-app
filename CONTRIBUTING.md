# Contributing

This is a .NET project, built with [ASP.NET] framework.

## Setting things up

First, clone the project with git (you will need to have git authenticated to your GitHub account):

```bash
git clone https://github.com/jdrha4/Dashboard-announcements-app
```

To set up the project, you will need [dotnet] installed. Once done, run:

```bash
dotnet restore
```

This will install all of the project's dependencies for you.

## Database

This project requires a database. We're using the [Microsoft SQL Server] database.

> [!WARNING]
> You shouldn't use the production database during development, instead, make your own, local instance. That way, you
> won't accidentally break the shared database for everyone. See the [docker database setup section](#docker-database)
> for a quick setup guide.

### Docker database

To quickly spin up your own instance of SQL Server database, you can use [docker]. Do note that on Windows, docker
support isn't great, you might need to use [WSL]. Alternatively, you can also set up SQL Server directly, without
docker containerization. However, using docker will likely be the simplest method to get things working.

> [!WARNING]
> The mcr.microsoft.com/mssql/server docker image only supports amd64 (x86_64) CPU architecture, if you're using an ARM
> system, you might need to use emulation or try [`azure-sql-edge`][azure-sql-edge] instead (untested).

If you do wish to use docker, simply execute the following:

#### For Linux

```bash
# Create a docker volume, to persist the database data
sudo docker volume create mssql_data

# Create your local instance of microsoft sql server, exposed on 1433 port
sudo docker run -d \
   -p 1433:1433 \
   -e ACCEPT_EULA=y \
   -e MSSQL_SA_PASSWORD=password123% \
   -v mssql_data:/var/lib/mssql \
   --name mssql \
   --restart always \
   mcr.microsoft.com/mssql/server:latest

# Run the initial sql script
sudo docker cp ./docker-init.sql mssql:/init.sql
sudo docker exec -it mssql /opt/mssql-tools18/bin/sqlcmd \
  -C -S localhost -U sa -P password123% -d master \
  -i /init.sql

# Update the database, making sure the connection works
dotnet ef database update
```

#### For Windows

```bash
# Create a docker instance
docker run -d -p 1433:1433 -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=StrongPassw0rd!" -d mcr.microsoft.com/mssql/server:2022-latest

# Set the login credentials
dotnet user-secrets set "ConnectionStrings:Database" "Data Source=localhost,1433;Initial Catalog=STAG-AUIUI-P8VT;Integrated Security=false;User ID=sa;Password=StrongPassw0rd!;TrustServerCertificate=true"

# Update the database, making sure the connection works
dotnet ef database update
```

### Configuring database connection for the project

> [!TIP]
> Note that if you're using the default connection string, you don't need to do any of this

During development, you can set a connection string through dotnet user secrets:

```bash
dotnet user-secrets set "ConnectionStrings:Database" "Data Source=localhost,1433;Initial Catalog=STAG-AUIUI-P8VT;Integrated Security=false;User ID=myuser;Password=123password%;TrustServerCertificate=true"
```

In production, you should prefer using an environment variable:

```bash
export ConnectionStrings__Database="ConnectionStrings:Database" "Data Source=localhost,1433;Initial Catalog=STAG-AUIUI-P8VT;Integrated Security=false;User ID=myuser;Password=123password%;TrustServerCertificate=true"
```

If you wish, there's nothing wrong with using an environment variable even during development, it's just that dotnet
secrets are often more convenient.

## Email server (SMTP)

This project contains logic for email sending, however, to utilize this support, you will need to configure an SMTP
server, which should be used for this purpose.

If you're running the website in development mode, by default, you will see all emails in logs only, this allows
developers to easily test the project out without having to configure email sending. However, if you wish to run the
project in production, or if you want to test out email sending during development, you will need to do the following:

```bash
dotnet user-secrets set "EmailSettings:Sender" "username@email.com"
dotnet user-secrets set "EmailSettings:SmtpServer" "smtp.server.com"
dotnet user-secrets set "EmailSettings:Port" "587"  # this is the default, only run if different
dotnet user-secrets set "EmailSettings:Username" "username"
dotnet user-secrets set "EmailSettings:Password" "password"

# If you're running from development mode, you will also need to override the mode to smtp
# in production, this is the default
dotnet user-secrets set "EmailSettings:Mode" "Smtp"
```

For gmail, you can set the `SmtpServer` to `smtp.gmail.com`, the `Sender` and `Username` to your full email address
(including the `@gmail.com`) and `Password` to an [application password][google-app-passwords] (the account password
will not work here)

If you wish to go back to using logging to send emails in dev mode, you can run:

```bash
dotnet user-secrets "EmailSettings:Mode" "log"
```

Finally, optionally, you can also override the sender name that will show to the recipient, the default is `AnnounceIt`. If you wish to change this, you can use:

```bash
dotnet user-secrets "EmailSettings:SenderName" "value"
```

## Running

First, you will want to build the project with:

```bash
dotnet build
```

After that, you should be able to run the web server simply with:

```bash
dotnet run
```

> [!NOTE]
> If this fails for you, you might be missing the aspnet runtime. Make sure to install it first.
> (On Arch Linux, you can do this with `pacman -S aspnet-runtime`.)

---

## Code style

During development, it's important to make sure that the code everyone writes remains consistent and readable, that's
the reason why we enforce a certain code style. You will need to follow this style when developing.

### Static Analysis

While there is no linter tool built directly into dotnet, there are so called [.NET Analyzers]. These will run
automatically during the build, producing warnings (or even errors) on any violations. We use these to catch some
common mistakes and to generally improve the code quality of the project.

To run the analyzers, simply build the project:

```bash
dotnet build
```

> [!NOTE]
> Dotnet caches builds, which means if you run this for the 2nd time without making any changes to the code, you will
> not see the warnings in the output again. If you run into this and you need to see the warnings again, simply run
> `dotnet clean` before the `dotnet build` command.

Just like with code formatting, passing code style analysis is a requirement before merging code into `main`. Note that
any warnings in the output above will be treated as errors and will fail the automated CI check. This means that you
will need to fix your code and resolve all of the produced warnings before asking to merge the code back into `main`.

> [!TIP]
> Most modern code editors and IDEs support showing these analysis warnings directly as diagnostics in your code editor,
> look into enabling it for your editor.

### Formatter

To make sure our code style remains consistent everywhere, we're using some tools to automatically format the code.

Namely, we're using the official `dotnet format` (part of the dotnet Sdk) and a more thorough `csharpier` formatter

To format the code, you will want to run:

```bash
dotnet format
```

This will use the default formatter, fixing some basic formatting issues. After that, you will also want to run
[csharpier], which does some stricter, more opinionated code formatting. You will first need to install it though:

```bash
dotnet tool install -g csharpier
```

Once done, to run the formatter, simply execute:

```bash
dotnet-csharpier .
```

This style is enforced using a GitHub workflow that runs for every commit. Passing this CI check is a requirement to
merge your code into the `main` branch.

#### Editor integration

Most modern code editors and IDEs support automatic formatting, look into enabling it for your editor. Additionally,
csharpier has extensions for most IDEs, so if you can, download one, so that you won't have to run these commands
manually. (See the [editor integration][csharpier-editor-integration] page from csharpier docs.)

For vscode, first install the [csharpier extension][csharpier-vscode-extension]. Once done, open the settings (to do so,
press `Ctrl + Shift + P` to open the command pallette and search for `Preferences: Open user settings (JSON)`) In there,
add the following:

```json
{
    "editor.formatOnSave": true,
    "[csharp]": {
        "editor.defaultFormatter": "csharpier.csharpier-vscode"
    },
    "[xml]": {
        "editor.defaultFormatter": "csharpier.csharpier-vscode"
    },
    "dotnet.automaticallyCreateSolutionInWorkspace": false
}
```

This will enable automatic formatting on save through csharpier.

### Run checks automatically (pre-commit)

Optionally, you can download a tool called [pre-commit], which can run the necessary checks for you automatically before
each commit. This tool is recommended as it avoids needlessly pushing commits that will fail the GitHub CI, leading to
fixing commits / force pushing.

To install pre-commit, you will need to have [Python] installed.

- Best option would be to use your package manager (On Arch linux, you can use `pacman -S pre-commit`)
- If you don't have a package manger (Windows), another option would however be to use something like [pipx], so that
  you don't run into dependency conflicts. If you installed pipx, you can then run: `pipx install pre-commit`
- Lastly, you can use `pip` (python package manager), by running: `pip install pre-commit` (or, on Windows, you
  sometimes instead need: `py -m pip install pre-commit` or `python -m ...`). Note that you can run into dependency
  conflicts when installing python packages in this way, so prefer `pipx` if you can.

After installation, you should be able to run the `pre-commit` command (or `py -m pre-commit` / `python -m pre-commit`,
though I will only use `pre-commit` in the rest of the examples, use what works for you though).

#### Running manually

You can now use pre-commit to run all of the necessary checks for you with just one command:

```bash
pre-commit run --all-files
```

> [!TIP]
> To check individual files, you can instead use:
>
> ```bash
> pre-commit run --files file1.cs file2.cs
> ```

This way of running pre-commit will work, but you should prefer to have it run automatically (keep reading).

#### Running before each commit

> [!WARNING]
> If you're using a GUI interface to make git commits (such as the one from vscode) instead of commiting from the
> terminal manually, this might cause issues for you and it might not be easy to see the output of pre-commit, so you
> might not know what went wrong. I'd recommend you to just commit through the terminal, however, if you don't like
> doing that, make sure your gui tool supports pre-commit git hook, or avoid installing pre-commit to run before each
> commit (only use it manually).

To run pre-commit automatically, simply run: `pre-commit install`. This will register pre-commit tool as a git hook that
will be triggered before every commit, and if pre-commit fails, it will stop your commit, showing you what went wrong in
the output. What pre-commit runs is controlled by `.pre-commit-config.yaml` file.

> [!TIP]
> In some cases, it might be useful to push a commit that is failing these checks, this should be fairly rare, but if
> you do encounter this, you can bypass pre-commit checks by adding a `--no-verify` CLI flag to your git commit command
> (e.g. `git commit -m "My commit" --no-verify`), which will skip the pre-commit hook, letting you commit regardless of
> the checks.
>
> You can also skip individual checks (like only skipping the build step, but leaving the formatting ones to run)
> by setting an environment variable `SKIP` with comma-separated list of checks to skip. e.g.:
> `SKIP=dotnet-build,csharpier-format git commit -m "My commit" -m "This commit can fail build & csharpier"`.
>
> Note that skipping pre-commit will still lead to the GitHub CI failing, so you will still need to fix the issues
> eventually, otherwise merging won't be allowed, but you can do it later / in another commit.

Do note that this will slow down the committing process, as the checks can take quite a while to run, but I don't find
it too annoying personally.

## Using Git & GitHub

### Picking issues

Before working on anything, check GitHub to see if an issue already exists. If you find an issue you’d like to work on,
assign yourself to it before starting. If no issue exists, create one first (see the ["Making New
Issues"](#making-new-issues) section). To assign yourself to the issue, use the panel on the right:

![Image](https://github.com/user-attachments/assets/8aea08c3-49b4-4914-81d1-ca9bedd97d1a)

If you're looking for something to work on, but you're not sure what you can do, look through the existing issues.
GitHub provides some filtering options to help you find relevant tasks:

1. **Unassigned issues** - Filter out issues that already have someone working on them.
   ![Image](https://github.com/user-attachments/assets/e4b71b8c-6d2c-4b6d-9b21-63dcab55e34f)
2. **Milestones** - Show only issues that are part of the current sprint.
   ![Image](https://github.com/user-attachments/assets/135f8e7d-99ba-45e5-a159-93f3a054fbfe)
3. **Labels** - Filter issues by priority (`p: ...`) and area (`a: ...`). For example, only showing backend tasks and
   excluding those with low priority.

    ![Image](https://github.com/user-attachments/assets/a2ff86b5-991a-45cd-8a64-a697df7eaf54)

Before assigning yourself to an issue, check issue description and any comments added to understand the context of the
issue.

> [!NOTE]
> While in most cases, you should first make an issue before working on a task, if the task is as simple as say "fix a
> typo in a comment", or simply, if it doesn't affect anyone else and is relatively small in it's scope (e.g. a purely
> cosmetic issue, that doesn't affect end-users and doesn't change any functionality), you can skip the issue and make a
> PR right away.

### Making new issues

Whenever you identify something that needs fixing or improving, create a new issue. Issues should mainly focus on
problems in the main branch or the overall project.

#### Issues for PRs

You **shouldn't make issues for problems in your unmerged PR** (e.g. based on someone's review request, or a bug you
found there yourself), **unless that bug is also affecting the `main` branch**.

If your PR introduces a **new problem** (one that doesn't already affect `main`, but will become an issue after your PR is
merged) and you don't wish to address this problem in your PR directly (perhaps it's out of scope of what your PR does,
or you don't know how to address it, or you wish to leave some work for others too), you can open an issue about it.
However, make sure to mention that the issue will only become relevant once your PR is merged in the issue description.

#### Issue scope

Each issue should focus on a **single** problem. Don't make issues that address 3 different things, even if those
things may be related. That said, this doesn't mean you should create an issue for every little part of some problem
either, we should all have an intuitive understanding of what a concise problem should be, but here are some examples to
make it clear:

- **❌ Too Broad:**
    - _"Implement announcements system and announcement types"_:<br>
      Involves both backend & frontend: adding db models, creating multiple pages for editing, creating, viewing.
      Instead, this should've been split up into first adding the dashboard, showing the announcements from the db (at
      first just manually added), then pages for creation and deletion, and lastly the announcement types feature.
    - _"Redesign webpage"_:<br>
      If multiple parts need to be redesigned, it would be better to make issues dividing this task, like redesigning
      the footer, background, and individual pages, rather than addressing it all in a single issue
    - _"Fix all bugs in the admin panel"_:<br>
      Unmanageable: bugs should be tracked as individual issues with specific descriptions.
- **❌ Too Narrow:**
    - _"Create a database migration for the announcements table"_:<br>
      This should definitely be a part of adding the announcements table in the first place, there's no good reason to
      separate adding migrations and adding the table.
    - _"Fix spacing of delete button on admin panel"_:<br>
      This might be too small, if there's already an issue addressing design of the admin panel in general.
      However, if there isn't a bigger issue for this, and this is the only design problem with the admin panel, it
      can make sense to have it as a standalone issue.
    - _"Write a single unit test for AnnouncementService.CreateAnnouncement"_:<br>
      If no other tests for this class exist, it doesn't make sense to make an issue for each unit test, instead, it's
      enough to make a single issue to add all necessary tests for this class. If needed, the specific tests can be
      mentioned in that issue's description.
- **✅ Well-Scoped**
    - _"Allow filtering announcements by category and date"_:<br>
      A single feature with clear requirements
    - _"Add backend logic for sending emails"_:<br>
      This can later be used for sending emails for password reset requests, but that should be in another issue, that
      depends on this one.
    - _"Improve footer design to match the one in figma"_:<br>
      Involves plenty of work design-wise to match the figma style, without being overly broad, like addressing design
      across the whole page.

#### Issue Title

A good title is short and descriptive. It should be a one-sentence executive summary of the issue, so the impact and
severity of the issue you want to report can be inferred right from the title. It should be worded similarly to a commit
title.

| <!---->        | Example                                                                                           |
| -------------- | ------------------------------------------------------------------------------------------------- |
| ✅ **Clear**   | Add logic for the forgot password page                                                            |
| ❌ **Wordy**   | The registration page has a forgot password page link that doesn't work yet, it needs to be added |
| ❌ **Unclear** | Missing link                                                                                      |
| ❌ **Useless** | Issue                                                                                             |

#### Labels & metadata

When making the issue, it's important to add some metadata to classify it. The first thing to add is the milestone it
belongs to. This is either the current sprint, the next sprint, or future.

After that, you'll want to add labels to classify what it's about. For issues, the most important labels are the type
(`t: ...`), area (`a: ...`) and priority (`p: ...`). The status label (`s: ...`) is usually not used for issues, it is
mainly used for pull-requests (the only relevant status label for issues is the `s: stalled` label, used if something is
blocking further progress on the issue).

The type and area are usually easy to figure out, when it comes to the priority, ask yourself a few things:

- Is this something that the product owner explicitly requested as a part of this sprint?
- Are there some other issues that can't be worked on unless this issue is finished?
- How bad would it be if we didn't manage to address this issue within this sprint?

Here's a reference table, explaining when to use which priority:

| Priority     | When to use                                                         |
| ------------ | ------------------------------------------------------------------- |
| **Low**      | Not in the current sprint, nice-to-have, doesn’t impact other tasks |
| **Normal**   | Should be done this sprint, but not urgent/blocking                 |
| **High**     | Blocks other tasks, requested by PO, or important bug               |
| **Critical** | Production bug, major blocker, urgent fix required                  |

### Contributing changes

By now, you should have something that you wish to work on already picked. Before starting any work though, you should
always create your own feature branch.

To make a new branch, make sure you're in the `main` branch (to see what branch you're in, you can use `git status`,
the first line should say `On branch main`). If you're not, you can switch between branches using `git checkout
[branch-name]` (so in this case `git checkout main`). After that, you should always do a `git pull` first, to make sure
you're starting off from an up-to-date version of the project. This will help you reduce the amount of potential
conflicts you might run into and it gives you access to the newest features implemented by others already.

After that, follow these commands:

```bash
# When making a new branch, you can use git checkout with the `-b` flag
# This tells git that you wish to make a new branch, not just switch to
# an existing one. You will also need to decide on the name of the branch.
# Ideally, use something short, but descriptive, keep it to lowercase letters,
# separate words with dashes.
git checkout -b my-branch-name

# [do some work]

# Stage the changes you made, getting them ready for a commit
# (the -A will add all changed files, but you can also add individual ones,
# or even use --patch to only stage certain changes made to the file)
git add -A

# Now you're ready for a commit:
git commit -m "Short title for your commit"
# Optionally, you can also provide a longer description, by repeating the -m flag again:
# git commit -m "Short title for your commit" -m "Longer description, explaining changes"
# you can also run the command without any arguments, which will open an editor where you
# can type out the title and the description more comfortably:
# git commit

# For now, your branch and commits are only local (only on your machine), now it's time
# to share your changes with others. Note that you can make several commits first before pushing,
# no need to do so right after the first one.
#
# To push your branch for the first time, use git push with the --set-upstream / -u flag:
git push -u origin my-branch-name
# Next time you push, it's enough to just use:
# git push
```

> [!TIP]
> If you're unsure how to write good commit messages, I wrote a guide specifically explaining that
> [here][great-commits].

When you push a new branch to github, it will automatically show you a notification on the top, asking you whether you
wish to create a pull request for your branch:

![Image](https://github.com/user-attachments/assets/962f6207-de5e-4aae-9942-a88539c304e7)

If this doesn't appear though, you can also find your branch here:

![Image](https://github.com/user-attachments/assets/72403281-db41-4964-bc7b-d5bc4cce61e6)

and create a PR from there:

![Image](https://github.com/user-attachments/assets/096863ff-f092-46cc-91b7-65cb54b6aea4)

### Pull request metadata

After making your PR, it is important to specify some metadata to help people know what to do with it. A lot of this is
similar to issue metadata (milestone, and labels for type, area & priority), however, with PRs, it's also important to
specify the status labels:

| Status Label            | When to use                                                                                                                                            |
| ----------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `s: wip`                | You're still working on this PR, it's not yet ready to be merged and you don't expect reviews yet. PRs with this label shouln't be approved by anyone. |
| `s: needs review`       | If your work is done and you now want people to review your code, add this label. An approving reveiw is a requirement to get your PR merged.          |
| `s: waiting for author` | Usually, the reviewer assigns this label, if they want you to address some changes that they pointed out in their review.                              |
| `s: stalled`            | Some other PR / issue is blocking further progress. Mention which one in the PR description.                                                           |
| `s: deferred`           | Some work was already done on this PR, but it was decided that it's too soon for this, it's now waiting.                                               |

In some cases, you can combine `s: wip` with `s: needs review`, if you're only seeking comments / change requests, but
not approving reviews, you can also leave a comment on the PR, if you only seek a review for some specific part of the
PR, while you're still working on the rest.

Finally, you should link the associated issue (if any) to the PR. That way, that issue will automatically get closed
once your PR is merged. You can do this in the `Development` tab:

![image](https://github.com/user-attachments/assets/396c8f6e-cd6a-4782-be28-0f4d5f8448f8)

### Reviewing PRs of others

A crucial job of everyone in the team is not only to write code, but also to look at code of others and test if it works
correctly and if there's something that could be improved.

When submitting a review, you have 3 options:

- **Comment:** If you have some questions about something in the PR, or you wish to point out some things that could be
  improved, but you don't insist on them being done, use this type.
- **Request Changes:** If you found something that needs to be changed in order for the PR to be allowed to get merged,
  use this type. (Any PRs with requested changes will not be allowed to get merged until another review from the person
  who submitted this review)
- **Approve:** This will give the author the permission to merge the PR. Only use this type once you've **checked all of
  the changes** made by the PR and also **ran the project** from the PR's branch and tested that it does in fact work as
  intended.

[ASP.NET]: https://learn.microsoft.com/en-us/aspnet/overview
[dotnet-format]: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-format
[csharpier]: https://csharpier.com/
[csharpier-editor-integration]: https://csharpier.com/docs/Editors
[dotnet]: https://dotnet.microsoft.com/en-us/download
[.NET Analyzers]: https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/overview?tabs=net-9
[pre-commit]: https://pre-commit.com/
[Microsoft SQL Server]: https://www.microsoft.com/en-us/sql-server/sql-server-downloads
[docker]: https://www.docker.com/
[WSL]: https://learn.microsoft.com/en-us/windows/wsl/install
[azure-sql-edge]: https://learn.microsoft.com/en-us/azure/azure-sql-edge/disconnected-deployment
[pipx]: https://pipx.pypa.io/latest/installation/
[Python]: https://www.python.org
[csharpier-vscode-extension]: https://marketplace.visualstudio.com/items?itemName=csharpier.csharpier-vscode
bugs-and-feature-reqs/
[google-app-passwords]: https://myaccount.google.com/apppasswords
