---
name: EinvWorld Professional
colors:
  surface: '#f5fbf5'
  surface-dim: '#d5dcd6'
  surface-bright: '#f5fbf5'
  surface-container-lowest: '#ffffff'
  surface-container-low: '#eff5ef'
  surface-container: '#e9efe9'
  surface-container-high: '#e4eae4'
  surface-container-highest: '#dee4de'
  on-surface: '#171d19'
  on-surface-variant: '#3d4a42'
  inverse-surface: '#2c322e'
  inverse-on-surface: '#ecf2ec'
  outline: '#6d7a72'
  outline-variant: '#bccac0'
  surface-tint: '#006c4a'
  primary: '#006948'
  on-primary: '#ffffff'
  primary-container: '#00855d'
  on-primary-container: '#f5fff7'
  inverse-primary: '#68dba9'
  secondary: '#545e75'
  on-secondary: '#ffffff'
  secondary-container: '#d8e2fe'
  on-secondary-container: '#5a647c'
  tertiary: '#9b3e3b'
  on-tertiary: '#ffffff'
  tertiary-container: '#ba5551'
  on-tertiary-container: '#fffbff'
  error: '#ba1a1a'
  on-error: '#ffffff'
  error-container: '#ffdad6'
  on-error-container: '#93000a'
  primary-fixed: '#85f8c4'
  primary-fixed-dim: '#68dba9'
  on-primary-fixed: '#002114'
  on-primary-fixed-variant: '#005137'
  secondary-fixed: '#d8e2fe'
  secondary-fixed-dim: '#bcc6e1'
  on-secondary-fixed: '#111b2f'
  on-secondary-fixed-variant: '#3d475d'
  tertiary-fixed: '#ffdad7'
  tertiary-fixed-dim: '#ffb3ae'
  on-tertiary-fixed: '#410004'
  on-tertiary-fixed-variant: '#7f2928'
  background: '#f5fbf5'
  on-background: '#171d19'
  surface-variant: '#dee4de'
  surface-bg: '#F6F8FB'
  border-subtle: '#E6E8EB'
  success-green: '#10b981'
  warning-amber: '#f59e0b'
  error-red: '#ef4444'
  info-blue: '#0ea5e9'
typography:
  headline-xl:
    fontFamily: Inter
    fontSize: 32px
    fontWeight: '700'
    lineHeight: 40px
    letterSpacing: -0.02em
  headline-lg:
    fontFamily: Inter
    fontSize: 24px
    fontWeight: '600'
    lineHeight: 32px
    letterSpacing: -0.01em
  headline-md:
    fontFamily: Inter
    fontSize: 18px
    fontWeight: '600'
    lineHeight: 24px
  body-lg:
    fontFamily: Inter
    fontSize: 16px
    fontWeight: '400'
    lineHeight: 24px
  body-md:
    fontFamily: Inter
    fontSize: 14px
    fontWeight: '400'
    lineHeight: 20px
  body-sm:
    fontFamily: Inter
    fontSize: 12px
    fontWeight: '400'
    lineHeight: 18px
  label-md:
    fontFamily: Inter
    fontSize: 12px
    fontWeight: '600'
    lineHeight: 16px
    letterSpacing: 0.05em
  label-sm:
    fontFamily: Inter
    fontSize: 11px
    fontWeight: '500'
    lineHeight: 14px
rounded:
  sm: 0.125rem
  DEFAULT: 0.25rem
  md: 0.375rem
  lg: 0.5rem
  xl: 0.75rem
  full: 9999px
spacing:
  stack-sm: 0.5rem
  stack-md: 1rem
  stack-lg: 2rem
  gutter: 1.5rem
  margin-mobile: 1rem
  margin-desktop: 2rem
  container-max: 1280px
---

## Brand & Style
The brand identity is rooted in the **Corporate / Modern** aesthetic, specifically tailored for the fintech and compliance sectors. It balances administrative efficiency with modern digital reliability. 

