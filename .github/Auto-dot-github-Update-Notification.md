# `Auto-dot-github.yml` Update Notification

> [!TIP]
> Update this file will notify the listener repos of changes.

```yml
name: '[Auto] Repo Gardening'

on:
  schedule:
    - cron: "0 23 * * *"
  pull_request:  # PR labeler
    types:
      - opened
      - edited
      - closed
      - reopened
  # NOTE: draft release won't trigger any 'release' event.
  #       For workaround, use dispatch event to invoke auto-bump action
  workflow_dispatch:
    inputs:
      auto-bump:
        type: string
        description: |
          Auto Bump & CHANGELOG: 'v'-starting semver or leave empty to Sync
      auto-bump-args:
        type: string
        # DEBUG
        description: '**DEBUG**: NOT WORKING'
        default: csproj=GlobalPackageVersion
  workflow_call:   # Auto bump
    inputs:
      auto-bump:
        type: string
      auto-bump-args:
        type: string
        description: |

          Auto bump PR

          - npm
            --> Run `npm version "VERSION_WITHOUT_v_PREFIX" --no-git-tag-version`
            --> Then performs plain text replacement.

          - csproj=XmlTagName (e.g. csproj=MyVersionProperty)
            --> Finds the current version from the first matching .csproj/.props file.
            --> Then performs plain text replacement.

          NOTE: Always apply plain text replacement to entire codebase that is
                not XML-aware, not schema-aware. Carefully review PR.

      # Backward compatibility (May 2026)
      auto-bump-version:
        type: string

jobs:
  main:
    uses: sator-imaging/.github/.github/workflows/Auto-dot-github.yml@main
    secrets: inherit
    permissions:
      pull-requests: write
      contents: write  # Sync .github | Auto bump
      issues: write    # PR labeler
    with:
      auto-bump: ${{ inputs.auto-bump }}

      # See above
      auto-bump-args: ${{}}  ### <-- UPDATE HERE #######
```
