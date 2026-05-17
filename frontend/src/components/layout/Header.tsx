import { useNavigate } from '@tanstack/react-router';
import { ChevronDown, UserRound } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { ThemeMenu } from '@/components/ThemeMenu';
import { useAccessTokenStore } from '@/auth/access-token-store';
import { apiClient } from '@/auth/api-client';

export function Header() {
  const navigate = useNavigate();
  const user = useAccessTokenStore((s) => s.user);

  const endSession = async (path: '/sessions' | '/sessions/all') => {
    try {
      await apiClient<void>(path, { method: 'DELETE' });
    } finally {
      useAccessTokenStore.getState().clear();
      window.location.assign('/login');
    }
  };

  return (
    <header className="flex h-14 shrink-0 items-center justify-between border-b border-border bg-surface-elevated px-4 md:px-6">
      <span className="font-display text-lg font-semibold text-ink">LicenceBackend</span>

      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button variant="ghost" className="gap-2" aria-label="Account menu">
            <UserRound className="size-4" aria-hidden="true" />
            <span className="hidden text-sm sm:inline">{user?.email ?? 'Account'}</span>
            <ChevronDown className="size-4 opacity-60" aria-hidden="true" />
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" className="w-60">
          <DropdownMenuLabel className="flex flex-col gap-1">
            <span className="text-sm font-medium text-ink">{user?.email ?? 'Unknown user'}</span>
            {user?.role && (
              <Badge variant="secondary" className="w-fit capitalize">
                {user.role}
              </Badge>
            )}
          </DropdownMenuLabel>
          <DropdownMenuSeparator />
          <ThemeMenu />
          <DropdownMenuItem onSelect={() => void navigate({ to: '/admin/me' })}>
            My profile
          </DropdownMenuItem>
          <DropdownMenuSeparator />
          <DropdownMenuItem onSelect={() => void endSession('/sessions')}>
            Sign out
          </DropdownMenuItem>
          <DropdownMenuItem
            variant="destructive"
            onSelect={() => void endSession('/sessions/all')}
          >
            Sign out everywhere
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
    </header>
  );
}
