import { Moon, Sun, Monitor } from 'lucide-react';
import {
  DropdownMenuLabel,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuSeparator,
} from '@/components/ui/dropdown-menu';
import { useThemeStore, type ThemeMode } from '@/theme/theme-store';

export function ThemeMenu() {
  const mode = useThemeStore((s) => s.mode);
  const setMode = useThemeStore((s) => s.setMode);

  return (
    <>
      <DropdownMenuLabel className="text-xs font-normal text-ink-muted">Theme</DropdownMenuLabel>
      <DropdownMenuRadioGroup value={mode} onValueChange={(v) => { setMode(v as ThemeMode); }}>
        <DropdownMenuRadioItem value="light">
          <Sun className="size-4" aria-hidden="true" /> Light
        </DropdownMenuRadioItem>
        <DropdownMenuRadioItem value="dark">
          <Moon className="size-4" aria-hidden="true" /> Dark
        </DropdownMenuRadioItem>
        <DropdownMenuRadioItem value="system">
          <Monitor className="size-4" aria-hidden="true" /> System
        </DropdownMenuRadioItem>
      </DropdownMenuRadioGroup>
      <DropdownMenuSeparator />
    </>
  );
}
