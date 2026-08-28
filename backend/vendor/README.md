# vendor/

Code this repository did not write and does not maintain, kept here as source
rather than pulled from a feed.

## Knight.StoreAgent

Everything this shop needs to connect to KNIGHT and take delivery of Features.
It is one library shared by every ASP.NET Core store on that platform, and its
home is the KNIGHT repository (`stores/dotnet-store-agent`).

**Why it is vendored.** There is no package feed to pull it from yet, and the
alternative — a project reference to a directory outside this repository — is a
build that works on one machine and fails everywhere else. A copy that is
obviously a copy is easier to reason about than a path that is silently wrong.

**Do not edit these files.** A change made here is a change that disappears the
next time the library is updated. Everything a store is expected to customise is
an interface it can replace from its own code, and this shop replaces two of
them in `src/Bojan.Api`:

- `IKnightCredentialStore` — where the credential an operator entered is kept;
- `IKnightProxyIdentity` — how this shop's own roles map onto
  `anonymous` / `customer` / `staff`.

The store's own event names and UI slots are set once at start-up through
`StoreEventCatalogue`, which is the only mutable state the library exposes on
purpose.

**Updating it.** Copy the `.cs` files and the `.csproj` from
`stores/dotnet-store-agent/src/Knight.StoreAgent` over this directory and run the
tests. The library targets `net8.0` so that stores on either .NET version can use
it; this repository is on `net10.0`, which references it without trouble.
