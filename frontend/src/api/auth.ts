import { http } from './client';

export interface LoginResp { token: string; username: string }

export async function login(username: string, password: string): Promise<LoginResp> {
  const { data } = await http.post<LoginResp>('/auth/login', { username, password });
  return data;
}

export async function changePassword(oldPassword: string, newPassword: string): Promise<void> {
  await http.post('/auth/change-password', { oldPassword, newPassword });
}
