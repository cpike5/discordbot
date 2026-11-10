# The cpike.Agent package feed

The assistant's agent framework (`cpike.Agent.*`, from
[cpike5/cpike-agent-framework](https://github.com/cpike5/cpike-agent-framework)) comes from the
owner's private GitHub Packages feed, `https://nuget.pkg.github.com/cpike5/index.json`. The committed
`nuget.config` names it `cpike5` and maps only `cpike.Agent.*` to it; every other package comes from
nuget.org. The file holds no credentials.

Reading the feed needs a **classic** personal access token with the `read:packages` scope.
Fine-grained tokens do not work with the GitHub Packages NuGet registry. A restore without one fails
with `401 (Unauthorized)` naming `nuget.pkg.github.com`.

## Where each build gets its credential

| Where | How |
| --- | --- |
| A developer machine | Once: `dotnet nuget add source https://nuget.pkg.github.com/cpike5/index.json --name cpike5 --username <github-user> --password <token> --store-password-in-clear-text`. That writes your user-level NuGet config; NuGet matches its credential to the repository's `cpike5` source by name. |
| A Claude cloud session | Save the token as a network credential for `nuget.pkg.github.com` (username and password, the token as the password) on the session's environment; the proxy adds it to the feed requests. The SessionStart hook warns instead of failing when restore cannot reach the feed. |
| CI (`ci.yml`, `release.yml`) | The run's own `GITHUB_TOKEN` (`packages: read`), written into the runner's copy of `nuget.config`. Each package must grant this repository Actions access: package settings → *Manage Actions access*. |
| Docker image (`docker-publish.yml`, a local `docker build`) | A BuildKit secret, `nuget_token`. The publish workflow passes `GITHUB_TOKEN`; locally, `export NUGET_TOKEN=<token>` and run `docker build --secret id=nuget_token,env=NUGET_TOKEN .`. The Dockerfile uses it in the restore step only, in a throwaway copy of `nuget.config`. |

## Without access to the feed

The packages can be packed from the framework's checkout at the release tag and restored from a
folder. It is the same package.

```bash
dotnet pack cpike.Agent.sln -c Release -p:Version=0.2.0 -o /tmp/cpike-packages
```

```bash
cp nuget.config /tmp/nuget.local.config
```

```bash
dotnet nuget update source cpike5 --configfile /tmp/nuget.local.config --source /tmp/cpike-packages
```

```bash
dotnet restore DiscordBot.sln --configfile /tmp/nuget.local.config
```

Run the first command in the framework checkout and the rest in this repository. Builds after that
can use `--no-restore`.
