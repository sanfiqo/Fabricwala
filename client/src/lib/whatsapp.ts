// Help and share links. Set VITE_SUPPORT_WHATSAPP (digits with country code, e.g. 919000000000) in .env.local.
const SUPPORT = (import.meta.env.VITE_SUPPORT_WHATSAPP as string | undefined)?.replace(/\D/g, '') ?? ''

export function waLink(text: string, phone: string = SUPPORT): string {
  return `https://wa.me/${phone}?text=${encodeURIComponent(text)}`
}
