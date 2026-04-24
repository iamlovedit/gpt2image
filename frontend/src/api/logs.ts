import { http } from './client';
import type { Paged } from './accounts';

export enum RequestBusinessStatus {
  Success = 0,
  UpstreamError = 1,
  AuthFailed = 2,
  RateLimited = 3,
  NoAvailableAccount = 4,
  ClientError = 5,
  InternalError = 6,
}

export const BusinessStatusLabel: Record<RequestBusinessStatus, string> = {
  [RequestBusinessStatus.Success]: '成功',
  [RequestBusinessStatus.UpstreamError]: '上游错误',
  [RequestBusinessStatus.AuthFailed]: '鉴权失败',
  [RequestBusinessStatus.RateLimited]: '限流',
  [RequestBusinessStatus.NoAvailableAccount]: '无可用账号',
  [RequestBusinessStatus.ClientError]: '调用方错误',
  [RequestBusinessStatus.InternalError]: '内部错误',
};

export interface RequestLog {
  id: string;
  requestId: string;
  clientKeyId: string | null;
  clientKeyName: string | null;
  upstreamAccountId: string | null;
  externalModel: string | null;
  upstreamModel: string | null;
  startedAt: string;
  completedAt: string | null;
  durationMs: number | null;
  httpStatus: number | null;
  businessStatus: RequestBusinessStatus;
  errorType: string | null;
  errorMessage: string | null;
  sseEventCount: number;
  retryCount: number;
  inputTokens: number | null;
  outputTokens: number | null;
  totalTokens: number | null;
  imageInputTokens: number | null;
  imageOutputTokens: number | null;
  imageTotalTokens: number | null;
}

export async function listLogs(params: {
  status?: RequestBusinessStatus;
  clientKeyId?: string;
  accountId?: string;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}): Promise<Paged<RequestLog>> {
  const { data } = await http.get('/logs', { params });
  return data;
}
