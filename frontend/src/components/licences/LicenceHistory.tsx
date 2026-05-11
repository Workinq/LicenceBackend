import { useState } from 'react';
import { useQuery, keepPreviousData } from '@tanstack/react-query';
import { History, Link2, CheckCircle2, XCircle } from 'lucide-react';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Button } from '@/components/ui/button';
import { AuditTimeline, type AuditEvent } from '@/components/AuditTimeline';
import {
  fetchLicenceStatusHistory,
  fetchLicenceBindingHistory,
  fetchLicenceVerificationAttempts,
} from '@/api/licences';
import type {
  LicenceStatusHistoryResponse,
  BindingHistoryEntryResponse,
  VerificationAttemptResponse,
} from '@/api/generated/api.schemas';

const PAGE = 20;

function formatJson(v: unknown): string {
  if (v == null) return 'none';
  if (typeof v === 'string') return v;
  return JSON.stringify(v);
}

function PagedAuditTab<T>({
  queryKey,
  queryFn,
  mapEvent,
  emptyText,
}: {
  queryKey: unknown[];
  queryFn: (params: { limit: number; offset: number }) => Promise<{ items: T[]; total: number; limit: number; offset: number }>;
  mapEvent: (item: T) => AuditEvent;
  emptyText: string;
}) {
  const [offset, setOffset] = useState(0);
  const query = useQuery({
    queryKey: [...queryKey, offset],
    queryFn: () => queryFn({ limit: PAGE, offset }),
    placeholderData: keepPreviousData,
  });
  const data = query.data;
  const events = (data?.items ?? []).map(mapEvent);
  return (
    <div className="space-y-4">
      <AuditTimeline events={events} isLoading={query.isPending} isError={query.isError} emptyText={emptyText} />
      {data && data.total > PAGE && (
        <div className="flex items-center justify-between text-sm text-ink-muted">
          <span>
            {data.offset + 1}-{Math.min(data.offset + data.limit, data.total)} of {data.total}
          </span>
          <div className="flex gap-2">
            <Button variant="outline" size="sm" disabled={offset === 0} onClick={() => { setOffset(Math.max(0, offset - PAGE)); }}>
              Previous
            </Button>
            <Button variant="outline" size="sm" disabled={offset + PAGE >= data.total} onClick={() => { setOffset(offset + PAGE); }}>
              Next
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}

export function LicenceHistory({ licenceId }: { licenceId: string }) {
  return (
    <Tabs defaultValue="status">
      <TabsList>
        <TabsTrigger value="status">Status history</TabsTrigger>
        <TabsTrigger value="bindings">Binding history</TabsTrigger>
        <TabsTrigger value="verifications">Verification attempts</TabsTrigger>
      </TabsList>
      <TabsContent value="status" className="pt-4">
        <PagedAuditTab<LicenceStatusHistoryResponse>
          queryKey={['licences', 'history', 'status', licenceId]}
          queryFn={(p) => fetchLicenceStatusHistory(licenceId, p)}
          emptyText="No status changes yet."
          mapEvent={(h) => ({
            id: h.id,
            icon: History,
            title: `${h.previousStatus} -> ${h.newStatus}`,
            meta: [h.changedByEmail ?? h.changedBy, h.reason].filter(Boolean).join(' - ') || undefined,
            timestamp: h.changedAt,
          })}
        />
      </TabsContent>
      <TabsContent value="bindings" className="pt-4">
        <PagedAuditTab<BindingHistoryEntryResponse>
          queryKey={['licences', 'history', 'bindings', licenceId]}
          queryFn={(p) => fetchLicenceBindingHistory(licenceId, p)}
          emptyText="No binding changes yet."
          mapEvent={(b) => ({
            id: b.id,
            icon: Link2,
            title: `${b.bindingType}: ${formatJson(b.previousValue)} -> ${formatJson(b.newValue)}`,
            meta: [b.changeSource, b.reason].filter(Boolean).join(' - ') || undefined,
            timestamp: b.changedAt,
          })}
        />
      </TabsContent>
      <TabsContent value="verifications" className="pt-4">
        <PagedAuditTab<VerificationAttemptResponse>
          queryKey={['licences', 'history', 'verifications', licenceId]}
          queryFn={(p) => fetchLicenceVerificationAttempts(licenceId, p)}
          emptyText="No verification attempts yet."
          mapEvent={(v) => ({
            id: v.id,
            icon: v.outcome === 'granted' ? CheckCircle2 : XCircle,
            title: v.denialReason ? `${v.outcome}: ${v.denialReason}` : v.outcome,
            meta: v.hwidFingerprint ? `${v.sourceIp} - HWID ${v.hwidFingerprint}` : v.sourceIp,
            timestamp: v.attemptedAt,
          })}
        />
      </TabsContent>
    </Tabs>
  );
}