The visual style is characterized by a "Clean Data" approach: high legibility, structured information density, and a calming professional color palette. It utilizes a refined version of the Tabler design language—combining subtle borders, soft shadows, and a systematic use of status-driven accents. The goal is to evoke a sense of security, accuracy, and ease of use in a high-stakes regulatory environment.

## Colors
The palette is dominated by a "Compliance Green" (#059669) that signals safety and validation. 

- **Primary & Success:** Green tones are used interchangeably for the brand and positive system statuses (Validated).
- **Surface Strategy:** The background uses a cool-toned gray-blue (#F6F8FB) to differentiate the page canvas from the pure white (#FFFFFF) component cards.
- **Functional Accents:** A rigorous semantic system is employed: Amber for pending states, Red for errors/overdue items, and Sky Blue for information/incoming tasks.
- **Borders:** Extremely subtle neutrals (#E6E8EB) define the structure without creating visual noise.

## Typography
The system relies exclusively on **Inter**, a typeface designed for user interfaces. 

- **Headlines:** Use tighter letter-spacing and heavier weights (600-700) to create a strong information hierarchy.
- **Labels:** Small caps or tracking-heavy labels are used for metadata and metric headers to distinguish them from actionable body text.
- **Data Display:** Numerical values in metrics use `headline-lg` to ensure immediate glanceability.
- **System Icons:** Material Symbols Outlined are used at a standard 20px size, vertically aligned to optical centers of text.

## Layout & Spacing
The system uses a **Fixed Grid** approach for desktop, centering content within a 1280px container, and transitions to a **Fluid Grid** for mobile devices.

- **Grid System:** A 12-column logic is used for large screens (8-column main content, 4-column sidebar).
- **Vertical Rhythm:** Spacing is managed through a "Stack" system where `stack-md` (16px) is the standard for internal component padding and `stack-lg` (32px) separates major sections.
- **Responsive Behavior:** 
  - **Desktop:** 32px side margins, 24px gutters.
  - **Tablet:** 2-column metric grid.
  - **Mobile:** 16px side margins, single-column reflow for all cards and metrics.

## Elevation & Depth
Depth is communicated through **Low-contrast outlines** combined with **Ambient shadows**.

- **Cards:** Use a 1px solid border (#E6E8EB) and a very soft, diffused shadow (`0 2px 4px rgba(0,0,0,0.02)`). This keeps the UI feeling flat and professional while providing enough separation for layered interaction.
- **Hover States:** Cards elevate slightly (translateY -2px) and shadows deepen to `0 4px 6px rgba(0,0,0,0.05)` to indicate interactivity.
- **Interactive Layers:** Navigation bars and sticky elements use a solid white background with a bottom border rather than heavy shadows to maintain a "planar" feel.

## Shapes
The shape language is **Soft** and systematic. 

- **Standard Radius:** 4px (0.25rem) is the default for cards, input fields, and standard buttons, providing a precise, engineered look.
- **Large Radius:** 8px (0.5rem) is used for "container" elements like profile selectors or grouped button backgrounds.
- **Circular:** Full rounding is reserved for user avatars and specific status indicators (e.g., the activity timeline pips).

## Components
- **Buttons:** Primary buttons use solid background fills with white text. Secondary buttons use ghost styles (border only) or subtle gray fills.
- **Cards (Tabler-style):** The core building block. Pure white background, 4px border-radius, and 1px border. Metric cards may feature a 4px left-border accent using semantic colors.
- **Status Pills:** Small, high-contrast badges with 10% opacity backgrounds of the semantic color and 100% opacity text. They must include a leading icon.
- **Tables:** Zebra-striping is applied to even rows using `#F6F8FB`. Header rows use a subtle `#e9efe9` fill.
- **Quick Action Grid:** A dedicated component using 2-column layout on sidebars; items feature top-aligned icons and bottom-aligned labels, responding to hover with brand-color borders.
- **Progress Bars:** Thin 8px tracks using `surface-container` backgrounds and solid primary fills for data visualization.