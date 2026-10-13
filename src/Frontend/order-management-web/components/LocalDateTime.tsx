const FORMATS = {
  short: { dateStyle: "short", timeStyle: "short" },
  long: { dateStyle: "long", timeStyle: "short" },
  time: { timeStyle: "medium" },
} as const satisfies Record<string, Intl.DateTimeFormatOptions>;

/**
 * A timestamp rendered in the viewer timezone.
 *
 * The server has no way to know that timezone, so the markup it produces
 * legitimately differs from the client's. suppressHydrationWarning is the
 * supported escape hatch for exactly this case - React keeps the client value
 * and stops reporting the difference as an error.
 */
export function LocalDateTime({
  value,
  format = "short",
  className,
}: {
  value: string;
  format?: keyof typeof FORMATS;
  className?: string;
}) {
  const date = new Date(value);

  return (
    <time dateTime={value} className={className} suppressHydrationWarning>
      {new Intl.DateTimeFormat("pt-BR", FORMATS[format]).format(date)}
    </time>
  );
}
