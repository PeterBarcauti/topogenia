// Logo.tsx
// The mark of the product: an orthogonal route across two lanes, drawn with the tokens of the charter (B-1.2-07, mockup).

export function Logo({ size = 24 }: { size?: number }) {
  return (
    <svg className="logo" width={size} height={size} viewBox="0 0 24 24" aria-hidden="true" focusable="false">
      <rect x="0.5" y="0.5" width="23" height="23" rx="6.5" fill="var(--accent)" />
      <rect x="0.5" y="0.5" width="23" height="11.5" rx="6.5" fill="var(--accent-strong)" opacity=".35" />
      <path d="M6 8h4.5a2 2 0 0 1 2 2v4a2 2 0 0 0 2 2H18" fill="none" stroke="var(--surface)" strokeWidth="2" strokeLinecap="round" />
      <circle cx="6" cy="8" r="1.8" fill="var(--surface)" />
      <circle cx="18" cy="16" r="1.8" fill="var(--surface)" />
    </svg>
  );
}
