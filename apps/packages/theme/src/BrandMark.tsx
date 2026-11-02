/**
 * The brand mark: an aurora arcing over a hotel's roofline, in a rounded square.
 */
export function BrandMark({ size = 32 }: { size?: number }) {
  return (
    <svg className="brand-mark" width={size} height={size} viewBox="0 0 32 32" aria-hidden="true" focusable="false">
      <defs>
        <linearGradient id="brand-mark-fill" x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" stopColor="#1b2a6b" />
          <stop offset="1" stopColor="#3b1f6e" />
        </linearGradient>
        <linearGradient id="brand-mark-aurora" x1="0" y1="0" x2="1" y2="0">
          <stop offset="0" stopColor="#43ffb5" />
          <stop offset=".6" stopColor="#2bd8ff" />
          <stop offset="1" stopColor="#b07bff" />
        </linearGradient>
      </defs>
      <rect width="32" height="32" rx="8" fill="url(#brand-mark-fill)" />
      <path d="M5 15 C 10 6, 22 6, 27 13" fill="none" stroke="url(#brand-mark-aurora)" strokeWidth="2.6" strokeLinecap="round" />
      <path d="M8 26 V18 H12 V14 L16 11 L20 14 V18 H24 V26 Z" fill="#fff" opacity=".95" />
      <path d="M15 26 V22 H17 V26 Z M10 20 H11 V21 H10 Z M21 20 H22 V21 H21 Z M15 16 H17 V17.5 H15 Z" fill="#ffc766" />
    </svg>
  );
}
