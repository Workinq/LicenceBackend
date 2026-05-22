import {
  CircleCheckIcon,
  InfoIcon,
  Loader2Icon,
  OctagonXIcon,
  TriangleAlertIcon,
} from "lucide-react"
import { Toaster as Sonner, type ToasterProps } from "sonner"
import { useThemeStore } from "@/theme/theme-store"

const Toaster = ({ ...props }: ToasterProps) => {
  const mode = useThemeStore((s) => s.mode)
  return (
    <Sonner
      theme={mode}
      richColors
      className="toaster group"
      icons={{
        success: <CircleCheckIcon className="size-4" />,
        info: <InfoIcon className="size-4" />,
        warning: <TriangleAlertIcon className="size-4" />,
        error: <OctagonXIcon className="size-4" />,
        loading: <Loader2Icon className="size-4 animate-spin" />,
      }}
      style={
        {
          "--normal-bg": "var(--popover)",
          "--normal-text": "var(--popover-foreground)",
          "--normal-border": "var(--border)",

          "--success-bg": "var(--status-active-bg)",
          "--success-text": "var(--status-active-fg)",
          "--success-border": "var(--status-active-fg)",

          "--error-bg": "var(--status-revoked-bg)",
          "--error-text": "var(--status-revoked-fg)",
          "--error-border": "var(--status-revoked-fg)",

          "--warning-bg": "var(--status-suspended-bg)",
          "--warning-text": "var(--status-suspended-fg)",
          "--warning-border": "var(--status-suspended-fg)",

          "--info-bg": "var(--accent)",
          "--info-text": "var(--accent-foreground)",
          "--info-border": "var(--border-strong)",

          "--border-radius": "var(--radius)",
        } as React.CSSProperties
      }
      {...props}
    />
  )
}

export { Toaster }
