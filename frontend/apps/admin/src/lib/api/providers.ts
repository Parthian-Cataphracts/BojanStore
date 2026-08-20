import { api, useMockData } from './client';
import type {
  LoyaltyProgrammeDto,
  AdminShippingMethodDto,
  PaymentSettingsDto,
  SmsSettingsDto,
  VerificationSettingsDto,
  WebPushSettingsDto,
} from './types';

/**
 * The two outside services the shop pays for: the payment gateway and the SMS
 * account.
 *
 * No mock fixtures. Every other admin module falls back to sample data so the
 * screens can be worked on without a backend, but a fabricated gateway would
 * show an owner a merchant id that is configured when nothing is — and the
 * whole reason these screens exist is to say truthfully whether the shop can
 * take money and sign customers in. In mock mode they read as unconfigured,
 * which is what they are.
 */

const UNCONFIGURED_PAYMENT: PaymentSettingsDto = {
  gateway: {
    provider: 'none',
    useSandboxEndpoints: false,
    hasMerchantId: false,
    callbackUrl: '',
    description: '',
  },
  methods: { online: false, wallet: false, cashOnDelivery: false },
};

const UNCONFIGURED_SMS: SmsSettingsDto = {
  provider: 'none',
  hasApiKey: false,
  lineNumber: '',
  otpTemplateId: 0,
  otpParameterName: 'Code',
};

export async function getPaymentSettings(): Promise<PaymentSettingsDto> {
  if (useMockData) return UNCONFIGURED_PAYMENT;

  // Caught rather than thrown: the settings screen has to render so the owner
  // can fix whatever is wrong, and an API that is down is one of the things
  // they might be there to fix.
  return api
    .get<PaymentSettingsDto>('/payment/settings', { auth: true })
    .catch(() => UNCONFIGURED_PAYMENT);
}

export async function getSmsSettings(): Promise<SmsSettingsDto> {
  if (useMockData) return UNCONFIGURED_SMS;

  return api.get<SmsSettingsDto>('/sms/settings', { auth: true }).catch(() => UNCONFIGURED_SMS);
}

/**
 * Browser notifications.
 *
 * Unconfigured in mock mode and on a failed read, like the two above. Reporting
 * push as enabled when there is no key pair would show an owner a channel that
 * queues broadcasts nothing can deliver.
 */
const UNCONFIGURED_PUSH: WebPushSettingsDto = {
  enabled: false,
  publicKey: '',
  hasPrivateKey: false,
  subject: '',
};

export async function getWebPushSettings(): Promise<WebPushSettingsDto> {
  if (useMockData) return UNCONFIGURED_PUSH;

  return api.get<WebPushSettingsDto>('/push/settings', { auth: true }).catch(() => UNCONFIGURED_PUSH);
}

/**
 * Whether email and phone verification are required at sign-up/checkout —
 * both off by default in mock mode and on a failed read, matching the two
 * providers above: an owner should never be shown a requirement as active
 * when the panel could not actually confirm it.
 */
const VERIFICATION_OFF: VerificationSettingsDto = {
  requireEmailVerification: false,
  requirePhoneVerification: false,
};

export async function getVerificationSettings(): Promise<VerificationSettingsDto> {
  if (useMockData) return VERIFICATION_OFF;

  return api
    .get<VerificationSettingsDto>('/settings/verification', { auth: true })
    .catch(() => VERIFICATION_OFF);
}

/**
 * The shop's shipping tiers.
 *
 * Empty rather than a fixture when there is nothing to read — the form says so
 * plainly, because a shop with no shipping method cannot take an order and that
 * is worth seeing rather than papering over with three invented rows.
 */
export async function getShippingMethods(): Promise<AdminShippingMethodDto[]> {
  if (useMockData) return [];

  return api.get<AdminShippingMethodDto[]>('/shipping/methods', { auth: true }).catch(() => []);
}

/**
 * The loyalty club, as the owner configures it.
 *
 * A club with no tiers reads as off rather than as an error — that is the state
 * every shop starts in, and the storefront hides the page for it.
 */
export async function getLoyaltyProgramme(): Promise<LoyaltyProgrammeDto> {
  const empty: LoyaltyProgrammeDto = { enabled: false, tomanPerPoint: 10_000, tiers: [] };

  if (useMockData) return empty;

  return api.get<LoyaltyProgrammeDto>('/loyalty', { auth: true }).catch(() => empty);
}
