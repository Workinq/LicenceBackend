import { createFileRoute } from '@tanstack/react-router';
import { ProfileEditor } from '@/components/ProfileEditor';

export const Route = createFileRoute('/admin/me')({
  component: ProfileEditor,
});
