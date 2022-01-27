## Local Source Packages

Aardvark.Build allows you to use locally-built packages that **override** nuget-dependencies. This is especially useful for testing things across multiple repositories and debugging.

For adding an external repository you need to create a file called `local.sources` in your repository-root that holds the source-project's path and build/pack commands. A little example:

```
/home/dev/myrepo
    dotnet build
    dotnet pack -o {OUTPUT}

/home/dev/otherrepo
    dotnet tool restore
    dotnet build MyProj/MyProj.fsproj
    dotnet paket pack {OUTPUT}
```

non-indented strings are interpreted as paths to the repository and all indented lines following are commands that create packages in the spliced folder-path `{OUTPUT}` provided by Aardvark.Build. 

All packages created this way will override their nuget/paket counterparts during build and startup. However we experienced some problems with auto-completion for newly added functions, etc.

Since building projects can be costly we reuse the packages whenever the source is unchanged. For this caching to work best it is strongly recommended that you use git-repositories as sources. It will work on non-git directories but might trigger rebuilds too often.

*transitive* `local.sources` overrides across repositories are also supported.