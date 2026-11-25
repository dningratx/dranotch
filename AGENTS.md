# Project Agent Instructions (DRANOTCH)

## Antislop-AI Routing

- **UI & Visual Design**: Before designing or modifying any XAML view, window, or visual component, read `DESIGN.md` for the product direction, then apply `.agents/skills/antislop/SKILL.md`, `.agents/skills/antislop-ui/SKILL.md`, and `.agents/skills/antislop-human/SKILL.md` as active filters.
- **Code & Comments**: When editing C# or XAML code comments, apply `.agents/skills/antislop-code/SKILL.md`. Keep comments short, sentence-case, and limited to non-obvious constraints or workarounds; remove decorative banners, obvious restatements, and decorative emojis.
- **Verification**: Always run `dotnet build NotchMusic.slnx` and `dotnet test` after UI or code changes before delivering.
