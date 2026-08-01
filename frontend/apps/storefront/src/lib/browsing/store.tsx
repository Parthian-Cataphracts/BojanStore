'use client';

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useReducer,
  type ReactNode,
} from 'react';
import type { Product } from '@/lib/api/types';

/**
 * Browsing history — recently viewed products and past search terms.
 *
 * Both screens that show this (57 and 90) were reading a fixture, so they
 * listed products the shopper may never have opened and searches they never
 * ran. Neither is shareable state, so unlike the catalogue filters this lives
 * in the browser rather than the URL, and unlike the cart it is disposable:
 * losing it costs nothing, so a storage failure is ignored rather than
 * reported.
 */

const STORAGE_KEY = 'bojan.browsing.v1';
const STORAGE_VERSION = 1;

/** Screen 57 shows a grid; more than this is history nobody scrolls to. */
const MAX_VIEWED = 20;
/** Screen 90 draws a short list. */
const MAX_TERMS = 10;

interface PersistedBrowsing {
  v: number;
  viewed: Product[];
  terms: string[];
}

interface BrowsingState {
  viewed: Product[];
  terms: string[];
  hydrated: boolean;
}

type BrowsingAction =
  | { type: 'hydrate'; viewed: Product[]; terms: string[] }
  | { type: 'view'; product: Product }
  | { type: 'search'; term: string }
  | { type: 'forgetTerm'; term: string }
  | { type: 'clearTerms' }
  | { type: 'clearViewed' };

const initialState: BrowsingState = { viewed: [], terms: [], hydrated: false };

function reducer(state: BrowsingState, action: BrowsingAction): BrowsingState {
  switch (action.type) {
    case 'hydrate': {
      // React runs a child's effects before its parent's, so the product page
      // records its view *before* this provider has read storage. Merging
      // rather than replacing keeps that view — replacing dropped it, which is
      // exactly the visit the shopper is making right now.
      const viewedIds = new Set(state.viewed.map((item) => item.id));
      const pendingTerms = new Set(state.terms);

      return {
        viewed: [
          ...state.viewed,
          ...action.viewed.filter((item) => !viewedIds.has(item.id)),
        ].slice(0, MAX_VIEWED),
        terms: [
          ...state.terms,
          ...action.terms.filter((term) => !pendingTerms.has(term)),
        ].slice(0, MAX_TERMS),
        hydrated: true,
      };
    }

    case 'view':
      // Most recent first, and a product seen twice moves up rather than
      // appearing twice.
      return {
        ...state,
        viewed: [
          action.product,
          ...state.viewed.filter((item) => item.id !== action.product.id),
        ].slice(0, MAX_VIEWED),
      };

    case 'search': {
      const term = action.term.trim();
      if (!term) return state;
      return {
        ...state,
        terms: [term, ...state.terms.filter((item) => item !== term)].slice(0, MAX_TERMS),
      };
    }

    case 'forgetTerm':
      return { ...state, terms: state.terms.filter((item) => item !== action.term) };

    case 'clearTerms':
      return { ...state, terms: [] };

    case 'clearViewed':
      return { ...state, viewed: [] };

    default:
      return state;
  }
}

/** Storage is untrusted — keep only entries shaped like a product. */
function isProduct(value: unknown): value is Product {
  if (typeof value !== 'object' || value === null) return false;
  const product = value as Record<string, unknown>;
  return (
    typeof product.id === 'string' &&
    typeof product.slug === 'string' &&
    typeof product.title === 'string' &&
    typeof product.image === 'string' &&
    typeof product.price === 'number' &&
    Number.isFinite(product.price)
  );
}

function readStorage(): { viewed: Product[]; terms: string[] } | null {
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    if (!raw) return null;

    const parsed = JSON.parse(raw) as PersistedBrowsing;
    if (parsed.v !== STORAGE_VERSION) return null;

    return {
      viewed: Array.isArray(parsed.viewed)
        ? parsed.viewed.filter(isProduct).slice(0, MAX_VIEWED)
        : [],
      terms: Array.isArray(parsed.terms)
        ? parsed.terms.filter((term): term is string => typeof term === 'string' && term.length > 0)
            .slice(0, MAX_TERMS)
        : [],
    };
  } catch {
    return null;
  }
}

export interface BrowsingContextValue {
  viewed: Product[];
  terms: string[];
  hydrated: boolean;
  recordView: (product: Product) => void;
  recordSearch: (term: string) => void;
  forgetTerm: (term: string) => void;
  clearTerms: () => void;
  clearViewed: () => void;
}

const BrowsingContext = createContext<BrowsingContextValue | null>(null);

export function BrowsingProvider({
  children,
  seedViewed,
  seedTerms,
}: {
  children: ReactNode;
  /** Demo history for a first-time visitor, so screens 57 and 90 render
   *  populated the way the design draws them. Dropped once real history
   *  exists. */
  seedViewed?: Product[];
  seedTerms?: string[];
}) {
  const [state, dispatch] = useReducer(reducer, initialState);

  useEffect(() => {
    const stored = readStorage();
    dispatch({
      type: 'hydrate',
      viewed: stored?.viewed ?? seedViewed ?? [],
      terms: stored?.terms ?? seedTerms ?? [],
    });
    // The seeds are server-rendered constants; re-seeding would undo history.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    if (!state.hydrated) return;
    try {
      const payload: PersistedBrowsing = {
        v: STORAGE_VERSION,
        viewed: state.viewed,
        terms: state.terms,
      };
      window.localStorage.setItem(STORAGE_KEY, JSON.stringify(payload));
    } catch {
      // History is disposable — losing it is not worth telling anyone about.
    }
  }, [state]);

  // Stable identity: the product page calls this from an effect keyed on it.
  const recordView = useCallback((product: Product) => {
    dispatch({ type: 'view', product });
  }, []);

  const recordSearch = useCallback((term: string) => {
    dispatch({ type: 'search', term });
  }, []);

  const value = useMemo<BrowsingContextValue>(
    () => ({
      viewed: state.viewed,
      terms: state.terms,
      hydrated: state.hydrated,
      recordView,
      recordSearch,
      forgetTerm: (term) => dispatch({ type: 'forgetTerm', term }),
      clearTerms: () => dispatch({ type: 'clearTerms' }),
      clearViewed: () => dispatch({ type: 'clearViewed' }),
    }),
    [state, recordView, recordSearch],
  );

  return <BrowsingContext.Provider value={value}>{children}</BrowsingContext.Provider>;
}

export function useBrowsing(): BrowsingContextValue {
  const context = useContext(BrowsingContext);
  if (!context) throw new Error('useBrowsing must be used inside <BrowsingProvider>.');
  return context;
}
