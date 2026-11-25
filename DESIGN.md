# DRANOTCH Design Direction

## Identity & Personality

- **Product**: DRANOTCH, a Windows top-bezel Dynamic Island utility for music, lyrics, prayer times, to-do lists, radio, weather, reminders, sticky notes, and screenshots.
- **Visual Language**: Compact, minimalist, utilitarian dark surface anchored to the top bezel. Precision over ornament: flat matte surfaces, crisp 1px borders, tight spacing, and clear typographic hierarchy.
- **Dial**: `ENERGY 1 / RHYTHM 2 / MOTION 1`
  - **ENERGY 1**: Calm, unobtrusive desktop companion that stays out of the user's way until hovered or opened.
  - **RHYTHM 2**: Consistent compact grid for tool tiles, broken intentionally by wider horizontal status pills (music, to-do, prayer) that carry live text.
  - **MOTION 1**: Subtle hover/press states and brief functional notch expand/collapse transitions only; no idle loops, pulses, or decorative floating animations.

## Palette (WCAG AA Verified)

- **Neutral Base Surfaces** (matte dark, chosen because the notch sits flush against the top display bezel and must blend cleanly across desktop wallpapers):
  - `SurfaceBase`: `#09090C` (main notch and primary window canvas)
  - `SurfaceElevated`: `#121216` (cards, pills, input fields)
  - `SurfaceInteractive`: `#1A1A21` (buttons, icon tiles at rest)
  - `SurfaceHover`: `#24242E` (hover fill)
  - `SurfacePressed`: `#2E2E3B` (active/pressed fill)
- **Borders**:
  - `BorderSubtle`: `#22222B` (hairline 1px separation)
  - `BorderInteractive`: `#323240` (control edges)
  - `BorderFocus`: `#E4E4EC` (high-contrast keyboard focus ring)
- **Typography Colors**:
  - `TextPrimary`: `#F4F4F8` (headings, active labels)
  - `TextSecondary`: `#C8C8D4` (artists, secondary details)
  - `TextMuted`: `#9A9AA8` (timestamps, hints, metadata; verified >= 4.5:1 on `#121216` and `#1A1A21`)
- **Single Deliberate Accent**:
  - `AccentActive`: `#30D158` (used strictly for real live states: active playback/module indicator dot, next prayer countdown, update available, completed check).
- **Semantic Status Only**:
  - `StatusDanger`: `#EF4444` (destructive hover/close/delete actions)
  - `StatusWarning`: `#F59E0B` (active sticky notes / deadline warning)
  - `StatusInfo`: `#38BDF8` (active smart reminders / screenshot indicator)

## Typography

- **Font Family**: `Segoe UI Variable Display, Segoe UI` (native Windows 11 system UI typeface for crisp ClearType rendering at small sizes).
- **Compact Scale**:
  - Window / Section Title: `13px` SemiBold
  - Primary Row / Track Title: `12px` SemiBold
  - Body / Control Label: `11px`–`11.5px` Medium/Regular
  - Metadata / Badge / Countdown: `10px` SemiBold

## Geometry & Spacing

- **Radius Scale (3 deliberate tiers, no uniform pill-everything)**:
  - `RadiusSm`: `6` (compact buttons, inputs, status chips)
  - `RadiusMd`: `10` (inner cards, lyrics box, schedule container)
  - `RadiusLg`: `14` (detached floating windows)
  - `RadiusNotch`: `0,0,16,16` (top-bezel notch shell)
- **Compact Sizing**:
  - Notch icon tiles: `32x32` (radius `8`), 15px crisp flat `#E4E4EC` icons (no metallic gradients).
  - Notch logo tile: `34x34`, 20px icon.
  - Notch horizontal live pills: `Height="34"` (top row), `Height="26"` (bottom status row).
  - Floating window outer shadow: subtle elevation marker only (`BlurRadius="12"`, `ShadowDepth="3"`, `Opacity="0.45"`).

## Identity Motif

- A precision matte-black bezel shell with 1px hairline borders, crisp monochrome vector glyphs, and a single 5px live status dot only when a module is genuinely active.
