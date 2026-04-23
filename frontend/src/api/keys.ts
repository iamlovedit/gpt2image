import { http } from './client';

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
  const { data } = await http.get('/keys');
  return data;
}

export async function createKey(payload: {
  name: string;
  expiresAt?: string;
  rpmLimit?: number;
  concurrencyLimit?: number;
  notes?: string;
}): Promise<{ key: ClientKey; plaintext: string }> {
  const { data } = await http.post('/keys', payload);
  return data;
}

export async function updateKey(id: string, patch: Partial<{
  name: string;
  status: ClientKeyStatus;
  expiresAt: string | null;
  rpmLimit: number;
  concurrencyLimit: number;
  notes: string;
}>): Promise<ClientKey> {
  const { data } = await http.patch(`/keys/${id}`, patch);
  return data;
}

export async function deleteKey(id: string): Promise<void> {
  await http.delete(`/keys/${id}`);
}
