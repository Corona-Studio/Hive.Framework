# Shared source generation

Hive DataSync and Application generators embed helpers from the private [Corona.SourceGeneration](https://github.com/Corona-Studio/Corona.SourceGeneration) repository through the pinned `SourceGeneration` Git submodule. Business attributes and generated APIs remain in Hive.

Use `git submodule update --init --recursive` after cloning. CI's `ACCESS_TOKEN` must have read access to the private shared repository. Import `SourceGeneration.props` only in generator projects; no extra helper DLL is needed at runtime. Update the common repository first, then test and commit the new submodule pin here.

Run `dotnet test Hive.SourceGeneration.Tests/Hive.SourceGeneration.Tests.csproj`. Tests compile generated nested partial sync objects, exercise change draining, validate custom serializers/options and handler context unwrapping, and check incremental caching. Unsupported sync field types require `CustomSerializer`; sync targets and containing types must be partial. Application binders currently support top-level non-generic applications with public/internal handlers, matching the binder provider's naming convention.
