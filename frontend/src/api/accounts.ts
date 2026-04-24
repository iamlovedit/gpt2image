import { apiDelete, apiGet, apiPatch, apiPost } from './client';

export enum UpstreamAccountStatus {
  Healthy = 0,
  Cooling = 1,
  RateLimited = 2,
  Banned = 3,
  Invalid = 4,
  Disabled = 5,
}

export const AccountStatusLabel: Record<UpstreamAccountStatus, string> = {
  [UpstreamAccountStatus.Healthy]: '健康',
  [UpstreamAccountStatus.Cooling]: '冷却中',
  [UpstreamAccountStatus.RateLimited]: '限流',
  [UpstreamAccountStatus.Banned]: '封禁',
  [UpstreamAccountStatus.Invalid]: '失效',
  [UpstreamAccountStatus.Disabled]: '禁用',
};

export const AccountStatusClass: Record<UpstreamAccountStatus, string> = {
  [UpstreamAccountStatus.Healthy]: 'status-tag-healthy',
  [UpstreamAccountStatus.Cooling]: 'status-tag-cooling',
  [UpstreamAccountStatus.RateLimited]: 'status-tag-cooling',
  [UpstreamAccountStatus.Banned]: 'status-tag-banned',
  [UpstreamAccountStatus.Invalid]: 'status-tag-invalid',
  [UpstreamAccountStatus.Disabled]: 'status-tag-invalid',
};

export interface Account {
  id: string;
  accessTokenPreview: string;
  refreshTokenPreview: string;
  chatGptAccountId: string | null;
  accessTokenExpiresAt: string | null;
  status: UpstreamAccountStatus;
  coolingUntil: string | null;
  lastError: string | null;
  lastUsedAt: string | null;
  successCount: number;
  failureCount: number;
  concurrencyLimit: number;
  notes: string | null;
  name: string | null;
  email: string | null;
  platform: string | null;
  accountType: string | null;
  proxyKey: string | null;
  priority: number | null;
  rateMultiplier: number | null;
  autoPauseOnExpired: boolean | null;
  chatGptUserId: string | null;
  clientId: string | null;
  organizationId: string | null;
  planType: string | null;
  subscriptionExpiresAt: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface Paged<T> {
  total: number;
  page: number;
  pageSize: number;
  items: T[];
}

export async function listAccounts(params: {
  status?: UpstreamAccountStatus;
  keyword?: string;
  page?: number;
  pageSize?: number;
}): Promise<Paged<Account>> {
  return apiGet<Paged<Account>>('/accounts', { params });
}

export enum ImportStrategy { Skip = 0, Overwrite = 1, Fail = 2 }

export interface ImportItem {
  accessToken: string;
  refreshToken: string;
  chatGptAccountId?: string;
  accessTokenExpiresAt?: string | number;
  concurrencyLimit?: number;
  notes?: string;
  name?: string;
  email?: string;
  platform?: string;
  accountType?: string;
  proxyKey?: string;
  priority?: number;
  rateMultiplier?: number;
  autoPauseOnExpired?: boolean;
  chatGptUserId?: string;
  clientId?: string;
  organizationId?: string;
  planType?: string;
  subscriptionExpiresAt?: string | number;
  rawMetadataJson?: string;
}

export async function importAccounts(items: ImportItem[], strategy: ImportStrategy) {
  return apiPost<{ inserted: number; updated: number; skipped: number }>('/accounts/import', { items, strategy });
}

export async function updateAccount(id: string, patch: {
  status?: UpstreamAccountStatus;
  notes?: string;
  concurrencyLimit?: number;
  chatGptAccountId?: string;
}): Promise<Account> {
  return apiPatch<Account>(`/accounts/${id}`, patch);
}

export async function refreshAccount(id: string): Promise<Account> {
  return apiPost<Account>(`/accounts/${id}/refresh`);
}

export interface AccountTestResult {
  ok: boolean;
  httpStatus: number | null;
  message: string;
  durationMs: number;
  status: UpstreamAccountStatus;
}

export async function testAccount(id: string): Promise<AccountTestResult> {
  return apiPost<AccountTestResult>(`/accounts/${id}/test`);
}

export async function deleteAccount(id: string): Promise<void> {
  await apiDelete(`/accounts/${id}`);
}
