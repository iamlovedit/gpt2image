import { http } from './client';
import type { RequestLog } from './logs';
import type { RequestBusinessStatus } from './logs';
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

export type DashboardStatsRange = 'today' | '7d' | '30d';

export interface RequestStatsBucket {
  bucketStart: string;
  label: string;
  requestCount: number;
  successCount: number;
  failureCount: number;
  successRate: number | null;
  avgDurationMs: number;
  inputTokens: number;
  outputTokens: number;
  totalTokens: number;
  imageTotalTokens: number;
}

export interface StatusBreakdownItem {
  status: RequestBusinessStatus;
  count: number;
}

export interface ErrorTypeBreakdownItem {
  errorType: string;
  count: number;
}

export interface AccountBreakdownItem {
  accountId: string | null;
  name: string;
  requestCount: number;
  successCount: number;
  failureCount: number;
}

export interface KeyBreakdownItem {
  clientKeyId: string | null;
  name: string;
  requestCount: number;
  successCount: number;
  failureCount: number;
}

export interface RequestStatsResponse {
  range: DashboardStatsRange;
  bucketUnit: 'hour' | 'day';
  series: RequestStatsBucket[];
  statusBreakdown: StatusBreakdownItem[];
  errorTypeBreakdown: ErrorTypeBreakdownItem[];
  accountBreakdown: AccountBreakdownItem[];
  keyBreakdown: KeyBreakdownItem[];
}

export async function getRequestStats(range: DashboardStatsRange): Promise<RequestStatsResponse> {
  const { data } = await http.get('/dashboard/request-stats', { params: { range } });
  return data;
}
