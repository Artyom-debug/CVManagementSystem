export function SocialIcon({ provider }: { provider: "google" | "facebook" }) {
  if (provider === "facebook")
    return (
      <svg
        width="20"
        height="20"
        viewBox="0 0 24 24"
        aria-hidden="true"
        focusable="false"
      >
        <circle cx="12" cy="12" r="12" fill="#1877F2" />
        <path
          fill="#fff"
          d="M16.67 15.47l.53-3.47h-3.33V9.75c0-.95.47-1.88 1.96-1.88h1.51V4.92s-1.37-.23-2.68-.23c-2.74 0-4.53 1.66-4.53 4.67V12H7.08v3.47h3.05v8.38a12.1 12.1 0 003.74 0v-8.38z"
        />
      </svg>
    );
  return (
    <svg
      width="20"
      height="20"
      viewBox="0 0 24 24"
      aria-hidden="true"
      focusable="false"
    >
      <path
        fill="#4285F4"
        d="M21.6 12.23c0-.71-.06-1.39-.18-2.05H12v3.88h5.38a4.6 4.6 0 01-2 3.02v2.52h3.24c1.9-1.75 2.98-4.33 2.98-7.37z"
      />
      <path
        fill="#34A853"
        d="M12 22c2.7 0 4.96-.9 6.62-2.4l-3.24-2.52c-.9.6-2.04.96-3.38.96-2.6 0-4.81-1.76-5.6-4.12H3.05v2.6A10 10 0 0012 22z"
      />
      <path
        fill="#FBBC05"
        d="M6.4 13.92a6 6 0 010-3.84v-2.6H3.05a10 10 0 000 9.04z"
      />
      <path
        fill="#EA4335"
        d="M12 5.96c1.47 0 2.79.5 3.82 1.5l2.86-2.86A9.61 9.61 0 0012 2a10 10 0 00-8.95 5.48l3.35 2.6A5.99 5.99 0 0112 5.96z"
      />
    </svg>
  );
}
