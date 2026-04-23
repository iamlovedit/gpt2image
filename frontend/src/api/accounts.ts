import { http } from './client';

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
  const { data } = await http.get('/accounts', { params });
  return data;
}

export enum ImportStrategy { Skip = 0, Overwrite = 1, Fail = 2 }

export interface ImportItem {
  accessToken: string;
  refreshToken: string;
  chatGptAccountId?: string;
}

export async function importAccounts(items: ImportItem[], strategy: ImportStrategy) {
  const { data } = await http.post('/accounts/import', { items, strategy });
  return data as { inserted: number; updated: number; skipped: number };
}

export async function updateAccount(id: string, patch: {
  status?: UpstreamAccountStatus;
  notes?: string;
  concurrencyLimit?: number;
  chatGptAccountId?: string;
}): Promise<Account> {
  const { data } = await http.patch(`/accounts/${id}`, patch);
  return data;
}

export async function refreshAccount(id: string): Promise<Account> {
  const { data } = await http.post(`/accounts/${id}/refresh`);
  return data;
}

export async function deleteAccount(id: string): Promise<void> {
  await http.delete(`/accounts/${id}`);
}
