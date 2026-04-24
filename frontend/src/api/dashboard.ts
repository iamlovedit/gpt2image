import { http } from './client';
import type { RequestLog } from './logs';
import type { UpstreamAccountStatus } from './accounts';

export interface DashboardSummary {
  total24h: number;
  success24h: number;
  successRate: number | null;
  avgDurationMs: number;
  inputTokens24h: number;
  outputTokens24h: number;
  totalTokens24h: number;
  imageTotalTokens24h: number;
  accountTotal: number;
  accountHealthy: number;
  accountByStatus: Array<{ status: UpstreamAccountStatus; count: number }>;
  keysActive: number;
  keysTotal: number;
  recent: RequestLog[];
}

export async function getSummary(): Promise<DashboardSummary> {
  const { data } = await http.get('/dashboard/summary');
  return data;
}
