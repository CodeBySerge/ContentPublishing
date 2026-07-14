---
name: open-code-ui-design
description: "Design and implement intentional UI in existing apps using Open Code AI style prompts. Use when creating new screens, redesigning pages, improving usability, and refining responsive behavior without breaking existing workflows."
argument-hint: "Screen or flow to design, constraints, and style direction"
user-invocable: true
---

# Open Code UI Design

Create production-ready UI updates that are visually intentional, accessible, responsive, and compatible with the current codebase.

## When to Use

- Designing a new page, dashboard, form, or component.
- Refreshing legacy UI while preserving backend behavior.
- Converting plain layouts into a clear visual system.
- Improving role-based pages where actions differ by user type.

## Inputs to Collect

- Target screen or flow: what page needs design work.
- Constraints: framework, CSS stack, browser support, no-regression requirements.
- Visual direction: tone, typography, color mood, density, and examples.
- Success criteria: what must be true when done.

If any input is missing, ask for it briefly before coding.

## Default Profile

- CSS stack: Tailwind-first. Add plain CSS only when Tailwind utility classes are insufficient.
- Visual tone: clinical and professional (clean, trustworthy, low-noise).
- Scope: workspace skill intended for project use in this repository.

## Workflow

1. Confirm scope and constraints.
2. Inspect current UI implementation and shared layout/style files.
3. Choose visual direction:
   - If an existing design language exists, extend it.
   - If the UI is inconsistent, define a compact token set (color, spacing, radius, typography).
4. Draft interaction model:
   - Primary actions, secondary actions, empty/loading/error states.
   - Desktop and mobile behavior.
5. Implement in small slices:
   - Update markup structure first.
   - Apply CSS variables/utilities.
   - Add minimal, meaningful motion.
6. Preserve behavior:
   - Do not change business logic unless requested.
   - Keep existing routes, form posts, and validation wiring intact.
7. Verify with evidence:
   - Build passes.
   - Page loads without server errors.
   - Core actions are still reachable.
8. Report what changed and any follow-up improvements.

## Decision Points

- Existing design system present:
  - Yes: keep naming, spacing rhythm, and component language.
  - No: introduce a minimal design token layer in the page or shared stylesheet.
- Layout complexity:
  - Small: single-pass implementation.
  - Large: split into sections and validate each section.
- Risk level:
  - High-risk forms/auth screens: prioritize safe structural changes over visual experimentation.

## Quality Checklist

- Visual hierarchy is clear at first glance.
- Contrast and focus states are accessible.
- Works at common mobile and desktop breakpoints.
- Empty and error states are explicit.
- No broken links, missing assets, or console/server errors.
- Naming and structure remain maintainable.

## Output Format

- Summary of design direction.
- List of files changed.
- Verification evidence (build/run/check results).
- Optional next-step enhancements.

## Example Prompts

- /open-code-ui-design Redesign the Home dashboard for OneHealthHandbook with a calm clinical look, keeping existing routes and role-based actions.
- /open-code-ui-design Improve the login and account pages for clarity and trust, mobile-first, no backend changes.
- /open-code-ui-design Create a consistent card and button system for all admin screens using existing Tailwind usage.
