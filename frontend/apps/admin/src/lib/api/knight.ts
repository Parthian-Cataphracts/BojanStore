import { api, useMockData } from './client';

/**
 * Where a delivered Feature's screen hangs in this panel.
 *
 * A slot and a URL rather than markup: the shop decides what a sidebar entry
 * looks like and the Feature decides what is behind it, which is the only
 * division that survives the panel being restyled.
 */
export interface KnightMountDto {
  slot: string;
  label: string;
  path: string;
  kind: string;
}

export interface KnightFeatureDto {
  slug: string;
  version: string;
  /** Installed and enabled are separate facts, and the screen shows both. */
  enabled: boolean;
  architecture: string;
  /** Whether the shared secret this Feature's service needs has reached the shop. */
  hasServiceSecret: boolean;
  /** The routes this shop now serves on its behalf. */
  routes: string[];
  mounts: KnightMountDto[];
}

/**
 * What the shop knows about its connection to KNIGHT.
 *
 * No secret in it, in either direction. Whether the shop is connected, when it
 * last worked and what has been delivered are all answerable without one, which
 * is what makes this safe to render on a screen somebody else can see.
 */
export interface KnightStatusDto {
  configured: boolean;
  enabled: boolean;
  connected: boolean;
  baseUrl: string;
  clientId: string;
  storeId: string;
  storeName: string;
  slug: string;
  integrationStatus: string;
  lastHandshakeAt: string | null;
  lastHeartbeatAt: string | null;
  lastJobAt: string | null;
  lastJob: string;
  lastError: string;
  lastErrorAt: string | null;
  proxyBasePath: string;
  features: KnightFeatureDto[];
}

const disconnected: KnightStatusDto = {
  configured: false,
  enabled: false,
  connected: false,
  baseUrl: '',
  clientId: '',
  storeId: '',
  storeName: '',
  slug: '',
  integrationStatus: '',
  lastHandshakeAt: null,
  lastHeartbeatAt: null,
  lastJobAt: null,
  lastJob: '',
  lastError: '',
  lastErrorAt: null,
  proxyBasePath: '/api/features',
  features: [],
};

/**
 * The connection, as the panel shows it.
 *
 * Falls back to "not connected" rather than throwing, and the fixture path says
 * the same thing. A screen about whether something is working must not itself
 * be the thing that is broken — and inventing a connected shop in mock mode
 * would be claiming a link to a control plane that is not there.
 */
export async function getKnightStatus(): Promise<KnightStatusDto> {
  if (useMockData) return disconnected;

  return api.get<KnightStatusDto>('/knight/status', { auth: true }).catch(() => disconnected);
}
