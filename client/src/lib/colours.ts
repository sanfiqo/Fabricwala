// Colour name -> swatch. This is product data (what "Maroon" looks like), not UI theme, so it lives here and not in index.css.
// Unknown names fall back to a grey swatch with the first letter.
const SWATCHES: Record<string, string> = {
  cream: '#e6dccb', white: '#f6f4ef', ivory: '#efe8d8', beige: '#d9c9ac', black: '#2b2a28', charcoal: '#3e3b36', grey: '#9a9892', gray: '#9a9892',
  navy: '#27395a', blue: '#4e6f93', sky: '#8db4d6', red: '#b3392f', maroon: '#7a2e3a', wine: '#6e2234', rust: '#c9705a', orange: '#d98a3a',
  mustard: '#c99a2e', yellow: '#e2bd4b', green: '#4f7a54', olive: '#7a8c6a', sage: '#9fb2a2', teal: '#3f7f80', pink: '#d98ba0', peach: '#eab08f',
  purple: '#6c4a86', brown: '#7a5a3c', camel: '#b98d5a', tan: '#b7a58a', khaki: '#a99a72', multi: '#c9b79a', natural: '#d8cdb8',
}

export function swatchFor(name: string): string | null {
  const key = name.trim().toLowerCase()
  if (SWATCHES[key]) return SWATCHES[key]
  const first = Object.keys(SWATCHES).find((k) => key.includes(k))
  return first ? SWATCHES[first] : null
}
