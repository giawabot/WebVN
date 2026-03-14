# WebVN

Browser-based visual novel staging editor built with Blazor WebAssembly.

The editor is script-first: writers own the story flow in CSV, while the editor handles scene layout, backgrounds, character placement, choice popup positioning, and audio cue names. Projects can be autosaved locally in the browser and exported as portable zip packages for handoff to a companion runtime.

> [!NOTE]
> Portions of this project were developed with LLMs such as Codex 5.3, GLM 4.7, and Qwen3.

## Current Scope
- Import a VN script from CSV.
- Stage dialogue rows with:
  - backgrounds
  - character placement, scale, tint, and flip
  - music cue names
  - one-shot SFX cue names
  - choice popup alignment
- Update an imported CSV while preserving scene edits by dialogue `Line ID#`.
- Open and export project packages as zip files.
- Autosave editor state locally in the browser.

## CSV Expectations
- `Dialogue` rows must have a unique `Line ID#`.
- Other row types may omit `Line ID#`.
- Supported columns currently include:
  - `Line ID#`
  - `Content`
  - `Action Type`
  - `Character`
  - `Condition`
  - `Action Effect`
  - `Notes`

- Supported action types currently include:
  - `StoryId`
  - `Dialogue`
  - `DialogueOption`
  - `JumpWhen`
  - `Jump`
  - `SetVariable`
  - `End`

The editor does not currently interpret `Condition` itself, but it preserves it in exported project JSON for downstream runtime use.

## Local Development
Run the editor locally:

```powershell
dotnet run --project WebVN.Editor\WebVN.Editor.csproj --urls http://localhost:5187
```

Restart using the helper script:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\restart-dev.ps1
```

## Project Workflow
1. Start a new project from CSV.
2. Upload backgrounds and character art locally in the editor.
3. Stage each dialogue line visually.
4. Export a package zip.
5. Re-open that package later to continue editing.

Project packages include:
- `project.json`
- extracted asset files referenced by the project

## Deployment
This app is a static Blazor WebAssembly site.

For a manual publish:

```powershell
dotnet publish WebVN.Editor\WebVN.Editor.csproj -c Release -o publish\WebVN.Editor
```

Upload the contents of:

`publish/WebVN.Editor/wwwroot`

### Cloudflare Pages
This repo includes `build.sh` for Cloudflare Pages.

Use:
- Build command: `./build.sh`
- Output directory: `output/wwwroot`
