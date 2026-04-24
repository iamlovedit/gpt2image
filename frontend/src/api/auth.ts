import { apiPost } from './client';

export interface LoginResp { token: string; username: string }

export async function login(username: string, password: string): Promise<LoginResp> {
  return apiPost<LoginResp>('/auth/login', { username, password });
}

export async function changePassword(oldPassword: string, newPassword: string): Promise<void> {
  await apiPost<void>('/auth/change-password', { oldPassword, newPassword });
}
