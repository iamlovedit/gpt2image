import { apiDelete, apiGet, apiPatch, apiPost } from './client';

export enum ClientKeyStatus { Active = 0, Disabled = 1 }

export interface ClientKey {
  id: string;
  name: string;
  keyPrefix: string;
  status: ClientKeyStatus;
  expiresAt: string | null;
  rpmLimit: number;
  concurrencyLimit: number;
  notes: string | null;
  createdAt: string;
  updatedAt: string;
}

export async function listKeys(): Promise<ClientKey[]> {
  return apiGet<ClientKey[]>('/keys');
}

export async function createKey(payload: {
  name: string;
  expiresAt?: string;
  rpmLimit?: number;
  concurrencyLimit?: number;
  notes?: string;
}): Promise<{ key: ClientKey; plaintext: string }> {
  return apiPost<{ key: ClientKey; plaintext: string }>('/keys', payload);
}

export async function updateKey(id: string, patch: Partial<{
  name: string;
  status: ClientKeyStatus;
  expiresAt: string | null;
  rpmLimit: number;
  concurrencyLimit: number;
  notes: string;
}>): Promise<ClientKey> {
  return apiPatch<ClientKey>(`/keys/${id}`, patch);
}

export async function deleteKey(id: string): Promise<void> {
  await apiDelete(`/keys/${id}`);
}
